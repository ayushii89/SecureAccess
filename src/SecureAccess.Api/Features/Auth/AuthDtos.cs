namespace SecureAccess.Api.Features.Auth;

public record RegisterRequest(string OrganizationName, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);

public record AuthResponse(string AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);
