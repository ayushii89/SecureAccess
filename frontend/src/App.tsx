import { AuthPage } from "./pages/AuthPage";
import { DashboardPage } from "./pages/DashboardPage";
import { useSession } from "./auth/SessionContext";

function App() {
  const { session } = useSession();
  return session ? <DashboardPage /> : <AuthPage />;
}

export default App;
