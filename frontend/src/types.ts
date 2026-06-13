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

export type TicketStatus = number | string;

export type TicketPriority = number | string;

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

export type DashboardBucket = {
  key: string;
  count: number;
};

export type DashboardTicket = {
  id: string;
  number: string;
  title: string;
  status: TicketStatus;
  priority: TicketPriority;
  category: string;
  assignedTo?: string | null;
  updatedAt: string;
  firstResponseDueAt?: string | null;
  resolutionDueAt?: string | null;
  firstResponseBreached: boolean;
  resolutionBreached: boolean;
};

export type DashboardOverview = {
  generatedAt: string;
  totalTickets: number;
  openTickets: number;
  resolvedTickets: number;
  unassignedTickets: number;
  overdueTickets: number;
  dueSoonTickets: number;
  slaBreachedTickets: number;
  criticalTickets: number;
  highPriorityTickets: number;
  byStatus: DashboardBucket[];
  byPriority: DashboardBucket[];
  recentTickets: DashboardTicket[];
  slaBreaches: DashboardTicket[];
};

export type KnowledgeBaseCategory = {
  id: string;
  name: string;
  description?: string | null;
  articleCount: number;
};

export type KnowledgeBaseArticleListItem = {
  id: string;
  title: string;
  slug: string;
  categoryId: string;
  category: string;
  isPublished: boolean;
  updatedAt: string;
};

export type KnowledgeBaseArticle = KnowledgeBaseArticleListItem & {
  body: string;
  createdAt: string;
};

export type KnowledgeBaseArticleQuery = {
  search?: string;
  categoryId?: string;
  published?: boolean | "";
};

export type UpsertKnowledgeBaseCategoryInput = {
  name: string;
  description?: string | null;
};

export type UpsertKnowledgeBaseArticleInput = {
  categoryId: string;
  title: string;
  slug: string;
  body: string;
  isPublished: boolean;
};
