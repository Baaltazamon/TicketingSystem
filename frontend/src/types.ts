export type UserProfile = {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
};

export type AuthResponse = {
  token: string;
  user: UserProfile;
};

export type HealthStatus = "Healthy" | "Degraded" | "Unhealthy";

export type TicketStatus = 0 | 1 | 2 | 3 | 4 | 5 | string;

export type TicketPriority = 0 | 1 | 2 | 3 | string;

export type TicketListItem = {
  id: string;
  number: string;
  title: string;
  status: TicketStatus;
  priority: TicketPriority;
  category: string;
  createdBy: string;
  assignedTo?: string | null;
  createdAt: string;
  updatedAt: string;
  firstResponseDueAt?: string | null;
  resolutionDueAt?: string | null;
  firstResponseBreached: boolean;
  resolutionBreached: boolean;
};
