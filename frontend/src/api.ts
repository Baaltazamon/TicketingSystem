import type {
  AuthResponse,
  CreateTicketInput,
  DashboardOverview,
  HealthStatus,
  KnowledgeBaseArticle,
  KnowledgeBaseArticleListItem,
  KnowledgeBaseArticleQuery,
  KnowledgeBaseCategory,
  TicketCategory,
  TicketDetail,
  TicketListItem,
  TicketQuery,
  UpsertKnowledgeBaseArticleInput,
  UpsertKnowledgeBaseCategoryInput,
  UserProfile
} from "./types";

const API_ROOT = "/api";
const TOKEN_STORAGE_KEY = "supportpilot.token";

export function getStoredToken(): string | null {
  return window.localStorage.getItem(TOKEN_STORAGE_KEY);
}

export function storeToken(token: string): void {
  window.localStorage.setItem(TOKEN_STORAGE_KEY, token);
}

export function clearToken(): void {
  window.localStorage.removeItem(TOKEN_STORAGE_KEY);
}

export async function login(email: string, password: string): Promise<AuthResponse> {
  return request<AuthResponse>("/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password })
  });
}

export async function getCurrentUser(token: string): Promise<UserProfile> {
  return request<UserProfile>("/auth/me", { token });
}

export async function getReadiness(): Promise<HealthStatus> {
  const response = await fetch(`${API_ROOT}/health/ready`);
  if (!response.ok) {
    return "Unhealthy";
  }

  return (await response.text()) as HealthStatus;
}

export async function getTickets(token: string, query: TicketQuery = {}): Promise<TicketListItem[]> {
  return request<TicketListItem[]>(`/tickets${toQueryString(query)}`, { token });
}

export async function getTicket(token: string, ticketId: string): Promise<TicketDetail> {
  return request<TicketDetail>(`/tickets/${ticketId}`, { token });
}

export async function createTicket(token: string, input: CreateTicketInput): Promise<{ id: string; number: string }> {
  return request<{ id: string; number: string }>("/tickets", {
    method: "POST",
    token,
    body: JSON.stringify(input)
  });
}

export async function updateTicketStatus(
  token: string,
  ticketId: string,
  status: number,
  reason: string | null
): Promise<void> {
  await request<void>(`/tickets/${ticketId}/status`, {
    method: "PATCH",
    token,
    body: JSON.stringify({ status, reason })
  });
}

export async function assignTicket(token: string, ticketId: string, assignedToId: string | null): Promise<void> {
  await request<void>(`/tickets/${ticketId}/assignee`, {
    method: "PATCH",
    token,
    body: JSON.stringify({ assignedToId })
  });
}

export async function addTicketComment(
  token: string,
  ticketId: string,
  body: string,
  isInternal: boolean
): Promise<void> {
  await request<void>(`/tickets/${ticketId}/comments`, {
    method: "POST",
    token,
    body: JSON.stringify({ body, isInternal })
  });
}

export async function uploadTicketAttachment(token: string, ticketId: string, file: File): Promise<{ id: string }> {
  const form = new FormData();
  form.append("file", file);

  return request<{ id: string }>(`/tickets/${ticketId}/attachments`, {
    method: "POST",
    token,
    body: form
  });
}

export async function deleteTicketAttachment(token: string, ticketId: string, attachmentId: string): Promise<void> {
  await request<void>(`/tickets/${ticketId}/attachments/${attachmentId}`, {
    method: "DELETE",
    token
  });
}

export async function getTicketCategories(token: string): Promise<TicketCategory[]> {
  return request<TicketCategory[]>("/tickets/categories", { token });
}

export async function getUsers(token: string): Promise<UserProfile[]> {
  return request<UserProfile[]>("/admin/users", { token });
}

export async function getDashboardOverview(token: string): Promise<DashboardOverview> {
  return request<DashboardOverview>("/reports/overview", { token });
}

export async function getKnowledgeBaseCategories(): Promise<KnowledgeBaseCategory[]> {
  return request<KnowledgeBaseCategory[]>("/kb/categories");
}

export async function getKnowledgeBaseArticles(
  query: KnowledgeBaseArticleQuery = {}
): Promise<KnowledgeBaseArticleListItem[]> {
  return request<KnowledgeBaseArticleListItem[]>(`/kb/articles${toQueryString(query)}`);
}

export async function getKnowledgeBaseArticle(slug: string): Promise<KnowledgeBaseArticle> {
  return request<KnowledgeBaseArticle>(`/kb/articles/${encodeURIComponent(slug)}`);
}

export async function getAdminKnowledgeBaseCategories(token: string): Promise<KnowledgeBaseCategory[]> {
  return request<KnowledgeBaseCategory[]>("/kb/admin/categories", { token });
}

export async function createAdminKnowledgeBaseCategory(
  token: string,
  input: UpsertKnowledgeBaseCategoryInput
): Promise<KnowledgeBaseCategory> {
  return request<KnowledgeBaseCategory>("/kb/admin/categories", {
    method: "POST",
    token,
    body: JSON.stringify(input)
  });
}

export async function updateAdminKnowledgeBaseCategory(
  token: string,
  categoryId: string,
  input: UpsertKnowledgeBaseCategoryInput
): Promise<KnowledgeBaseCategory> {
  return request<KnowledgeBaseCategory>(`/kb/admin/categories/${categoryId}`, {
    method: "PUT",
    token,
    body: JSON.stringify(input)
  });
}

export async function getAdminKnowledgeBaseArticles(
  token: string,
  query: KnowledgeBaseArticleQuery = {}
): Promise<KnowledgeBaseArticleListItem[]> {
  return request<KnowledgeBaseArticleListItem[]>(`/kb/admin/articles${toQueryString(query)}`, { token });
}

export async function getAdminKnowledgeBaseArticle(token: string, articleId: string): Promise<KnowledgeBaseArticle> {
  return request<KnowledgeBaseArticle>(`/kb/admin/articles/${articleId}`, { token });
}

export async function createAdminKnowledgeBaseArticle(
  token: string,
  input: UpsertKnowledgeBaseArticleInput
): Promise<KnowledgeBaseArticle> {
  return request<KnowledgeBaseArticle>("/kb/admin/articles", {
    method: "POST",
    token,
    body: JSON.stringify(input)
  });
}

export async function updateAdminKnowledgeBaseArticle(
  token: string,
  articleId: string,
  input: UpsertKnowledgeBaseArticleInput
): Promise<KnowledgeBaseArticle> {
  return request<KnowledgeBaseArticle>(`/kb/admin/articles/${articleId}`, {
    method: "PUT",
    token,
    body: JSON.stringify(input)
  });
}

type RequestOptions = RequestInit & {
  token?: string | null;
};

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set("Accept", "application/json");

  if (options.body && !(options.body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  if (options.token) {
    headers.set("Authorization", `Bearer ${options.token}`);
  }

  const response = await fetch(`${API_ROOT}${path}`, {
    ...options,
    headers
  });

  if (!response.ok) {
    const message = await readErrorMessage(response);
    throw new Error(message || `Request failed with HTTP ${response.status}.`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

function toQueryString(query: TicketQuery): string {
  const params = new URLSearchParams();

  Object.entries(query).forEach(([key, value]) => {
    if (value === undefined || value === null || value === "") {
      return;
    }

    params.set(key, String(value));
  });

  const serialized = params.toString();
  return serialized ? `?${serialized}` : "";
}

async function readErrorMessage(response: Response): Promise<string | null> {
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("application/json")) {
    return response.statusText;
  }

  const payload = (await response.json().catch(() => null)) as { message?: string } | null;
  return payload?.message ?? null;
}
