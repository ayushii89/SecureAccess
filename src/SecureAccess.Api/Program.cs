using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SecureAccess.Api.Authorization;
using SecureAccess.Api.Data;
using SecureAccess.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Railway (and most PaaS hosts) assign the listen port via $PORT rather than a fixed one.
var assignedPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(assignedPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{assignedPort}");
}

// Railway's Postgres plugin injects DATABASE_URL as a postgres:// URI rather than the
// keyword=value format Npgsql expects; only used as a fallback when ConnectionStrings:Default
// isn't set, so local/dev config (which sets it directly) is unaffected.
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (string.IsNullOrEmpty(builder.Configuration.GetConnectionString("Default")) && !string.IsNullOrEmpty(databaseUrl))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);
    var npgsqlConnectionString =
        $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};" +
        $"Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
    builder.Configuration["ConnectionStrings:Default"] = npgsqlConnectionString;
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));
builder.Services.Configure<FrontendOptions>(builder.Configuration.GetSection(FrontendOptions.SectionName));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
});

const string FrontendCorsPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        // The frontend sends the JWT as an Authorization header, not cookies, so no
        // credentials/cookies need to cross origins — AllowAnyHeader/Method is enough.
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IExternalAuthService, ExternalAuthService>();
builder.Services.AddSingleton<IOAuthCodeStore, OAuthCodeStore>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Without this, the framework silently remaps "sub" -> ClaimTypes.NameIdentifier and
    // "email" -> ClaimTypes.Email on incoming tokens, so claim lookups by JWT-standard names
    // (e.g. JwtRegisteredClaimNames.Sub) would fail even though the token has the claim.
    options.MapInboundClaims = false;

    // Read from builder.Configuration lazily (this delegate runs on first options resolution,
    // not at startup) so it picks up any config sources layered on after this point — e.g.
    // WebApplicationFactory's test overrides — the same way the AppDbContext connection
    // string below is resolved lazily rather than captured into a local variable up front.
    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? throw new InvalidOperationException("Jwt configuration section is missing.");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtOptions.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtOptions.SigningKey)),
        ClockSkew = TimeSpan.FromSeconds(30),
    };
})
// "External" is a short-lived cookie used only to carry the OAuth handshake between Google's
// redirect back to us and our own /auth/google/complete handler — it never becomes the app's
// session mechanism, which stays the JWT bearer scheme above (DefaultAuthenticateScheme is
// untouched, so [Authorize] on API endpoints is unaffected by any of this).
.AddCookie("External")
.AddGoogle(options =>
{
    options.SignInScheme = "External";

    // AddGoogle's handler validates ClientId/ClientSecret as non-empty on every request (not
    // just Google ones) — it has to probe each request's path against its CallbackPath. If
    // Google isn't configured yet (e.g. this environment), a placeholder keeps the rest of the
    // API working; anyone who actually clicks "Continue with Google" just gets a clear error
    // from Google's own authorize endpoint instead of every request in the app 500ing.
    var clientId = builder.Configuration["Google:ClientId"];
    var clientSecret = builder.Configuration["Google:ClientSecret"];
    options.ClientId = string.IsNullOrWhiteSpace(clientId) ? "not-configured" : clientId;
    options.ClientSecret = string.IsNullOrWhiteSpace(clientSecret) ? "not-configured" : clientSecret;
    options.CallbackPath = "/auth/google/callback";
    options.ClaimActions.Add(new JsonKeyClaimAction("email_verified", System.Security.Claims.ClaimValueTypes.String, "email_verified"));
});

builder.Services.AddAuthorizationBuilder();
builder.Services.AddAuthorization(options => options.AddPermissionPolicies());
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

// Per-IP rate limits on the unauthenticated auth endpoints — the ones brute-force/credential-
// stuffing/registration-spam attacks actually hit. Everything else sits behind a JWT already.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static string ClientKey(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    options.AddPolicy("login", httpContext =>
    {
        var opts = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        return RateLimitPartition.GetSlidingWindowLimiter(ClientKey(httpContext), _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = opts.LoginPermitLimit,
            Window = TimeSpan.FromSeconds(opts.LoginWindowSeconds),
            SegmentsPerWindow = 4,
            QueueLimit = 0,
        });
    });

    options.AddPolicy("register", httpContext =>
    {
        var opts = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        return RateLimitPartition.GetSlidingWindowLimiter(ClientKey(httpContext), _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = opts.RegisterPermitLimit,
            Window = TimeSpan.FromSeconds(opts.RegisterWindowSeconds),
            SegmentsPerWindow = 4,
            QueueLimit = 0,
        });
    });

    options.AddPolicy("refresh", httpContext =>
    {
        var opts = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        return RateLimitPartition.GetSlidingWindowLimiter(ClientKey(httpContext), _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = opts.RefreshPermitLimit,
            Window = TimeSpan.FromSeconds(opts.RefreshWindowSeconds),
            SegmentsPerWindow = 4,
            QueueLimit = 0,
        });
    });
});

var app = builder.Build();

// Swagger stays on in every environment, including the deployed demo — this is a portfolio
// project meant to be explored, not an internal API that should hide its shape.
app.UseSwagger();
app.UseSwaggerUI();

// Single-instance deployment (Railway/demo), so running migrations on startup has no
// multi-instance race risk. Skipped only for the WebApplicationFactory test environment,
// which drives this explicitly in ApiFactory.InitializeAsync instead.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.EnsurePermissionCatalogAsync(db);
}

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

// Exposed so WebApplicationFactory<Program> in the integration test project can bootstrap the app.
public partial class Program;
