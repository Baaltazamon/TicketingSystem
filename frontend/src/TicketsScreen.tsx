import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from "react";
import {
  addTicketComment,
  assignTicket,
  createTicket,
  deleteTicketAttachment,
  getTicketAssignees,
  getTicket,
  getTicketCategories,
  getTickets,
  updateTicketStatus,
  uploadTicketAttachment
} from "./api";
import {
  formatBytes,
  formatDate,
  formatPriority,
  formatStatus,
  ticketPriorities,
  ticketStatuses
} from "./ticketFormat";
import type {
  CreateTicketInput,
  TicketCategory,
  TicketDetail,
  TicketListItem,
  TicketQuery,
  UserProfile
} from "./types";

export function TicketsScreen({ token, user }: { token: string; user: UserProfile }) {
  const [tickets, setTickets] = useState<TicketListItem[]>([]);
  const [selectedTicketId, setSelectedTicketId] = useState<string | null>(null);
  const [selectedTicket, setSelectedTicket] = useState<TicketDetail | null>(null);
  const [categories, setCategories] = useState<TicketCategory[]>([]);
  const [users, setUsers] = useState<UserProfile[]>([]);
  const [query, setQuery] = useState<TicketQuery>({});
  const [isLoadingList, setIsLoadingList] = useState(true);
  const [isLoadingDetails, setIsLoadingDetails] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canUseSupportActions = user.roles.some((role) => role === "Admin" || role === "Agent");

  async function loadTickets(nextQuery = query) {
    setIsLoadingList(true);
    setError(null);

    try {
      const items = await getTickets(token, nextQuery);
      setTickets(items);
      setSelectedTicketId((current) => current ?? items[0]?.id ?? null);
    } catch (err) {
      setError(readError(err, "Failed to load tickets."));
    } finally {
      setIsLoadingList(false);
    }
  }

  async function loadTicketDetails(ticketId: string) {
    setIsLoadingDetails(true);
    setError(null);

    try {
      setSelectedTicket(await getTicket(token, ticketId));
    } catch (err) {
      setError(readError(err, "Failed to load ticket details."));
    } finally {
      setIsLoadingDetails(false);
    }
  }

  useEffect(() => {
    loadTickets();
  }, []);

  useEffect(() => {
    if (!selectedTicketId) {
      setSelectedTicket(null);
      return;
    }

    loadTicketDetails(selectedTicketId);
  }, [selectedTicketId]);

  useEffect(() => {
    let isActive = true;

    async function loadLookups() {
      const nextCategories = await getTicketCategories(token).catch(() => []);
      const nextUsers = canUseSupportActions ? await getTicketAssignees(token).catch(() => []) : [];

      if (isActive) {
        setCategories(nextCategories.filter((category) => category.isActive));
        setUsers(nextUsers);
      }
    }

    loadLookups();

    return () => {
      isActive = false;
    };
  }, [canUseSupportActions, token]);

  async function refreshCurrentTicket() {
    await loadTickets(query);

    if (selectedTicketId) {
      await loadTicketDetails(selectedTicketId);
    }
  }

  return (
    <section className="tickets-shell">
      <header className="product-header">
        <div>
          <span className="eyebrow">Operations queue</span>
          <h2>Manage customer requests, SLA and ticket workflow.</h2>
        </div>
        <p>Compact case work for support agents: filter the queue, inspect the selected ticket and move it forward.</p>
      </header>

      <section className="tickets-workspace">
        <div className="tickets-left">
          <section className="tickets-list-shell">
            <header className="tickets-list-header">
              <div>
                <span className="eyebrow">Ticket queue</span>
                <h2>Case work</h2>
              </div>
              <CreateTicketPanel
                categories={categories}
                token={token}
                canCreate={categories.length > 0}
                onCreated={(ticketId) => {
                  setSelectedTicketId(ticketId);
                  loadTickets(query);
                }}
              />
            </header>
            <TicketsToolbar
              categories={categories}
              query={query}
              canUseSupportFilters={canUseSupportActions}
              onChange={(nextQuery) => setQuery(nextQuery)}
              onApply={(nextQuery) => loadTickets(nextQuery)}
            />
            <TicketList
              isLoading={isLoadingList}
              selectedTicketId={selectedTicketId}
              tickets={tickets}
              onSelect={setSelectedTicketId}
            />
          </section>
        </div>

        <div className="tickets-right">
          {error ? <div className="notice notice-error">{error}</div> : null}
          <TicketDetailsPanel
            canUseSupportActions={canUseSupportActions}
            isLoading={isLoadingDetails}
            ticket={selectedTicket}
            token={token}
            users={users}
            onChanged={refreshCurrentTicket}
          />
        </div>
      </section>
    </section>
  );
}

