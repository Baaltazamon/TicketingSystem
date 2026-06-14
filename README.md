# SupportPilot

SupportPilot - MVP системы обращений в поддержку на ASP.NET Core Web API.

## Что уже есть

- Clean Architecture: `Api`, `Application`, `Domain`, `Infrastructure`, `Contracts`.
- JWT-аутентификация и роли `Admin`, `Agent`, `Customer`.
- Регистрация, вход и получение текущего пользователя через application use cases.
- Обращения, категории, статусы, приоритеты и назначение ответственных.
- SLA-политики для `Critical`, `High`, `Normal`, `Low`.
- Фоновый SLA-monitor, который помечает нарушения и создает уведомления.
- Отдельный `SupportPilot.Notifications.Worker` для SLA monitor и обработки уведомлений через RabbitMQ.
- Публичные комментарии и внутренние заметки.
- Вложения через `IFileStorage`: локальное хранилище или MinIO.
- Timeline обращения: создание, смена статуса, комментарии, файлы.
- База знаний / FAQ с поиском по статьям.
- Отчеты, аудит и SignalR hub `/hubs/tickets`.
- Health checks: `/api/health`, `/api/health/ready`, `/api/health/live`.
- EF Core migrations вместо `EnsureCreated`.
- SQLite для локального режима, PostgreSQL для Docker/production-like режима.

## Быстрый запуск

Локальный режим использует SQLite и локальное файловое хранилище:

```powershell
dotnet run --project src/SupportPilot.Api
```

Swagger будет доступен по адресу:

```text
http://localhost:5295/swagger
```

Администратор создается автоматически при первом запуске:

```text
email: admin@supportpilot.local
password: Admin123!
```

## Infrastructure Profile

Провайдер базы данных выбирается через конфиг:

```json
{
  "Database": {
    "Provider": "Sqlite"
  }
}
```

или:

```json
{
  "Database": {
    "Provider": "PostgreSql"
  }
}
```

Провайдер файлового хранилища выбирается через конфиг:

```json
{
  "FileStorage": {
    "Provider": "Local"
  }
}
```

или:

```json
{
  "FileStorage": {
    "Provider": "Minio"
  }
}
```

Провайдер application cache выбирается через конфиг:

```json
{
  "Cache": {
    "Provider": "Memory",
    "KnowledgeBaseExpirationSeconds": 300,
    "ReportsExpirationSeconds": 30
  }
}
```

или:

```json
{
  "Cache": {
    "Provider": "Redis"
  }
}
```

Режимы:

- SQLite + Local storage + Memory cache: локальная разработка через `dotnet run`.
- PostgreSQL + MinIO + Redis cache: Docker/production-like профиль через `docker compose`.
- Кэшируются публичные ответы базы знаний и `/api/reports/overview`; write-операции сбрасывают соответствующие cache group.

## XML Documentation

The main public API surface generates XML documentation during build and publish:

- `SupportPilot.Api`
- `SupportPilot.Application`
- `SupportPilot.Contracts`
- `SupportPilot.Domain`
- `SupportPilot.Notifications.Worker`

Swagger includes XML comments from API, Contracts, Domain and Application assemblies when the files are present in the application output directory. This documents endpoint groups, auth/ticket/admin/knowledge-base/report use cases, request/response DTOs, domain entities, enums and application ports. Minimal API routes that need route-level text use explicit OpenAPI metadata through `WithRouteDocs(...)`.

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

Use Docker API instead:

```powershell
$env:SUPPORTPILOT_API_TARGET="http://localhost:8080"
npm run dev
```

Current frontend scope includes login, JWT session persistence, protected layout, health indicator, realtime dashboard, ticket workflows, knowledge base workflows and admin management for users, roles, ticket categories and SLA policies.

## Docker Compose

Скопируйте `.env.example` в `.env` при необходимости и запустите:

```powershell
docker compose up --build
```

Compose переключает API на PostgreSQL, MinIO, RabbitMQ и Redis cache:

```text
Database__Provider=PostgreSql
FileStorage__Provider=Minio
Notifications__Transport=RabbitMQ
Cache__Provider=Redis
```

Сервисы:

- API: `http://localhost:8080`
- Notifications worker: отдельный контейнер без HTTP-порта.
- PostgreSQL: `localhost:5432`
- MinIO console: `http://localhost:9001`
- RabbitMQ management: `http://localhost:15672`
- Redis: `localhost:6379`

