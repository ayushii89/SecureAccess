import type { AuditLogResponse, AuthResponse, RoleResponse, UserResponse } from "./types";

const BASE_URL = import.meta.env.VITE_API_BASE_URL as string;

export class ApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

async function request<T>(
  path: string,
  options: { method?: string; body?: unknown; accessToken?: string } = {},
): Promise<T> {
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (options.accessToken) {
    headers.Authorization = `Bearer ${options.accessToken}`;
  }

  const response = await fetch(`${BASE_URL}${path}`, {
    method: options.method ?? "GET",
    headers,
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  });

  if (!response.ok) {
    const text = await response.text();
    if (response.status === 429) {
      throw new ApiError(429, "Too many attempts — please wait a moment and try again.");
    }
    throw new ApiError(response.status, text || response.statusText);
  }

  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

export const api = {
  register: (organizationName: string, email: string, password: string) =>
    request<AuthResponse>("/auth/register", { method: "POST", body: { organizationName, email, password } }),

  login: (email: string, password: string) =>
    request<AuthResponse>("/auth/login", { method: "POST", body: { email, password } }),

  refresh: (refreshToken: string) => request<AuthResponse>("/auth/refresh", { method: "POST", body: { refreshToken } }),

  logout: (refreshToken: string) => request<void>("/auth/logout", { method: "POST", body: { refreshToken } }),

  getRoles: (accessToken: string) => request<RoleResponse[]>("/roles", { accessToken }),

  createRole: (accessToken: string, name: string, permissionNames: string[]) =>
    request<RoleResponse>("/roles", { method: "POST", accessToken, body: { name, permissionNames } }),

  assignPermission: (accessToken: string, roleId: string, permissionName: string) =>
    request<void>(`/roles/${roleId}/permissions`, { method: "POST", accessToken, body: { permissionName } }),

  assignRole: (accessToken: string, userId: string, roleId: string) =>
    request<void>("/roles/assign", { method: "POST", accessToken, body: { userId, roleId } }),

  getUsers: (accessToken: string) => request<UserResponse[]>("/users", { accessToken }),

  createUser: (accessToken: string, email: string, password: string, roleId: string | null) =>
    request<UserResponse>("/users", { method: "POST", accessToken, body: { email, password, roleId } }),

  getAuditLogs: (accessToken: string) => request<AuditLogResponse[]>("/audit-logs", { accessToken }),
};

export const PERMISSION_CATALOG = [
  "users:create",
  "users:read",
  "users:delete",
  "users:manage_roles",
  "roles:manage",
  "audit:read",
  "projects:read",
];