function TicketsToolbar({
  categories,
  query,
  canUseSupportFilters,
  onChange,
  onApply
}: {
  categories: TicketCategory[];
  query: TicketQuery;
  canUseSupportFilters: boolean;
  onChange: (query: TicketQuery) => void;
  onApply: (query: TicketQuery) => void;
}) {
  function patchQuery(patch: Partial<TicketQuery>) {
    const nextQuery = { ...query, ...patch };
    onChange(nextQuery);
    return nextQuery;
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onApply(query);
  }

  return (
    <form className="tickets-toolbar" onSubmit={submit}>
      <label>
        Search
        <input
          placeholder="Number, title or description"
          value={query.search ?? ""}
          onChange={(event) => patchQuery({ search: event.target.value })}
        />
      </label>
      <label>
        Status
        <select
          value={query.status ?? ""}
          onChange={(event) => patchQuery({ status: parseOptionalNumber(event.target.value) })}
        >
          <option value="">Any</option>
          {ticketStatuses.map((status) => (
            <option key={status.value} value={status.value}>
              {status.label}
            </option>
          ))}
        </select>
      </label>
      <label>
        Priority
        <select
          value={query.priority ?? ""}
          onChange={(event) => patchQuery({ priority: parseOptionalNumber(event.target.value) })}
        >
          <option value="">Any</option>
          {ticketPriorities.map((priority) => (
            <option key={priority.value} value={priority.value}>
              {priority.label}
            </option>
          ))}
        </select>
      </label>
      <label>
        Category
        <select
          value={query.categoryId ?? ""}
          onChange={(event) => patchQuery({ categoryId: event.target.value })}
        >
          <option value="">Any</option>
          {categories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.name}
            </option>
          ))}
        </select>
      </label>
      {canUseSupportFilters ? (
        <div className="filter-flags">
          <label>
            <input
              checked={query.mine === true}
              type="checkbox"
              onChange={(event) => patchQuery({ mine: event.target.checked || undefined })}
            />
            Mine
          </label>
          <label>
            <input
              checked={query.unassigned === true}
              type="checkbox"
              onChange={(event) => patchQuery({ unassigned: event.target.checked || undefined })}
            />
            Unassigned
          </label>
          <label>
            <input
              checked={query.overdue === true}
              type="checkbox"
              onChange={(event) => patchQuery({ overdue: event.target.checked || undefined })}
            />
            Overdue
          </label>
        </div>
      ) : null}
      <button className="button-secondary button-small" type="submit">Apply filters</button>
    </form>
  );
}

