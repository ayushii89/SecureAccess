import { useEffect, useState } from "react";
import { api, ApiError, PERMISSION_CATALOG } from "../api/client";
import type { RoleResponse } from "../api/types";
import { useSession } from "../auth/SessionContext";

export function RolesPanel() {
  const { session } = useSession();
  const [roles, setRoles] = useState<RoleResponse[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [newRoleName, setNewRoleName] = useState("");
  const [newRolePermissions, setNewRolePermissions] = useState<string[]>([]);
  const [creating, setCreating] = useState(false);

  async function load() {
    if (!session) return;
    try {
      setRoles(await api.getRoles(session.accessToken));
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? describeError(err) : "Failed to load roles.");
    }
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session]);

  function togglePermission(name: string) {
    setNewRolePermissions((prev) => (prev.includes(name) ? prev.filter((p) => p !== name) : [...prev, name]));
  }

  async function createRole() {
    if (!session || !newRoleName.trim()) return;
    setCreating(true);
    try {
      await api.createRole(session.accessToken, newRoleName.trim(), newRolePermissions);
      setNewRoleName("");
      setNewRolePermissions([]);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? describeError(err) : "Failed to create role.");
    } finally {
      setCreating(false);
    }
  }

  if (error) return <div className="error">{error}</div>;
  if (!roles) return <p className="muted">Loading roles…</p>;

  return (
    <div>
      <table>
        <thead>
          <tr>
            <th>Role</th>
            <th>Permissions</th>
          </tr>
        </thead>
        <tbody>
          {roles.map((role) => (
            <tr key={role.id}>
              <td>{role.name}</td>
              <td>
                <div className="chip-row">
                  {role.permissions.map((p) => (
                    <span className="chip" key={p}>
                      {p}
                    </span>
                  ))}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="panel-section">
        <h3>Create role</h3>
        <div className="form-row">
          <input placeholder="Role name" value={newRoleName} onChange={(e) => setNewRoleName(e.target.value)} />
        </div>
        <div className="chip-row selectable">
          {PERMISSION_CATALOG.map((p) => (
            <label className={newRolePermissions.includes(p) ? "chip chip-toggle selected" : "chip chip-toggle"} key={p}>
              <input type="checkbox" checked={newRolePermissions.includes(p)} onChange={() => togglePermission(p)} />
              {p}
            </label>
          ))}
        </div>
        <button className="primary" onClick={createRole} disabled={creating || !newRoleName.trim()}>
          {creating ? "Creating…" : "Create role"}
        </button>
      </div>
    </div>
  );
}

function describeError(err: ApiError): string {
  if (err.status === 403) return "You don't have permission to manage roles (requires roles:manage).";
  return err.message;
}
