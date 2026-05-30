# SupportPilot

SupportPilot - MVP системы обращений в поддержку на ASP.NET Core Web API.

## Что уже есть

- Clean Architecture: `Api`, `Application`, `Domain`, `Infrastructure`, `Contracts`.
- JWT-аутентификация и роли `Admin`, `Agent`, `Customer`.
- Регистрация, вход и получение текущего пользователя через application use cases.
- Обращения, категории, статусы, приоритеты и назначение ответственных.
- SLA-политики для `Critical`, `High`, `Normal`, `Low`.
- Фоновый SLA-monitor, который помечает нарушения и создает уведомления.
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

Режимы:

- SQLite + Local storage: локальная разработка через `dotnet run`.
- PostgreSQL + MinIO: Docker/production-like профиль через `docker compose`.

## Docker Compose

Скопируйте `.env.example` в `.env` при необходимости и запустите:

```powershell
docker compose up --build
```

Compose переключает API на PostgreSQL и MinIO:

```text
Database__Provider=PostgreSql
FileStorage__Provider=Minio
```

Сервисы:

- API: `http://localhost:8080`
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
  SupportPilot.Infrastructure  EF Core, PostgreSQL/SQLite, JWT, password hashing, file storage, SLA worker
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

HTTP-слой принимает запросы и возвращает ответы. Бизнес-логика авторизации находится в `SupportPilot.Application/Auth/AuthUseCases.cs`.

Application определяет порты:

- `IUserAccountStore` - доступ к учетным записям, ролям и audit log.
- `IPasswordHasher` - хеширование и проверка пароля.
- `ITokenService` - выпуск access token.
- `IFileStorage` - работа с файлами вложений.

Infrastructure содержит реализации портов:

- `UserAccountStore` - EF Core-доступ к пользователям и ролям.
- `AspNetPasswordHasher` - хеширование через ASP.NET Core Identity.
- `JwtTokenService` - генерация JWT.
- `LocalFileStorage` и `MinioFileStorage` - файловое хранилище.

Единый результат application use cases возвращается через `ApplicationResult<T>`, а API маппит ошибки в HTTP-коды: validation, not found, unauthorized, forbidden и conflict.

## Основные endpoint-группы

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/auth/me`
- `GET /api/tickets`
- `POST /api/tickets`
- `GET /api/tickets/{id}`
- `PATCH /api/tickets/{id}/status`
- `PATCH /api/tickets/{id}/assignee`
- `POST /api/tickets/{id}/comments`
- `POST /api/tickets/{id}/attachments`
- `GET /api/tickets/{ticketId}/attachments/{attachmentId}`
- `DELETE /api/tickets/{ticketId}/attachments/{attachmentId}`
- `GET /api/kb/articles?search=...`
- `GET /api/reports/overview`
- `GET /api/admin/audit`

## Проверки

```powershell
dotnet build SupportPilot.sln
dotnet test SupportPilot.sln
dotnet publish src/SupportPilot.Api/SupportPilot.Api.csproj -c Release -o artifacts/api
```

## Следующие технические шаги

- Вынести уведомления в отдельный RabbitMQ worker.
- Добавить Redis cache для базы знаний и отчетов.
- Добавить React/Blazor frontend после восстановления доступа к `node.exe`.