function CreateTicketPanel({
  categories,
  token,
  canCreate,
  onCreated
}: {
  categories: TicketCategory[];
  token: string;
  canCreate: boolean;
  onCreated: (ticketId: string) => void;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [input, setInput] = useState<CreateTicketInput>({
    title: "",
    description: "",
    categoryId: "",
    priority: 1
  });

  useEffect(() => {
    if (!input.categoryId && categories[0]) {
      setInput((current) => ({ ...current, categoryId: categories[0].id }));
    }
  }, [categories, input.categoryId]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);

    try {
      const created = await createTicket(token, input);
      setInput({ title: "", description: "", categoryId: categories[0]?.id ?? "", priority: 1 });
      setIsOpen(false);
      onCreated(created.id);
    } catch (err) {
      setError(readError(err, "Failed to create ticket."));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="create-ticket-panel">
      <button className="button-primary button-small" type="button" onClick={() => setIsOpen((value) => !value)}>
        {isOpen ? "Close" : "+ Create ticket"}
      </button>
      {!canCreate ? (
        <p className="helper-text">Active categories are required to create a ticket.</p>
      ) : null}
      {isOpen ? (
        <form className="ticket-form" onSubmit={submit}>
          <label>
            Title
            <input
              value={input.title}
              onChange={(event) => setInput((current) => ({ ...current, title: event.target.value }))}
              required
            />
          </label>
          <label>
            Description
            <textarea
              value={input.description}
              onChange={(event) => setInput((current) => ({ ...current, description: event.target.value }))}
              required
            />
          </label>
          <label>
            Category
            <select
              value={input.categoryId}
              onChange={(event) => setInput((current) => ({ ...current, categoryId: event.target.value }))}
              required
            >
              {categories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Priority
            <select
              value={input.priority}
              onChange={(event) => setInput((current) => ({ ...current, priority: Number(event.target.value) }))}
            >
              {ticketPriorities.map((priority) => (
                <option key={priority.value} value={priority.value}>
                  {priority.label}
                </option>
              ))}
            </select>
          </label>
          {error ? <p className="form-error">{error}</p> : null}
          <button className="button-primary" type="submit" disabled={!canCreate || isSubmitting}>
            {isSubmitting ? "Creating..." : "Submit ticket"}
          </button>
        </form>
      ) : null}
    </section>
  );
}

function TicketList({
  isLoading,
  selectedTicketId,
  tickets,
  onSelect
}: {
  isLoading: boolean;
  selectedTicketId: string | null;
  tickets: TicketListItem[];
  onSelect: (ticketId: string) => void;
}) {
  if (isLoading) {
    return <div className="empty-state">Loading ticket queue...</div>;
  }

  if (tickets.length === 0) {
    return <div className="empty-state">No tickets match current filters.</div>;
  }

  return (
    <div className="ticket-queue">
      {tickets.map((ticket) => (
        <button
          key={ticket.id}
          className={ticket.id === selectedTicketId ? "queue-item queue-item-active" : "queue-item"}
          onClick={() => onSelect(ticket.id)}
          type="button"
        >
          <span className="queue-item-topline">
            <span className="ticket-number">{ticket.number}</span>
            <span className="queue-meta">
              <Badge tone="status">{formatStatus(ticket.status)}</Badge>
              <Badge tone={isHighPriority(ticket.priority) ? "danger" : "neutral"}>{formatPriority(ticket.priority)}</Badge>
            </span>
          </span>
          <strong>{ticket.title}</strong>
          <small>
            {ticket.category} · created {formatQueueDate(ticket.createdAt)} · due {formatQueueDate(ticket.resolutionDueAt)}
          </small>
        </button>
      ))}
    </div>
  );
}

function TicketDetailsPanel({
  canUseSupportActions,
  isLoading,
  ticket,
  token,
  users,
  onChanged
}: {
  canUseSupportActions: boolean;
  isLoading: boolean;
  ticket: TicketDetail | null;
  token: string;
  users: UserProfile[];
  onChanged: () => Promise<void>;
}) {
  if (isLoading) {
    return <div className="empty-state">Loading ticket details...</div>;
  }

  if (!ticket) {
    return <div className="empty-state">Select a ticket to inspect workflow, comments and attachments.</div>;
  }

  return (
    <article className="ticket-detail-card">
      <header className="detail-header">
        <div>
          <span className="ticket-number">{ticket.number}</span>
          <h2>{ticket.title}</h2>
          <p>{ticket.description}</p>
        </div>
        <div className="detail-badges">
          <Badge tone="status">{formatStatus(ticket.status)}</Badge>
          <Badge tone={ticket.priority === 3 || ticket.priority === "Critical" ? "danger" : "neutral"}>
            {formatPriority(ticket.priority)}
          </Badge>
        </div>
      </header>

      <section className="detail-grid">
        <Fact label="Category" value={ticket.category} />
        <Fact label="Created by" value={ticket.createdBy.displayName} />
        <Fact label="Assigned to" value={ticket.assignedTo?.displayName ?? "Unassigned"} />
        <Fact label="Created" value={formatDate(ticket.createdAt)} />
        <Fact label="First response due" value={formatDate(ticket.firstResponseDueAt)} />
        <Fact label="Resolution due" value={formatDate(ticket.resolutionDueAt)} />
      </section>

      {canUseSupportActions ? (
        <WorkflowPanel ticket={ticket} token={token} users={users} onChanged={onChanged} />
      ) : null}

      <CommentPanel canUseInternalNotes={canUseSupportActions} ticket={ticket} token={token} onChanged={onChanged} />
      <AttachmentPanel ticket={ticket} token={token} onChanged={onChanged} />
      <TimelinePanel ticket={ticket} />
    </article>
  );
}

function WorkflowPanel({
  ticket,
  token,
  users,
  onChanged
}: {
  ticket: TicketDetail;
  token: string;
  users: UserProfile[];
  onChanged: () => Promise<void>;
}) {
  const [status, setStatus] = useState(() => normalizeEnumValue(ticket.status));
  const [reason, setReason] = useState("");
  const [assignedToId, setAssignedToId] = useState(ticket.assignedTo?.id ?? "");
  const [isSaving, setIsSaving] = useState(false);
  const supportUsers = useMemo(
    () => users.filter((item) => item.roles.some((role) => role === "Admin" || role === "Agent")),
    [users]
  );

  useEffect(() => {
    setStatus(normalizeEnumValue(ticket.status));
    setAssignedToId(ticket.assignedTo?.id ?? "");
  }, [ticket.assignedTo?.id, ticket.status]);

  async function saveStatus() {
    setIsSaving(true);
    try {
      await updateTicketStatus(token, ticket.id, status, reason || null);
      setReason("");
      await onChanged();
    } finally {
      setIsSaving(false);
    }
  }

  async function saveAssignee() {
    setIsSaving(true);
    try {
      await assignTicket(token, ticket.id, assignedToId || null);
      await onChanged();
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="workflow-panel">
      <h3>Workflow controls</h3>
      <div className="workflow-actions">
        <div className="workflow-action-card">
          <span>Status</span>
          <label>
            Next state
            <select value={status} onChange={(event) => setStatus(Number(event.target.value))}>
              {ticketStatuses.map((item) => (
                <option key={item.value} value={item.value}>
                  {item.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Reason
            <input value={reason} onChange={(event) => setReason(event.target.value)} placeholder="Optional timeline note" />
          </label>
          <button className="button-secondary button-small" type="button" disabled={isSaving} onClick={saveStatus}>
            Update status
          </button>
        </div>
        <div className="workflow-action-card">
          <span>Assignee</span>
          <label>
            Owner
            <select value={assignedToId} onChange={(event) => setAssignedToId(event.target.value)}>
              <option value="">Unassigned</option>
              {supportUsers.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.displayName}
                </option>
              ))}
            </select>
          </label>
          <button className="button-secondary button-small" type="button" disabled={isSaving || supportUsers.length === 0} onClick={saveAssignee}>
            Save assignee
          </button>
        </div>
      </div>
    </section>
  );
}

function CommentPanel({
  canUseInternalNotes,
  ticket,
  token,
  onChanged
}: {
  canUseInternalNotes: boolean;
  ticket: TicketDetail;
  token: string;
  onChanged: () => Promise<void>;
}) {
  const [body, setBody] = useState("");
  const [isInternal, setIsInternal] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    try {
      await addTicketComment(token, ticket.id, body, canUseInternalNotes && isInternal);
      setBody("");
      setIsInternal(false);
      await onChanged();
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="detail-section">
      <h3>Comments</h3>
      <form className="comment-form" onSubmit={submit}>
        <textarea value={body} onChange={(event) => setBody(event.target.value)} required />
        <div className="comment-actions">
          {canUseInternalNotes ? (
            <label>
              <input
                checked={isInternal}
                type="checkbox"
                onChange={(event) => setIsInternal(event.target.checked)}
              />
              Internal note
            </label>
          ) : null}
          <button className="button-secondary button-small" type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Posting..." : "Add comment"}
          </button>
        </div>
      </form>
      <div className="comment-list">
        {ticket.comments.map((comment) => (
          <article key={comment.id} className={comment.isInternal ? "comment-card comment-internal" : "comment-card"}>
            <div>
              <strong>{comment.author.displayName}</strong>
              <small>{formatDate(comment.createdAt)}</small>
            </div>
            {comment.isInternal ? <Badge tone="danger">Internal</Badge> : null}
            <p>{comment.body}</p>
          </article>
        ))}
        {ticket.comments.length === 0 ? <p className="empty-state">No comments yet.</p> : null}
      </div>
    </section>
  );
}

function AttachmentPanel({
  ticket,
  token,
  onChanged
}: {
  ticket: TicketDetail;
  token: string;
  onChanged: () => Promise<void>;
}) {
  const [isUploading, setIsUploading] = useState(false);

  async function upload(file: File | null | undefined) {
    if (!file) {
      return;
    }

    setIsUploading(true);
    try {
      await uploadTicketAttachment(token, ticket.id, file);
      await onChanged();
    } finally {
      setIsUploading(false);
    }
  }

  async function remove(attachmentId: string) {
    await deleteTicketAttachment(token, ticket.id, attachmentId);
    await onChanged();
  }

  return (
    <section className="detail-section">
      <h3>Attachments</h3>
      <label className="file-drop">
        <input
          type="file"
          onChange={(event) => {
            upload(event.target.files?.[0]);
            event.target.value = "";
          }}
        />
        {isUploading ? "Uploading..." : "Drop in a file via picker"}
      </label>
      <div className="attachment-list">
        {ticket.attachments.map((attachment) => (
          <article key={attachment.id} className="attachment-card">
            <div>
              <strong>{attachment.fileName}</strong>
              <small>{formatBytes(attachment.sizeBytes)} / {attachment.uploadedBy.displayName}</small>
            </div>
            <div className="attachment-actions">
              <button className="button-ghost button-small" type="button" onClick={() => window.open(attachment.downloadUrl, "_blank", "noopener")}>
                Download
              </button>
              <button className="button-danger button-small" type="button" onClick={() => remove(attachment.id)}>
                Delete
              </button>
            </div>
          </article>
        ))}
        {ticket.attachments.length === 0 ? <p className="empty-state">No files attached.</p> : null}
      </div>
    </section>
  );
}

function TimelinePanel({ ticket }: { ticket: TicketDetail }) {
  return (
    <section className="detail-section">
      <h3>Timeline</h3>
      <div className="timeline">
        {ticket.timeline.map((item) => (
          <article key={`${item.type}-${item.createdAt}-${item.text}`} className="timeline-item">
            <span>{item.type}</span>
            <strong>{item.text}</strong>
            <small>{formatDate(item.createdAt)}</small>
          </article>
        ))}
      </div>
    </section>
  );
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div className="fact">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function Badge({ children, tone }: { children: ReactNode; tone: "danger" | "neutral" | "status" }) {
  return <span className={`badge badge-${tone}`}>{children}</span>;
}

function parseOptionalNumber(value: string): number | "" {
  return value === "" ? "" : Number(value);
}

function normalizeEnumValue(value: number | string): number {
  if (typeof value === "number") {
    return value;
  }

  return ticketStatuses.find((item) => item.label.replace(/\s/g, "") === value)?.value ?? 0;
}

function isHighPriority(priority: number | string): boolean {
  return priority === 2 || priority === 3 || priority === "High" || priority === "Critical";
}

function formatQueueDate(value?: string | null): string {
  if (!value) {
    return "not set";
  }

  return new Intl.DateTimeFormat(undefined, { day: "2-digit", month: "short" }).format(new Date(value));
}

function readError(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}
