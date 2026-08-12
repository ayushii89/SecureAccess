export interface DecodedToken {
  email: string;
  roles: string[];
  exp: number;
}

// Client-side decode only, for display purposes — the server is the source of truth for
// validation. No signature check needed here since we never trust this for authorization.
export function decodeAccessToken(token: string): DecodedToken {
  const payload = token.split(".")[1];
  const json = atob(payload.replace(/-/g, "+").replace(/_/g, "/"));
  const claims = JSON.parse(json);

  const roleClaim = claims["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"];
  const roles = Array.isArray(roleClaim) ? roleClaim : roleClaim ? [roleClaim] : [];

  return { email: claims.email, roles, exp: claims.exp };
}
