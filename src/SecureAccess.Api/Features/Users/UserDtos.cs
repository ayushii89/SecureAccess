namespace SecureAccess.Api.Features.Users;

public record CreateUserRequest(string Email, string Password, Guid? RoleId);
public record UserResponse(Guid Id, string Email, IEnumerable<string> Roles);
