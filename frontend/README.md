# SupportPilot Frontend

React + Vite frontend shell for the SupportPilot API.

## Local Run

Install dependencies:

```powershell
npm install
```

Start the backend API in another terminal:

```powershell
dotnet run --project ../src/SupportPilot.Api
```

Start the frontend:

```powershell
npm run dev
```

The app runs on:

```text
http://localhost:5173
```

The Vite dev server proxies `/api` and `/hubs` to the backend. The default backend target is:

```text
http://localhost:5295
```

Override it with:

```powershell
$env:SUPPORTPILOT_API_TARGET="http://localhost:8080"
npm run dev
```

## Scope

The frontend currently contains:

- login and JWT session persistence;
- protected application layout;
- API readiness indicator;
- ticket list filters;
- ticket creation;
- ticket details with SLA metadata;
- status and assignment controls for support users;
- public comments and internal notes;
- attachment upload, download and deletion;
- navigation placeholders for dashboard, knowledge base and admin.

Realtime dashboard widgets, knowledge base administration and Redis-backed backend caching belong to separate PRs.
