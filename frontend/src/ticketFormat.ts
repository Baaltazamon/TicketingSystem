import type { TicketListItem, TicketPriority, TicketStatus } from "./types";

export const ticketStatuses: Array<{ value: number; label: string }> = [
  { value: 0, label: "New" },
  { value: 1, label: "In Progress" },
  { value: 2, label: "Waiting for Customer" },
  { value: 3, label: "Resolved" },
  { value: 4, label: "Closed" },
  { value: 5, label: "Cancelled" }
];

export const ticketPriorities: Array<{ value: number; label: string }> = [
  { value: 0, label: "Low" },
  { value: 1, label: "Normal" },
  { value: 2, label: "High" },
  { value: 3, label: "Critical" }
];

export function isOpenStatus(status: TicketListItem["status"]): boolean {
  return ![3, 4, 5, "Resolved", "Closed", "Cancelled"].includes(status);
}

export function formatStatus(status: TicketStatus): string {
  return formatEnum(status, ticketStatuses);
}

export function formatPriority(priority: TicketPriority): string {
  return formatEnum(priority, ticketPriorities);
}

export function formatDate(value?: string | null): string {
  if (!value) {
    return "Not set";
  }

  return new Intl.DateTimeFormat(undefined, {
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(value));
}

export function formatBytes(value: number): string {
  if (value < 1024) {
    return `${value} B`;
  }

  if (value < 1024 * 1024) {
    return `${(value / 1024).toFixed(1)} KB`;
  }

  return `${(value / (1024 * 1024)).toFixed(1)} MB`;
}

function formatEnum(value: number | string, labels: Array<{ value: number; label: string }>): string {
  if (typeof value === "number") {
    return labels.find((item) => item.value === value)?.label ?? String(value);
  }

  return value.replace(/([a-z])([A-Z])/g, "$1 $2");
}
