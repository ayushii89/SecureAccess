// Mirrors the record DTOs in SecureAccess.Api/Features/**.

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
}

export interface RoleResponse {
  id: string;
  name: string;
  permissions: string[];
}

export interface UserResponse {
  id: string;
  email: string;
  roles: string[];
}

export interface AuditLogResponse {
  id: string;
  userId: string | null;
  eventType: string;
  metadata: string | null;
  createdAt: string;
}

export interface Session {
  accessToken: string;
  refreshToken: string;
  email: string;
  roles: string[];
}
