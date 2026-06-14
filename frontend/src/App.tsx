import { useEffect, useState, type FormEvent } from "react";
import {
  clearToken,
  getCurrentUser,
  getReadiness,
  getStoredToken,
  login,
  storeToken
} from "./api";
import { AdminScreen } from "./AdminScreen";
import { DashboardScreen } from "./DashboardScreen";
import { KnowledgeBaseScreen } from "./KnowledgeBaseScreen";
import { TicketsScreen } from "./TicketsScreen";
import type { HealthStatus, UserProfile } from "./types";

type ViewKey = "dashboard" | "tickets" | "knowledge-base" | "admin";

const navItems: Array<{ key: ViewKey; label: string; description: string }> = [
  { key: "dashboard", label: "Command Center", description: "SLA, workload and system pulse" },
  { key: "tickets", label: "Tickets", description: "Queues, filters and case work" },
  { key: "knowledge-base", label: "Knowledge Base", description: "FAQ and article operations" },
  { key: "admin", label: "Admin", description: "Users, roles and policies" }
];

export function App() {
  const [token, setToken] = useState<string | null>(() => getStoredToken());
  const [user, setUser] = useState<UserProfile | null>(null);
  const [activeView, setActiveView] = useState<ViewKey>("dashboard");
  const [isBootstrapping, setIsBootstrapping] = useState(true);
  const [readiness, setReadiness] = useState<HealthStatus>("Unhealthy");

  useEffect(() => {
    let isActive = true;

    async function bootstrap() {
      try {
        const [ready, profile] = await Promise.all([
          getReadiness(),
          token ? getCurrentUser(token) : Promise.resolve(null)
        ]);

        if (!isActive) {
          return;
        }

        setReadiness(ready);
        setUser(profile);
      } catch {
        if (isActive) {
          clearToken();
          setToken(null);
          setUser(null);
        }
      } finally {
        if (isActive) {
          setIsBootstrapping(false);
        }
      }
    }

    bootstrap();

    return () => {
      isActive = false;
    };
  }, [token]);

  async function handleLogin(email: string, password: string) {
    const response = await login(email, password);
    storeToken(response.token);
    setToken(response.token);
    setUser(response.user);
  }

  function handleLogout() {
    clearToken();
    setToken(null);
    setUser(null);
  }

  if (isBootstrapping) {
    return <BootScreen />;
  }

  if (!token || !user) {
    return <LoginScreen readiness={readiness} onLogin={handleLogin} />;
  }

  return (
    <AppShell
      activeView={activeView}
      readiness={readiness}
      token={token}
      user={user}
      onNavigate={setActiveView}
      onLogout={handleLogout}
    />
  );
}

function BootScreen() {
  return (
    <main className="boot-screen">
      <div className="boot-mark">SP</div>
      <p>Preparing support operations...</p>
    </main>
  );
}

function LoginScreen({
  readiness,
  onLogin
}: {
  readiness: HealthStatus;
  onLogin: (email: string, password: string) => Promise<void>;
}) {
  const [email, setEmail] = useState("admin@supportpilot.local");
  const [password, setPassword] = useState("Admin123!");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      await onLogin(email, password);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="login-page">
      <section className="login-hero">
        <div className="eyebrow">SupportPilot Frontend Shell</div>
        <h1>Work the queue before the queue works you.</h1>
        <p>
          A focused operations surface for tickets, SLA pressure, live updates and support knowledge.
        </p>
        <div className="hero-grid" aria-label="Product capabilities">
          <MetricCard label="SLA engine" value="30m" detail="critical first response" />
          <MetricCard label="Workflow" value="6" detail="ticket states ready" />
          <MetricCard label="Realtime" value="SignalR" detail="planned dashboard channel" />
        </div>
      </section>

      <section className="login-panel" aria-label="Sign in">
        <div className="panel-header">
          <span className={`status-dot status-${readiness.toLowerCase()}`} />
          <span>API readiness: {readiness}</span>
        </div>
        <h2>Enter workspace</h2>
        <form onSubmit={submit} className="login-form">
          <label>
            Email
            <input
              autoComplete="email"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
          </label>
          <label>
            Password
            <input
              autoComplete="current-password"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />
          </label>
          {error ? <p className="form-error">{error}</p> : null}
          <button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Signing in..." : "Sign in"}
          </button>
        </form>
      </section>
    </main>
  );
}

function AppShell({
  activeView,
  readiness,
  token,
  user,
  onNavigate,
  onLogout
}: {
  activeView: ViewKey;
  readiness: HealthStatus;
  token: string;
  user: UserProfile;
  onNavigate: (view: ViewKey) => void;
  onLogout: () => void;
}) {
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark">SP</span>
          <span>
            <strong>SupportPilot</strong>
            <small>Service desk control</small>
          </span>
        </div>
        <nav className="nav-list" aria-label="Main navigation">
          {navItems.map((item) => (
            <button
              key={item.key}
              className={item.key === activeView ? "nav-item nav-item-active" : "nav-item"}
              onClick={() => onNavigate(item.key)}
              type="button"
            >
              <span>{item.label}</span>
              <small>{item.description}</small>
            </button>
          ))}
        </nav>
        <div className="sidebar-footer">
          <span className={`status-dot status-${readiness.toLowerCase()}`} />
          <span>API {readiness}</span>
        </div>
      </aside>

      <div className="workspace">
        <header className="topbar">
          <div>
            <span className="eyebrow">Signed in</span>
            <h1>{currentTitle(activeView)}</h1>
          </div>
          <div className="user-card">
            <span>
              <strong>{user.displayName}</strong>
              <small>{user.roles.join(", ")}</small>
            </span>
            <button type="button" onClick={onLogout}>
              Logout
            </button>
          </div>
        </header>
        <main className="content-panel">
          {activeView === "dashboard" ? <DashboardScreen token={token} user={user} /> : null}
          {activeView === "tickets" ? <TicketsScreen token={token} user={user} /> : null}
          {activeView === "knowledge-base" ? <KnowledgeBaseScreen token={token} user={user} /> : null}
          {activeView === "admin" ? <AdminScreen token={token} user={user} /> : null}
        </main>
      </div>
    </div>
  );
}

function MetricCard({ label, value, detail }: { label: string; value: string; detail: string }) {
  return (
    <article className="metric-card">
      <span>{label}</span>
      <strong>{value}</strong>
      <small>{detail}</small>
    </article>
  );
}

function currentTitle(view: ViewKey): string {
  return navItems.find((item) => item.key === view)?.label ?? "SupportPilot";
}
