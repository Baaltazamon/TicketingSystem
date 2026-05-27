# SupportPilot

SupportPilot - MVP системы обращений в поддержку на ASP.NET Core Web API.

## Что уже есть

- Clean Architecture разбиение: `Api`, `Application`, `Domain`, `Infrastructure`, `Contracts`.
- JWT-аутентификация и роли `Admin`, `Agent`, `Customer`.
- Обращения, категории, статусы, приоритеты и назначение ответственных.
- SLA-политики для `Critical`, `High`, `Normal`, `Low`.
- Фоновый SLA-monitor, который помечает нарушения и создает уведомления.
- Публичные комментарии и внутренние заметки.
- Вложения через локальное файловое хранилище вне БД.
- Timeline обращения: создание, смена статуса, комментарии, файлы.
- База знаний / FAQ с поиском по статьям.
- Отчеты, аудит и SignalR hub `/hubs/tickets`.
- Docker Compose для API, Redis, RabbitMQ и MinIO.

## Быстрый запуск

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

## Docker Compose

```powershell
docker compose up --build
```

Сервисы:

- API: `http://localhost:8080`
- MinIO console: `http://localhost:9001`
- RabbitMQ management: `http://localhost:15672`
- Redis: `localhost:6379`

Redis, RabbitMQ и MinIO пока подключены как инфраструктурная база для следующих итераций. В текущем MVP API использует SQLite и локальное файловое хранилище.

## Архитектура

```text
src/
  SupportPilot.Api             HTTP endpoints, Swagger, auth policies, SignalR adapter
  SupportPilot.Application     application ports and use-case boundaries
  SupportPilot.Contracts       request/response DTO
  SupportPilot.Domain          entities, enums and core business concepts
  SupportPilot.Infrastructure  EF Core, SQLite, JWT, file storage, SLA background worker
```

Зависимости направлены внутрь:

```text
Api -> Application / Contracts / Infrastructure
Infrastructure -> Application / Domain
Application -> Domain
Contracts -> Domain
```

Внешние механизмы подключаются через порты из `Application`. Например, SLA worker из `Infrastructure` уведомляет клиентов через `ITicketRealtimeNotifier`, а конкретная SignalR-реализация находится в `Api`.

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
- `GET /api/kb/articles?search=...`
- `GET /api/reports/overview`
- `GET /api/admin/audit`

## Следующие технические шаги

- Заменить `EnsureCreated` на EF Core migrations.
- Подключить MinIO как реализацию `IFileStorage`.
- Вынести уведомления в RabbitMQ worker.
- Добавить Redis cache для базы знаний и отчетов.
- Добавить React/Blazor frontend после восстановления доступа к `node.exe`.
