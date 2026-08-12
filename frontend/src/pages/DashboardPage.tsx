import { useState } from "react";
import { AuditLogPanel } from "../components/AuditLogPanel";
import { RolesPanel } from "../components/RolesPanel";
import { UsersPanel } from "../components/UsersPanel";
import { useSession } from "../auth/SessionContext";

type Tab = "roles" | "users" | "audit";

export function DashboardPage() {
  const { session, logout } = useSession();
  const [tab, setTab] = useState<Tab>("roles");

  if (!session) return null;

  return (
    <div className="dashboard">
      <header className="topbar">
        <div>
          <strong>SecureAccess</strong>
          <span className="muted"> · {session.email}</span>
        </div>
        <div className="chip-row">
          {session.roles.map((r) => (
            <span className="chip" key={r}>
              {r}
            </span>
          ))}
          <button className="ghost" onClick={() => logout()}>
            Log out
          </button>
        </div>
      </header>

      <nav className="tabs">
        <button className={tab === "roles" ? "tab active" : "tab"} onClick={() => setTab("roles")}>
          Roles &amp; permissions
        </button>
        <button className={tab === "users" ? "tab active" : "tab"} onClick={() => setTab("users")}>
          Users
        </button>
        <button className={tab === "audit" ? "tab active" : "tab"} onClick={() => setTab("audit")}>
          Audit log
        </button>
      </nav>

      <main className="panel">
        {tab === "roles" && <RolesPanel />}
        {tab === "users" && <UsersPanel />}
        {tab === "audit" && <AuditLogPanel />}
      </main>
    </div>
  );
}
