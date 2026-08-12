import { useEffect, useState } from "react";
import { api, ApiError } from "../api/client";
import type { AuditLogResponse } from "../api/types";
import { useSession } from "../auth/SessionContext";

export function AuditLogPanel() {
  const { session } = useSession();
  const [logs, setLogs] = useState<AuditLogResponse[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!session) return;
    api
      .getAuditLogs(session.accessToken)
      .then(setLogs)
      .catch((err) => setError(err instanceof ApiError ? describeError(err) : "Failed to load audit logs."));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session]);

  if (error) return <div className="error">{error}</div>;
  if (!logs) return <p className="muted">Loading audit log…</p>;
  if (logs.length === 0) return <p className="muted">No events yet.</p>;

  return (
    <table>
      <thead>
        <tr>
          <th>Event</th>
          <th>Details</th>
          <th>When</th>
        </tr>
      </thead>
      <tbody>
        {logs.map((log) => (
          <tr key={log.id}>
            <td>
              <span className="chip event">{log.eventType}</span>
            </td>
            <td className="muted mono">{log.metadata ?? "—"}</td>
            <td className="muted">{new Date(log.createdAt).toLocaleString()}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function describeError(err: ApiError): string {
  if (err.status === 403) return "You don't have permission to view the audit log (requires audit:read).";
  return err.message;
}
