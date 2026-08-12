using System.Security.Claims;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SecureAccess.Api.Services;

namespace SecureAccess.Api.Features.Auth;

[ApiController]
[Route("auth/google")]
public class GoogleAuthController : ControllerBase
{
    private readonly IExternalAuthService _externalAuthService;
    private readonly IOAuthCodeStore _codeStore;
    private readonly FrontendOptions _frontendOptions;

    public GoogleAuthController(IExternalAuthService externalAuthService, IOAuthCodeStore codeStore, IOptions<FrontendOptions> frontendOptions)
    {
        _externalAuthService = externalAuthService;
        _codeStore = codeStore;
        _frontendOptions = frontendOptions.Value;
    }

    [HttpGet("login")]
    [EnableRateLimiting("login")]
    public IActionResult Login()
    {
        var props = new AuthenticationProperties { RedirectUri = "/auth/google/complete" };
        return Challenge(props, GoogleDefaults.AuthenticationScheme);
    }

    // Landed on after Google's own /auth/google/callback (handled internally by the Google
    // handler) signs the user into the "External" cookie scheme.
    [HttpGet("complete")]
    public async Task<IActionResult> Complete(CancellationToken ct)
    {
        var result = await HttpContext.AuthenticateAsync("External");
        if (!result.Succeeded || result.Principal is null)
        {
            return RedirectToFrontend(error: "oauth_failed");
        }

        await HttpContext.SignOutAsync("External");

        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        var emailVerifiedClaim = result.Principal.FindFirstValue("email_verified");
        var emailVerified = emailVerifiedClaim is null || bool.TryParse(emailVerifiedClaim, out var verified) && verified;

        if (string.IsNullOrEmpty(email) || !emailVerified)
        {
            return RedirectToFrontend(error: "email_not_verified");
        }

        var tokens = await _externalAuthService.CompleteLoginAsync(email, "google", ct);
        var code = _codeStore.Store(tokens);

        return RedirectToFrontend(code: code);
    }

    [HttpPost("exchange")]
    [EnableRateLimiting("login")]
    public ActionResult<AuthResponse> Exchange(ExchangeCodeRequest request)
    {
        var tokens = _codeStore.TryConsume(request.Code);
        if (tokens is null)
        {
            return BadRequest("Invalid or expired sign-in code.");
        }
        return Ok(new AuthResponse(tokens.AccessToken, tokens.RefreshToken, tokens.RefreshTokenExpiresAt));
    }

    private RedirectResult RedirectToFrontend(Guid? code = null, string? error = null)
    {
        var query = code is not null
            ? $"oauth_code={HttpUtility.UrlEncode(code.ToString())}"
            : $"oauth_error={HttpUtility.UrlEncode(error)}";
        return Redirect($"{_frontendOptions.BaseUrl}/?{query}");
    }
}
