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

export type UserProfileShort = {
  id: string;
  displayName: string;
  email: string;
};

export type TicketComment = {
  id: string;
  body: string;
  isInternal: boolean;
  author: UserProfileShort;
  createdAt: string;
};

export type TicketAttachment = {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  downloadUrl: string;
  uploadedBy: UserProfileShort;
  createdAt: string;
};

export type TimelineItem = {
  type: string;
  text: string;
  createdAt: string;
};

export type TicketDetail = Omit<TicketListItem, "createdBy" | "assignedTo"> & {
  description: string;
  categoryId: string;
  createdBy: UserProfileShort;
  assignedTo?: UserProfileShort | null;
  firstResponseAt?: string | null;
  resolvedAt?: string | null;
  comments: TicketComment[];
  attachments: TicketAttachment[];
  timeline: TimelineItem[];
};

export type TicketCategory = {
  id: string;
  name: string;
  description?: string | null;
  isActive: boolean;
};

export type TicketQuery = {
  status?: TicketStatus | "";
  priority?: TicketPriority | "";
  categoryId?: string;
  search?: string;
  mine?: boolean;
  unassigned?: boolean;
  overdue?: boolean;
};

export type CreateTicketInput = {
  title: string;
  description: string;
  categoryId: string;
  priority: number;
};
