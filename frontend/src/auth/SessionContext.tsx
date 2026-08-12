import { createContext, useContext, useMemo, useState, type ReactNode } from "react";
import { api } from "../api/client";
import { decodeAccessToken } from "../api/jwt";
import type { Session } from "../api/types";

const STORAGE_KEY = "secureaccess.session";

interface SessionContextValue {
  session: Session | null;
  register: (organizationName: string, email: string, password: string) => Promise<void>;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  hasRole: (role: string) => boolean;
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

  function persist(next: Session | null) {
    setSession(next);
    if (next) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  }

  const value = useMemo<SessionContextValue>(
    () => ({
      session,
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
    [session],
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession() {
  const ctx = useContext(SessionContext);
  if (!ctx) throw new Error("useSession must be used within SessionProvider");
  return ctx;
}