Health checks:

- `GET /api/health` - все проверки.
- `GET /api/health/ready` - БД, Redis, RabbitMQ, object storage.
- `GET /api/health/live` - liveness без внешних зависимостей.

## EF Core

Применить миграции вручную:

```powershell
dotnet ef database update --project src/SupportPilot.Infrastructure --startup-project src/SupportPilot.Api
```

Создать новую миграцию:

```powershell
dotnet ef migrations add MigrationName --project src/SupportPilot.Infrastructure --startup-project src/SupportPilot.Api --output-dir Data/Migrations
```

На старте API вызывает `Database.MigrateAsync()` и затем выполняет seed базовых ролей, SLA, категорий, FAQ и admin-пользователя.

## Архитектура

```text
src/
  SupportPilot.Api             HTTP endpoints, Swagger, auth policies, SignalR adapter
  SupportPilot.Application     use cases and application ports
  SupportPilot.Contracts       request/response DTO
  SupportPilot.Domain          entities, enums and core business concepts
  SupportPilot.Infrastructure  EF Core, PostgreSQL/SQLite, JWT, password hashing, file storage, notification adapters
  SupportPilot.Notifications.Worker  RabbitMQ consumer, notification persistence, SLA monitor host
tests/
  SupportPilot.IntegrationTests
```

Зависимости направлены внутрь:

```text
Api -> Application / Contracts / Infrastructure
Infrastructure -> Application / Domain
Application -> Domain / Contracts
Contracts -> Domain
```

Knowledge base workflows live in `SupportPilot.Application/KnowledgeBase/KnowledgeBaseUseCases.cs`; support dashboard report queries live in `SupportPilot.Application/Reports/ReportUseCases.cs`. API endpoints stay as request/response adapters and map `ApplicationResult<T>` to HTTP responses.

HTTP-слой принимает запросы и возвращает ответы. Бизнес-логика авторизации находится в `SupportPilot.Application/Auth/AuthUseCases.cs`, а бизнес-логика администрирования пользователей, категорий тикетов и SLA policies - в `SupportPilot.Application/Admin/AdminUseCases.cs`.

Application определяет порты:

- `IUserAccountStore` - доступ к учетным записям, ролям и audit log.
- `IPasswordHasher` - хеширование и проверка пароля.
- `ITokenService` - выпуск access token.
- `IFileStorage` - работа с файлами вложений.
- `INotificationPublisher` - публикация уведомлений в локальную БД или RabbitMQ.
- `IApplicationCache` - кэширование read-heavy application responses через Memory cache или Redis.

Infrastructure содержит реализации портов:

- `UserAccountStore` - EF Core-доступ к пользователям и ролям.
- `AspNetPasswordHasher` - хеширование через ASP.NET Core Identity.
- `JwtTokenService` - генерация JWT.
- `LocalFileStorage` и `MinioFileStorage` - файловое хранилище.
- `DatabaseNotificationPublisher` - локальный режим уведомлений без RabbitMQ.
- `RabbitMqNotificationPublisher` - публикация notification-событий в очередь.
- `RabbitMqNotificationWorker` - чтение очереди и сохранение уведомлений в БД.
- `DistributedApplicationCache` - cache abstraction поверх `IDistributedCache`, локально memory-backed, в Docker Redis-backed.

В локальном режиме API сохраняет уведомления напрямую в БД через `Notifications:Transport=Database`. В Docker/production-like режиме API публикует события в RabbitMQ через `Notifications:Transport=RabbitMQ`, а `SupportPilot.Notifications.Worker` сохраняет их в таблицу `Notifications`.

Единый результат application use cases возвращается через `ApplicationResult<T>`, а API маппит ошибки в HTTP-коды: validation, not found, unauthorized, forbidden и conflict.

## Основные endpoint-группы

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

## Проверки

```powershell
dotnet build SupportPilot.sln
dotnet test SupportPilot.sln
npm --prefix frontend run build
dotnet publish src/SupportPilot.Api/SupportPilot.Api.csproj -c Release -o artifacts/api
dotnet publish src/SupportPilot.Notifications.Worker/SupportPilot.Notifications.Worker.csproj -c Release -o artifacts/notifications-worker
```

## Следующие технические шаги

- Move notification inbox and audit-log read flows into Application use cases so the remaining API endpoints stay thin request/response adapters.
