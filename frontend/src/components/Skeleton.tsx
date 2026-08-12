export function TableSkeleton({ columns, rows = 4 }: { columns: number; rows?: number }) {
  return (
    <table>
      <tbody>
        {Array.from({ length: rows }).map((_, r) => (
          <tr key={r}>
            {Array.from({ length: columns }).map((__, c) => (
              <td key={c}>
                <div className="skeleton-bar" style={{ width: `${55 + ((r * 7 + c * 13) % 35)}%` }} />
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  );
}
