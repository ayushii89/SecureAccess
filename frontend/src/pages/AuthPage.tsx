import { useState, type FormEvent } from "react";
import { ApiError } from "../api/client";
import { useSession } from "../auth/SessionContext";

export function AuthPage() {
  const [mode, setMode] = useState<"login" | "register">("login");
  const [organizationName, setOrganizationName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const { login, register } = useSession();

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      if (mode === "login") {
        await login(email, password);
      } else {
        await register(organizationName, email, password);
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="auth-page">
      <div className="auth-card">
        <h1>SecureAccess</h1>
        <p className="subtitle">Enterprise identity &amp; access management</p>

        <div className="tabs">
          <button className={mode === "login" ? "tab active" : "tab"} onClick={() => setMode("login")} type="button">
            Log in
          </button>
          <button className={mode === "register" ? "tab active" : "tab"} onClick={() => setMode("register")} type="button">
            Register organization
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          {mode === "register" && (
            <label>
              Organization name
              <input value={organizationName} onChange={(e) => setOrganizationName(e.target.value)} required />
            </label>
          )}
          <label>
            Email
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          </label>
          <label>
            Password
            <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required minLength={8} />
          </label>

          {error && <div className="error">{error}</div>}

          <button type="submit" className="primary" disabled={loading}>
            {loading ? "Please wait…" : mode === "login" ? "Log in" : "Create organization"}
          </button>
        </form>
      </div>
    </div>
  );
}
