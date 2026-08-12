namespace SecureAccess.Api.Features.Roles;

public record RoleResponse(Guid Id, string Name, IEnumerable<string> Permissions);
public record CreateRoleRequest(string Name, IEnumerable<string> PermissionNames);
public record AssignPermissionRequest(string PermissionName);
public record AssignRoleRequest(Guid UserId, Guid RoleId);
