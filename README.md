# SupportPilot

SupportPilot is an MVP support ticketing system built with ASP.NET Core Web API.

## Current Scope

- Clean Architecture split into `Api`, `Application`, `Domain`, `Infrastructure` and `Contracts`.
- JWT authentication with `Admin`, `Agent` and `Customer` roles.
- Registration, login and current-user profile workflows implemented through application use cases.
- Tickets with categories, statuses, priorities and assignee management.
- SLA policies for `Critical`, `High`, `Normal` and `Low` priorities.
- Background SLA monitor that marks breaches and creates notifications.
- Dedicated `SupportPilot.Notifications.Worker` for SLA monitoring and RabbitMQ notification processing.
- Public comments and internal support notes.
- Attachments through `IFileStorage`, backed by local storage or MinIO.
- Ticket timeline assembled from creation, status changes, comments and attachments.
- Knowledge base / FAQ articles with category filtering and search.
- Reports, audit log and SignalR hub at `/hubs/tickets`.
- Health checks at `/api/health`, `/api/health/ready` and `/api/health/live`.
- EF Core migrations instead of `EnsureCreated`.
- SQLite for local development, PostgreSQL for Docker and production-like runs.
- Memory cache locally and Redis cache in the Docker profile.

## Quick Start

Local mode uses SQLite, local file storage and in-memory cache:

```powershell
dotnet run --project src/SupportPilot.Api
```

Swagger is available at:

```text
http://localhost:5295/swagger
```

The default administrator is created automatically during the first startup:

```text
email: admin@supportpilot.local
password: Admin123!
```

## Infrastructure Profile

The database provider is selected through configuration:

```json
{
  "Database": {
    "Provider": "Sqlite"
  }
}
```

or:

```json
{
  "Database": {
    "Provider": "PostgreSql"
  }
}
```

The file storage provider is selected through configuration:

```json
{
  "FileStorage": {
    "Provider": "Local"
  }
}
```

or:

```json
{
  "FileStorage": {
    "Provider": "Minio"
  }
}
```

The application cache provider is selected through configuration:

```json
{
  "Cache": {
    "Provider": "Memory",
    "KnowledgeBaseExpirationSeconds": 300,
    "ReportsExpirationSeconds": 30
  }
}
```

or:

```json
{
  "Cache": {
    "Provider": "Redis"
  }
}
```

Runtime profiles:

- SQLite + Local storage + Memory cache: local development through `dotnet run`.
- PostgreSQL + MinIO + Redis cache: Docker and production-like profile through `docker compose`.
- Public knowledge base responses and `/api/reports/overview` are cached; write operations invalidate the related cache group.

## XML And OpenAPI Documentation

The public API surface generates XML documentation during build and publish:

- `SupportPilot.Api`
- `SupportPilot.Application`
- `SupportPilot.Contracts`
- `SupportPilot.Domain`
- `SupportPilot.Notifications.Worker`

Swagger includes XML comments from API, Contracts, Domain and Application assemblies when the XML files are present in the application output directory. Route-level OpenAPI metadata is added through `WithRouteDocs(...)` for authentication, ticket, administration, knowledge base, report and notification endpoints.

Build output location:

```text
src/{ProjectName}/bin/{Configuration}/net8.0/{ProjectName}.xml
```

Publish output can be checked with:

```powershell
dotnet publish src/SupportPilot.Api/SupportPilot.Api.csproj -c Release -o artifacts/api
Get-ChildItem artifacts/api -Filter *.xml
```

## Frontend Shell

The React frontend lives in `frontend/`.

Local development:

```powershell
cd frontend
npm install
npm run dev
```

The dev server runs on `http://localhost:5173` and proxies `/api` plus `/hubs` to the backend. By default it targets `http://localhost:5295`.

Use the Docker API instead:

```powershell
$env:SUPPORTPILOT_API_TARGET="http://localhost:8080"
npm run dev
```

Current frontend scope includes login, JWT session persistence, protected layout, health indicator, realtime dashboard, ticket workflows, knowledge base workflows and admin management for users, roles, ticket categories and SLA policies.

## Docker Compose

Copy `.env.example` to `.env` if you need local overrides, then run:

```powershell
docker compose up --build
```

Compose switches the API to PostgreSQL, MinIO, RabbitMQ and Redis cache:

```text
Database__Provider=PostgreSql
FileStorage__Provider=Minio
Notifications__Transport=RabbitMQ
Cache__Provider=Redis
```

Services:

- API: `http://localhost:8080`
- Notifications worker: standalone container without an HTTP port.
- PostgreSQL: `localhost:5432`
- MinIO console: `http://localhost:9001`
- RabbitMQ management: `http://localhost:15672`
- Redis: `localhost:6379`

Health checks:

- `GET /api/health`: all checks.
- `GET /api/health/ready`: database, Redis, RabbitMQ and object storage readiness.
- `GET /api/health/live`: liveness without external dependencies.

## EF Core

Apply migrations manually:

```powershell
dotnet ef database update --project src/SupportPilot.Infrastructure --startup-project src/SupportPilot.Api
```

Create a new migration:

```powershell
dotnet ef migrations add MigrationName --project src/SupportPilot.Infrastructure --startup-project src/SupportPilot.Api --output-dir Data/Migrations
```

On startup, the API calls `Database.MigrateAsync()` and then seeds baseline roles, SLA policies, ticket categories, FAQ content and the default admin user.

