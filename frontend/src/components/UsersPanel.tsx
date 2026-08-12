import { useEffect, useState } from "react";
import { api, ApiError } from "../api/client";
import type { RoleResponse, UserResponse } from "../api/types";
import { useSession } from "../auth/SessionContext";

export function UsersPanel() {
  const { session } = useSession();
  const [users, setUsers] = useState<UserResponse[] | null>(null);
  const [roles, setRoles] = useState<RoleResponse[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [roleId, setRoleId] = useState("");
  const [creating, setCreating] = useState(false);

  async function load() {
    if (!session) return;
    try {
      const [userList, roleList] = await Promise.all([api.getUsers(session.accessToken), api.getRoles(session.accessToken).catch(() => [])]);
      setUsers(userList);
      setRoles(roleList);
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? describeError(err) : "Failed to load users.");
    }
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session]);

  async function createUser() {
    if (!session || !email.trim() || !password) return;
    setCreating(true);
    try {
      await api.createUser(session.accessToken, email.trim(), password, roleId || null);
      setEmail("");
      setPassword("");
      setRoleId("");
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? describeError(err) : "Failed to create user.");
    } finally {
      setCreating(false);
    }
  }

  if (error) return <div className="error">{error}</div>;
  if (!users) return <p className="muted">Loading users…</p>;

  return (
    <div>
      <table>
        <thead>
          <tr>
            <th>Email</th>
            <th>Roles</th>
          </tr>
        </thead>
        <tbody>
          {users.map((u) => (
            <tr key={u.id}>
              <td>{u.email}</td>
              <td>
                <div className="chip-row">
                  {u.roles.map((r) => (
                    <span className="chip" key={r}>
                      {r}
                    </span>
                  ))}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="panel-section">
        <h3>Create user</h3>
        <div className="form-row">
          <input type="email" placeholder="Email" value={email} onChange={(e) => setEmail(e.target.value)} />
          <input type="password" placeholder="Password" value={password} onChange={(e) => setPassword(e.target.value)} minLength={8} />
          <select value={roleId} onChange={(e) => setRoleId(e.target.value)}>
            <option value="">No role</option>
            {roles.map((r) => (
              <option value={r.id} key={r.id}>
                {r.name}
              </option>
            ))}
          </select>
          <button className="primary" onClick={createUser} disabled={creating || !email.trim() || !password}>
            {creating ? "Creating…" : "Create user"}
          </button>
        </div>
      </div>
    </div>
  );
}

function describeError(err: ApiError): string {
  if (err.status === 403) return "You don't have permission to view/create users (requires users:read / users:create).";
  return err.message;
}
