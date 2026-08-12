import { useState, type ReactElement } from "react";
import { AuditLogPanel } from "../components/AuditLogPanel";
import { RolesPanel } from "../components/RolesPanel";
import { UsersPanel } from "../components/UsersPanel";
import { IconAudit, IconLogout, IconRoles, IconShieldLogo, IconUsers } from "../components/icons";
import { useSession } from "../auth/SessionContext";

type Tab = "roles" | "users" | "audit";

const NAV: { id: Tab; label: string; description: string; icon: (props: { size?: number }) => ReactElement }[] = [
  { id: "roles", label: "Roles & permissions", description: "Manage RBAC roles and the permission catalog for your organization.", icon: IconRoles },
  { id: "users", label: "Users", description: "View and provision users within your organization.", icon: IconUsers },
  { id: "audit", label: "Audit log", description: "Security-relevant events, scoped to your organization.", icon: IconAudit },
];

export function DashboardPage() {
  const { session, logout } = useSession();
  const [tab, setTab] = useState<Tab>("roles");

  if (!session) return null;

  const active = NAV.find((n) => n.id === tab)!;
  const initials = session.email.slice(0, 2).toUpperCase();

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <IconShieldLogo />
          <span>SecureAccess</span>
        </div>

        <nav className="sidebar-nav">
          {NAV.map(({ id, label, icon: Icon }) => (
            <button key={id} className={id === tab ? "nav-item active" : "nav-item"} onClick={() => setTab(id)}>
              <Icon />
              {label}
            </button>
          ))}
        </nav>

        <div className="sidebar-footer">
          <div className="user-card">
            <div className="avatar">{initials}</div>
            <div className="user-card-info">
              <div className="user-card-email" title={session.email}>
                {session.email}
              </div>
              <div className="chip-row">
                {session.roles.map((r) => (
                  <span className="chip" key={r}>
                    {r}
                  </span>
                ))}
              </div>
            </div>
          </div>
          <button className="ghost logout-button" onClick={() => logout()}>
            <IconLogout />
            Log out
          </button>
        </div>
      </aside>

      <main className="content">
        <header className="content-header">
          <h1>{active.label}</h1>
          <p className="muted">{active.description}</p>
        </header>

        <div className="panel">
          {tab === "roles" && <RolesPanel />}
          {tab === "users" && <UsersPanel />}
          {tab === "audit" && <AuditLogPanel />}
        </div>
      </main>
    </div>
  );
}
