import type { AuthResponse, HealthStatus, TicketListItem, UserProfile } from "./types";

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

export async function getTickets(token: string): Promise<TicketListItem[]> {
  return request<TicketListItem[]>("/tickets", { token });
}

type RequestOptions = RequestInit & {
  token?: string | null;
};

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set("Accept", "application/json");

  if (options.body && !headers.has("Content-Type")) {
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

async function readErrorMessage(response: Response): Promise<string | null> {
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("application/json")) {
    return response.statusText;
  }

  const payload = (await response.json().catch(() => null)) as { message?: string } | null;
  return payload?.message ?? null;
}
