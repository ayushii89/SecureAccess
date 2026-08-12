import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { api, ApiError } from "../api/client";
import { decodeAccessToken } from "../api/jwt";
import type { Session } from "../api/types";

const STORAGE_KEY = "secureaccess.session";

interface SessionContextValue {
  session: Session | null;
  register: (organizationName: string, email: string, password: string) => Promise<void>;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  hasRole: (role: string) => boolean;
  // True while a Google redirect's ?oauth_code is being exchanged for a real session.
  completingOAuth: boolean;
  oauthError: string | null;
}

const SessionContext = createContext<SessionContextValue | null>(null);

function toSession(accessToken: string, refreshToken: string): Session {
  const decoded = decodeAccessToken(accessToken);
  return { accessToken, refreshToken, email: decoded.email, roles: decoded.roles };
}

function loadSession(): Session | null {
  const raw = localStorage.getItem(STORAGE_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as Session;
  } catch {
    return null;
  }
}

export function SessionProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(loadSession);
  const [completingOAuth, setCompletingOAuth] = useState(false);
  const [oauthError, setOauthError] = useState<string | null>(null);

  function persist(next: Session | null) {
    setSession(next);
    if (next) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  }

  // Handles the redirect back from GoogleAuthController.Complete: it lands on the SPA's own
  // root URL with either ?oauth_code=... (swap for a real session) or ?oauth_error=....
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const code = params.get("oauth_code");
    const error = params.get("oauth_error");
    if (!code && !error) return;

    window.history.replaceState({}, "", window.location.pathname);

    if (error) {
      setOauthError(error === "email_not_verified" ? "Your Google email isn't verified." : "Google sign-in failed. Please try again.");
      return;
    }

    setCompletingOAuth(true);
    api
      .exchangeOAuthCode(code!)
      .then((auth) => persist(toSession(auth.accessToken, auth.refreshToken)))
      .catch((err) => setOauthError(err instanceof ApiError ? err.message : "Google sign-in failed. Please try again."))
      .finally(() => setCompletingOAuth(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const value = useMemo<SessionContextValue>(
    () => ({
      session,
      completingOAuth,
      oauthError,
      async register(organizationName, email, password) {
        const auth = await api.register(organizationName, email, password);
        persist(toSession(auth.accessToken, auth.refreshToken));
      },
      async login(email, password) {
        const auth = await api.login(email, password);
        persist(toSession(auth.accessToken, auth.refreshToken));
      },
      async logout() {
        if (session) {
          await api.logout(session.refreshToken).catch(() => undefined);
        }
        persist(null);
      },
      hasRole(role: string) {
        return session?.roles.includes(role) ?? false;
      },
    }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [session, completingOAuth, oauthError],
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession() {
  const ctx = useContext(SessionContext);
  if (!ctx) throw new Error("useSession must be used within SessionProvider");
  return ctx;
}
