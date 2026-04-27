# Hermes

Hermes is a **personal news digest service**. The idea is that you configure **who you are** and **what news you care about** once—through a **Blazor web frontend** (interactive UI wired to the API)—and the system persists that configuration in a database via a **REST API**. On a **schedule** you define (weekdays and times), a **background component** fetches matching articles from a **third-party news API** (**[NewsData.io](https://newsdata.io/)** — already integrated via `**Hermes.NewsClient`**), composes an **HTML email** using a dedicated layout, and sends it so you receive **regular, predictable** news by mail. Hermes does not own the news corpus; it **calls NewsData.io’s HTTP API** (e.g. the **latest** endpoint) using your API key and the filters derived from each user’s saved profile.

The codebase is intentionally structured for **clarity and maintainability**: layered architecture, explicit domain models, validation at the API boundary, and separate libraries for **news retrieval** and **email delivery**. **Automated tests** are planned as the surface area stabilizes. **Docker** is the intended packaging and deployment story for running the API, database, frontend, and future worker process together.

For **HTTP route details, request/response examples, and OpenAPI notes**, see `[Hermes.Api/README.md](Hermes.Api/README.md)`.

---

## Product vision (end state)

1. **Web UI**: Sign in, manage account basics, and edit one or more **news profiles** per user (keywords, categories, languages, countries, send days, send times).
2. **API + database**: The UI talks to **Hermes.Api**; settings are validated and stored as structured entities (not ad hoc JSON blobs where avoidable).
3. **Scheduled delivery**: A **server-side scheduler** (see note below on naming) runs periodically, determines which users/profiles are due, calls **NewsData.io** (through `**Hermes.NewsClient`**) for fresh articles, fills the **HTML newsletter templates**, sends email via **SMTP**, and records outcomes in **notification logs**.

This is **not** a browser “Service Worker” in the PWA sense. Service workers run in the client and cannot reliably replace a server cron or a .NET **hosted service / worker** that has access to your database, API keys, and SMTP credentials. Hermes will use an **always-on backend process** (or separate worker host) for scheduling and sending.

---

## Repository layout and responsibilities

The solution is organized into focused projects:


| Project                                                | Responsibility                                                                                                                                                                                                                                                                                                                                                                       |
| ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Hermes.Domain**                                      | Core **entities** (`User`, `News`, `NotificationLog`), **DTOs**, **enums** (categories, languages, countries, weekdays, delivery channel, notification status), and **abstractions** the application depends on.                                                                                                                                                                     |
| **Hermes.Application**                                 | **Use cases** and **services** (users, authentication, news configuration, etc.) that orchestrate domain rules and call into persistence through interfaces.                                                                                                                                                                                                                         |
| **Hermes.Infrastructure**                              | **Entity Framework Core** with **Pomelo.EntityFrameworkCore.MySql**; **repositories**; `HermesDbContext`; resilience helpers (e.g. **Polly**) where appropriate. The database is **MySQL**.                                                                                                                                                                                          |
| **Hermes.Api**                                         | **ASP.NET Core** host: controllers, **JWT** authentication, **FluentValidation**, global exception handling mapped to **Problem Details**, **health** endpoints (live/ready), **CORS** and DI composition. OpenAPI is available in **Development**.                                                                                                                                  |
| **Hermes.NewsClient**                                  | Typed **HTTP client** for the external **[NewsData.io](https://newsdata.io/)** REST API (**latest** news): URL construction (`NewsDataIoUrlBuilder`), query parameters (`ApiUrlParts` — API key, countries, languages, categories, timezone, sort, optional flags), deserialization DTOs, `NewsDataIoClient.GetLatestAsync`. This is the **live news source** Hermes talks to today. |
| **Hermes.Notifications**                               | **Email sending** (`IEmailSender`, `SmtpEmailSender` using `System.Net.Mail.SmtpClient`), configuration models, and **HTML newsletter composition** (`NewsletterHtmlComposer`) from **embedded** partial templates (header, repeating item row, footer).                                                                                                                             |
| **Hermes.WebFrontend** / **Hermes.WebFrontend.Client** | **Blazor Web App** (.NET 10) with **Interactive WebAssembly**: authentication (login, register + auto-login), JWT/refresh via `HttpClient`, home, user profile, and CRUD UI for **news digest profiles**. See `[Hermes.WebFrontend/README.md](Hermes.WebFrontend/README.md)`.                                                                                                        |
| **Hermes**                                             | Small **console** executable referencing **NewsClient** and **Notifications**—useful as an **integration playground** or local experiments (e.g. send a composed mail in dev). It is not the production scheduler.                                                                                                                                                                   |


```mermaid
flowchart LR
  subgraph client [Client]
    FE[Blazor WebFrontend]
  end
  subgraph backend [Backend today]
    API[Hermes.Api]
    APP[Hermes.Application]
    INF[Hermes.Infrastructure]
    DB[(MySQL)]
  end
  subgraph libs [Libraries]
    NC[Hermes.NewsClient]
    NT[Hermes.Notifications]
  end
  subgraph external [External]
    NDI[NewsData.io API]
  end
  FE -->|REST + JWT| API
  API --> APP
  APP --> INF
  INF --> DB
  Worker[Future: hosted worker] -.->|read schedules| INF
  Worker -.-> NC
  NC -.->|HTTPS latest| NDI
  Worker -.-> NT
```



---

## What is already implemented (and how)

### Authentication and users

- **Registration** and **user** CRUD-style operations are exposed from the API and implemented through application services and EF-backed repositories.
- **Login** returns a **short-lived JWT** access token and an **opaque refresh token**. Refresh tokens are stored **hashed** server-side; rotation is supported via a dedicated **refresh** endpoint. **Logout** can revoke the current refresh session or all sessions for the user.
- JWT signing and validation settings live under configuration (e.g. `Jwt` in `appsettings`); production secrets should be supplied via **environment variables** or a secret store, not committed files.

### Personalized news configuration (`News` entity)

Each row represents a **digest profile** for a user, including:

- **Keywords**, **categories**, **languages**, **countries** (aligned with domain enums and API JSON as string enums where applicable).
- **SendOnWeekdays** and **SendAtTimes** — the data model already captures *when* a digest should run; the **orchestration** that reads these fields and triggers sends is the main piece still to wire up.

The API exposes **list**, **get by id**, **create**, **update**, and **delete** (including delete-all for a user) under versioned routes. Authorization ensures callers can only access their own user’s data where applicable.

### Notification logs

- A **notification log** entity tracks **sent-at** time, **status**, **channel** (e.g. email), optional **error message**, **retry** metadata, etc.
- The API can **append** log entries so the delivery pipeline (once implemented) can record success, failure, and retries for observability and debugging.

### Third-party news API: NewsData.io (`Hermes.NewsClient`)

Hermes already integrates with **[NewsData.io](https://newsdata.io/)** as the **external news provider**. That service exposes a documented **REST** surface; this repository implements the client side only.

- `**NewsDataIoUrlBuilder`** builds the **GET** URL for the **latest** feed (base URL `https://newsdata.io/api/1/latest` with query string).
- `**ApiUrlParts`** carries everything needed for that request: **API key** (required), optional **countries**, **languages**, **categories**, **timezone**, **sort**, image / dedupe flags, and **field exclusion** defaults tuned for lighter payloads.
- `**NewsDataIoClient.GetLatestAsync`** executes the request and deserializes the JSON into DTOs (`NewsDataIoDto` / result rows).

The **Hermes.Api** stores *what* to ask for per user (`News` entity: keywords, categories, languages, countries, schedule). The **worker** (not yet wired) will translate those rows into `ApiUrlParts`, call NewsData.io, map articles into `**NewsletterItemContent`**, and hand off to the mail composer. Until then, `**Hermes.NewsClient**` is fully usable from code (including the small `**Hermes**` console playground) for manual or experimental fetches.

### Web frontend (`Hermes.WebFrontend`)

- **Stack:** Blazor **Web App** host + **WebAssembly** client; API base URL from client `wwwroot/appsettings.json` (`ApiBaseUrl`).
- **Auth:** Login and registration call `api/v1/auth/login` and `api/v1/users`; tokens stored in **browser local storage**; `AuthMessageHandler` attaches Bearer tokens; **refresh** and session idle handling on the client.
- **Routes (examples):** `/` (redirect), `/login`, `/register`, `/home`, `/user-settings`, `/news-settings` (and `/news-settings/new` for create).
- **UI:** Swiss-style poster chrome (main layout, home rail, auth side panel), top navigation for authenticated areas, `GlobalAuthGuard` for protected navigation.
- **CORS:** The API must list the Blazor dev origin (e.g. `http://localhost:5269`) under `Cors:AllowedOrigins`—see `[Hermes.WebFrontend/README.md](Hermes.WebFrontend/README.md)`.

### Email and HTML layout (`Hermes.Notifications`)

- **SMTP** delivery is abstracted behind `IEmailSender` with a concrete `SmtpEmailSender` taking **host, port, SSL, credentials, from/reply-to**, etc.
- **NewsletterHtmlComposer** loads **embedded** HTML fragments (`NewsletterHeader.html`, `NewsletterItem.html`, `NewsletterFooter.html`), substitutes placeholders, repeats the item template per article, and returns a **single HTML document** suitable for `IsBodyHtml` email.
- Together, this is the **presentation layer** for the digest email; the missing link is feeding it **live article data** from the news client on a schedule.

### API quality and operations

- **FluentValidation** for input; failures return **400** with `ValidationProblemDetails`.
- A **global exception handler** maps domain and infrastructure failures to appropriate status codes (**403**, **404**, **409**, etc.) without leaking internal details in production-oriented responses.
- **Health checks**: **liveness** and **readiness** (readiness includes the database) for orchestration and future container deployments.

---

## What is not implemented yet (by design)

- **No production scheduler**: Nothing in the solution yet continuously evaluates `SendOnWeekdays` / `SendAtTimes` and triggers sends. That will be a **.NET hosted service**, **Quartz.NET**, or a **separate worker process**—invoking **Hermes.Application** / repositories for due rows, **Hermes.NewsClient** for content, **Hermes.Notifications** for HTML + SMTP, and **notification logs** for outcomes.
- **Email verification flow**: Not fully productized end-to-end in the UI (API/domain may already expose related fields).
- **Tests**: **unit tests** for services and **integration tests** for API + database are natural next steps once behaviors stabilize.

---

## Roadmap (high level)

1. **Frontend polish**: Hardening (tests, a11y), optional session/refresh-token cleanup UX, and any remaining flows (e.g. email verification). Details: `[Hermes.WebFrontend/README.md](Hermes.WebFrontend/README.md)`.
2. **Worker / scheduler**: New host or extension of the API host—read due profiles, respect time zones if needed, call NewsData.io, map results into `NewsletterItemContent`, compose HTML, send mail, write/update logs, handle retries.
3. **Configuration & secrets**: SMTP settings, NewsData API key, connection strings, JWT keys—standardized for **Development** vs **Production**, all overrideable via environment variables.
4. **Testing**: Unit tests for composition and scheduling logic; integration tests for API and persistence; optional contract tests against OpenAPI.
5. **Docker**: `Dockerfile`(s) for API, worker, and static/Blazor hosting; **docker-compose** with MySQL, optional **MailHog** (or similar) for local SMTP capture, and documented ports/volumes.

---

## Docker and deployment intent

The target runtime is **containerized**:

- **Hermes.Api** as one (or more) API container(s) behind a reverse proxy if needed.
- **MySQL** (or compatible) as a database container with persisted volume.
- **Hermes.WebFrontend** served from its container or static hosting, configured with the API base URL.
- A future **worker** container with the same configuration surface (connection string, SMTP, news API key) but without public HTTP, or with only health/metrics if desired.

Exact compose files and images are **not** committed yet; this README states the **direction** so new work (CI, compose, K8s manifests) stays consistent.

---

## Quality bar

The author aims to keep Hermes at a **solid engineering level**: clear boundaries between layers, typed APIs, validation and error contracts suitable for a real client, and reusable libraries for **news** and **mail** so the scheduler remains thin. Documentation (this file, `[Hermes.Api/README.md](Hermes.Api/README.md)`) should stay close to what the code actually does.

---

## Building and running (brief)

Requirements: **.NET SDK** matching the solution target (currently **.NET 10** in project files), and a **MySQL** instance configured in `Hermes.Api` settings for local runs.

```bash
dotnet build Hermes.slnx
```

Run the API from the `Hermes.Api` project directory (set `ASPNETCORE_ENVIRONMENT=Development` for OpenAPI). See `[Hermes.Api/README.md](Hermes.Api/README.md)` for endpoint summaries and `GET /openapi/v1.json` in Development.

Run the Blazor app from `Hermes.WebFrontend/Hermes.WebFrontend` (`dotnet run`). Configure `ApiBaseUrl` and CORS as described in `[Hermes.WebFrontend/README.md](Hermes.WebFrontend/README.md)`.