## Architecture

```text
src/
  SupportPilot.Api             HTTP endpoints, Swagger, auth policies, SignalR adapter
  SupportPilot.Application     use cases and application ports
  SupportPilot.Contracts       request/response DTOs
  SupportPilot.Domain          entities, enums and core business concepts
  SupportPilot.Infrastructure  EF Core, PostgreSQL/SQLite, JWT, password hashing, file storage, notification adapters
  SupportPilot.Notifications.Worker  RabbitMQ consumer, notification persistence, SLA monitor host
tests/
  SupportPilot.IntegrationTests
```

Dependencies point inward:

```text
Api -> Application / Contracts / Infrastructure
Infrastructure -> Application / Domain
Application -> Domain / Contracts
Contracts -> Domain
```

Ticket workflows, support lookups and attachment download authorization live in `SupportPilot.Application/Tickets/TicketUseCases.cs`. Knowledge base workflows live in `SupportPilot.Application/KnowledgeBase/KnowledgeBaseUseCases.cs`. Support dashboard report queries live in `SupportPilot.Application/Reports/ReportUseCases.cs`. Notification inbox workflows live in `SupportPilot.Application/Notifications/NotificationUseCases.cs`. Audit-log reads are exposed through `SupportPilot.Application/Admin/AdminUseCases.cs`.

API endpoints stay as request/response adapters and map `ApplicationResult<T>` to HTTP responses. Business logic for authentication lives in `SupportPilot.Application/Auth/AuthUseCases.cs`; administration of users, ticket categories and SLA policies lives in `SupportPilot.Application/Admin/AdminUseCases.cs`.

Application defines ports:

- `IUserAccountStore`: account, role and audit-log access.
- `IPasswordHasher`: password hashing and verification.
- `ITokenService`: access token issuing.
- `IFileStorage`: attachment file operations.
- `INotificationPublisher`: notification publishing to the local database or RabbitMQ.
- `IApplicationCache`: read-heavy application response caching through memory cache or Redis.

Infrastructure contains port implementations:

- `UserAccountStore`: EF Core access to users and roles.
- `AspNetPasswordHasher`: hashing through ASP.NET Core Identity.
- `JwtTokenService`: JWT generation.
- `LocalFileStorage` and `MinioFileStorage`: file storage providers.
- `DatabaseNotificationPublisher`: local notification mode without RabbitMQ.
- `RabbitMqNotificationPublisher`: notification event publishing to RabbitMQ.
- `RabbitMqNotificationWorker`: queue consumer that stores notifications in the database.
- `DistributedApplicationCache`: cache abstraction over `IDistributedCache`, memory-backed locally and Redis-backed in Docker.

In local mode, the API stores notifications directly through `Notifications:Transport=Database`. In Docker and production-like mode, the API publishes events to RabbitMQ through `Notifications:Transport=RabbitMQ`, and `SupportPilot.Notifications.Worker` stores them in the `Notifications` table.

Storage-optimized notification inbox and audit-log queries are exposed to Application through `INotificationInboxStore` and `IAuditLogReader`, with EF Core implementations in Infrastructure.

## Main Endpoint Groups

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/auth/me`
- `GET /api/tickets/categories`
- `GET /api/tickets/assignees`
- `GET /api/tickets`
- `POST /api/tickets`
- `GET /api/tickets/{id}`
- `PATCH /api/tickets/{id}/status`
- `PATCH /api/tickets/{id}/assignee`
- `POST /api/tickets/{id}/comments`
- `POST /api/tickets/{id}/attachments`
- `GET /api/tickets/{ticketId}/attachments/{attachmentId}`
- `DELETE /api/tickets/{ticketId}/attachments/{attachmentId}`
- `GET /api/kb/categories`
- `GET /api/kb/articles?search=...`
- `GET /api/kb/articles/{slug}`
- `GET /api/kb/admin/categories`
- `POST /api/kb/admin/categories`
- `PUT /api/kb/admin/categories/{id}`
- `GET /api/kb/admin/articles`
- `GET /api/kb/admin/articles/{id}`
- `POST /api/kb/admin/articles`
- `PUT /api/kb/admin/articles/{id}`
- `GET /api/reports/overview`
- `GET /api/admin/users`
- `PUT /api/admin/users/{id}`
- `GET /api/admin/roles`
- `GET /api/admin/categories`
- `POST /api/admin/categories`
- `PUT /api/admin/categories/{id}`
- `GET /api/admin/sla-policies`
- `PUT /api/admin/sla-policies/{id}`
- `GET /api/admin/audit`
- `GET /api/notifications`
- `POST /api/notifications/{id}/read`

## Validation

```powershell
dotnet build SupportPilot.sln
dotnet test SupportPilot.sln
npm --prefix frontend run build
dotnet publish src/SupportPilot.Api/SupportPilot.Api.csproj -c Release -o artifacts/api
dotnet publish src/SupportPilot.Notifications.Worker/SupportPilot.Notifications.Worker.csproj -c Release -o artifacts/notifications-worker
```

## Next Technical Steps

- Add Docker Compose smoke automation for health, login, ticket creation and attachment upload/download.
- Move remaining high-volume dashboard and ticket read paths behind query-specific Application ports if database load grows.
- Add release notes for the next backend hardening milestone.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).
