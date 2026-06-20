# Changelog

All notable SupportPilot changes are tracked here.

## v0.8 - Compose CI Smoke

- Added Docker Compose frontend service backed by nginx.
- Added automated Docker smoke coverage for API readiness, login, ticket creation, assignment, comments, MinIO attachment upload/download/delete, status change and RabbitMQ notification persistence.
- Added frontend build validation to GitHub Actions.
- Added full-stack Docker topology and CI validation documentation.

## v0.7 - API Documentation And Frontend Density

- Enabled XML/OpenAPI route documentation.
- Converted working screens to product headers.
- Tightened frontend spacing, ticket list density and secondary text contrast.
- Added frontend button variants for primary, secondary, danger, ghost and small actions.

## v0.6 - Knowledge Base And Admin

- Added FAQ browse and staff management flows.
- Added admin UI for users, ticket categories and SLA policies.

## v0.5 - Ticket Workspace

- Added ticket filters, details, comments, attachments and workflow controls.
- Added live dashboard data and SignalR wiring.

## v0.4 - Frontend Shell

- Added React login, session persistence, protected layout and API readiness indicator.

## v0.3 - Notification Worker

- Added RabbitMQ notification transport.
- Added standalone notifications worker.

## v0.2 - Infrastructure Hardening

- Replaced `EnsureCreated` with EF Core migrations.
- Added PostgreSQL provider and Docker profile.
- Added Redis cache, MinIO storage and health checks.

## v0.1 - Backend MVP

- Added authentication, roles, tickets, comments, attachments, SLA policies, audit log and initial knowledge base flows.
