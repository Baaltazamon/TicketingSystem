import { useEffect, useState } from "react";
import { getDashboardOverview } from "./api";
import { createTicketHubConnection } from "./realtime";
import { formatDate, formatPriority, formatStatus } from "./ticketFormat";
import type { DashboardBucket, DashboardOverview, DashboardTicket, UserProfile } from "./types";

type RealtimeState = "Connecting" | "Live" | "Reconnecting" | "Offline";

const realtimeEvents = [
  "ticketCreated",
  "ticketUpdated",
  "ticketAssigned",
  "commentAdded",
  "attachmentUploaded",
  "slaUpdated"
];

export function DashboardScreen({ token, user }: { token: string; user: UserProfile }) {
  const canViewDashboard = user.roles.some((role) => role === "Admin" || role === "Agent");
  const [overview, setOverview] = useState<DashboardOverview | null>(null);
  const [isLoading, setIsLoading] = useState(canViewDashboard);
  const [error, setError] = useState<string | null>(null);
  const [realtimeState, setRealtimeState] = useState<RealtimeState>("Offline");
  const [lastRealtimeEvent, setLastRealtimeEvent] = useState<string | null>(null);

  async function loadOverview() {
    if (!canViewDashboard) {
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      setOverview(await getDashboardOverview(token));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load dashboard.");
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    loadOverview();
  }, [canViewDashboard, token]);

  useEffect(() => {
    if (!canViewDashboard) {
      setRealtimeState("Offline");
      return;
    }

    let isActive = true;
    const connection = createTicketHubConnection(token);

    function refreshFromRealtime(eventName: string) {
      if (!isActive) {
        return;
      }

      setLastRealtimeEvent(`${eventName} at ${formatDate(new Date().toISOString())}`);
      loadOverview();
    }

    realtimeEvents.forEach((eventName) => {
      connection.on(eventName, () => refreshFromRealtime(eventName));
    });
    connection.onreconnecting(() => {
      if (isActive) {
        setRealtimeState("Reconnecting");
      }
    });
    connection.onreconnected(() => {
      if (isActive) {
        setRealtimeState("Live");
        loadOverview();
      }
    });
    connection.onclose(() => {
      if (isActive) {
        setRealtimeState("Offline");
      }
    });

    setRealtimeState("Connecting");
    connection
      .start()
      .then(() => {
        if (isActive) {
          setRealtimeState("Live");
        }
      })
      .catch((err) => {
        if (isActive) {
          setRealtimeState("Offline");
          setError(err instanceof Error ? err.message : "Realtime connection failed.");
        }
      });

    return () => {
      isActive = false;
      realtimeEvents.forEach((eventName) => connection.off(eventName));
      connection.stop();
    };
  }, [canViewDashboard, token]);

  if (!canViewDashboard) {
    return (
      <section className="dashboard-shell">
        <article className="roadmap-panel">
          <h2>Support dashboard is restricted</h2>
          <p className="empty-state">
            The operational dashboard is available to Admin and Agent users because it exposes global workload,
            assignment and SLA pressure.
          </p>
        </article>
      </section>
    );
  }

  if (isLoading && !overview) {
    return <div className="empty-state">Loading support operations dashboard...</div>;
  }

  return (
    <section className="dashboard-shell">
      {error ? <div className="notice notice-error">{error}</div> : null}
      <header className="dashboard-hero">
        <div>
          <span className="eyebrow">Live operations</span>
          <h2>Queue pressure, SLA risk and ticket movement.</h2>
          <p>
            Snapshot generated {overview ? formatDate(overview.generatedAt) : "not yet"}.
            {lastRealtimeEvent ? ` Last realtime event: ${lastRealtimeEvent}.` : ""}
          </p>
        </div>
        <div className={`realtime-pill realtime-${realtimeState.toLowerCase()}`}>
          <span className="status-dot" />
          {realtimeState}
        </div>
      </header>

      {overview ? (
        <>
          <section className="dashboard-metrics" aria-label="Support workload metrics">
            <DashboardMetric label="Open tickets" value={overview.openTickets} detail={`${overview.totalTickets} total`} />
            <DashboardMetric label="Overdue SLA" value={overview.overdueTickets} detail={`${overview.dueSoonTickets} due soon`} tone="danger" />
            <DashboardMetric label="Unassigned" value={overview.unassignedTickets} detail="waiting for owner" tone="warning" />
            <DashboardMetric label="Critical" value={overview.criticalTickets} detail={`${overview.highPriorityTickets} high priority`} tone="danger" />
          </section>

          <section className="dashboard-grid">
            <DistributionPanel title="Status mix" buckets={overview.byStatus} formatter={formatStatus} />
            <DistributionPanel title="Priority mix" buckets={overview.byPriority} formatter={formatPriority} />
            <TicketPulsePanel title="Recent movement" tickets={overview.recentTickets} emptyText="No ticket activity yet." />
            <TicketPulsePanel title="SLA breach feed" tickets={overview.slaBreaches} emptyText="No breached SLA tickets." isBreachList />
          </section>
        </>
      ) : null}
    </section>
  );
}

function DashboardMetric({
  label,
  value,
  detail,
  tone = "neutral"
}: {
  label: string;
  value: number;
  detail: string;
  tone?: "danger" | "neutral" | "warning";
}) {
  return (
    <article className={`dashboard-metric dashboard-metric-${tone}`}>
      <span>{label}</span>
      <strong>{value}</strong>
      <small>{detail}</small>
    </article>
  );
}

function DistributionPanel({
  title,
  buckets,
  formatter
}: {
  title: string;
  buckets: DashboardBucket[];
  formatter: (value: number | string) => string;
}) {
  const total = buckets.reduce((sum, bucket) => sum + bucket.count, 0);

  return (
    <article className="dashboard-panel">
      <h3>{title}</h3>
      <div className="distribution-list">
        {buckets.map((bucket) => {
          const width = total === 0 ? 0 : Math.max(8, Math.round((bucket.count / total) * 100));

          return (
            <div key={bucket.key} className="distribution-row">
              <span>{formatter(bucket.key)}</span>
              <strong>{bucket.count}</strong>
              <div className="distribution-bar" aria-hidden="true">
                <i style={{ width: `${width}%` }} />
              </div>
            </div>
          );
        })}
        {buckets.length === 0 ? <p className="empty-state">No distribution data yet.</p> : null}
      </div>
    </article>
  );
}

function TicketPulsePanel({
  title,
  tickets,
  emptyText,
  isBreachList = false
}: {
  title: string;
  tickets: DashboardTicket[];
  emptyText: string;
  isBreachList?: boolean;
}) {
  return (
    <article className="dashboard-panel dashboard-panel-wide">
      <h3>{title}</h3>
      <div className="pulse-list">
        {tickets.map((ticket) => (
          <article key={ticket.id} className={isBreachList ? "pulse-item pulse-item-danger" : "pulse-item"}>
            <div>
              <span className="ticket-number">{ticket.number}</span>
              <strong>{ticket.title}</strong>
              <small>{ticket.category} / {ticket.assignedTo ?? "Unassigned"}</small>
            </div>
            <div className="pulse-meta">
              <span>{formatStatus(ticket.status)}</span>
              <span>{formatPriority(ticket.priority)}</span>
              <small>Updated {formatDate(ticket.updatedAt)}</small>
            </div>
          </article>
        ))}
        {tickets.length === 0 ? <p className="empty-state">{emptyText}</p> : null}
      </div>
    </article>
  );
}
