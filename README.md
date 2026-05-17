# Hermes

Hermes is a **personal news digest service**: configure **who you are** and **what news you care about** in a **Blazor** front end; **`Hermes.Api`** persists profiles; **`Hermes.Worker`** (Hangfire on **MySQL**) runs on your schedule, fetches headlines from **[NewsData.io](https://newsdata.io/)** via **`Hermes.Infrastructure`**, renders HTML via **`Hermes.Notifications`**, and delivers mail over **SMTP**.

The codebase uses a **hexagonal (ports-and-adapters)** shape: **`Hermes.Domain`** and **`Hermes.Application`** define the core; adapters include REST (**`Hermes.Api`**), EF Core (**`Hermes.Infrastructure`** → **MySQL**), **`NewsDataIoClient`**, and **`Hermes.Notifications`**. Automated tests live in **`Hermes.UnitTests`** and **`Hermes.IntegrationTests`** (Docker/Testcontainers MySQL).

**Deployment:** The usual setup is **`docker compose`** in **`Docker/`**: it builds **MySQL**, runs **EF migrations**, then brings up **API**, **worker**, and **MailHog** (SMTP capture). **`Hermes.WebFrontend` stays outside Compose** (`dotnet run` under `Hermes.WebFrontend/Hermes.WebFrontend`) with **`ApiBaseUrl`** pointing at the published API (**`http://localhost:5165/`** when using the compose port map) and the API **`Cors:AllowedOrigins`** including the Blazor dev URL.

---

## Product overview

1. **Web UI**: Sign in (**JWT**, refresh rotation), manage account (**password**, **e-mail**) with **security** features including **e-mail verification** (codes sent over SMTP; confirm via API/UI), manage **news profiles** (keywords, categories, languages, countries, send days, send times), and browse an authenticated **home** (`/home`).
2. **API + database**: The UI drives **`Hermes.Api`** (`/api/v1/…`). Settings are validated and stored as **`News`** and related structured rows.
3. **Scheduled delivery**: **`Hermes.Worker`** wakes on a recurring Hangfire slot (minute-granularity by default), detects due profiles via application services, enqueues digest jobs per profile row, pulls **NewsData.io**, composes **`NewsletterHtmlComposer`** output, SMTP-sends outcomes, writes **notification logs**, and optionally shares Hangfire tables with the API so profile changes can nudge scheduling.

---

## Repository layout and responsibilities

| Project | Responsibility |
|---------|----------------|
| **Hermes.Domain** | **Entities**, **DTOs** (e.g. **`UserScope`**, **`isEmailVerified`**), **`HermesProblemTypes`**, enums, exceptions, repositories-as-interfaces consumed by **`Hermes.Application`**. |
| **Hermes.Application** | Use-case **services**: users/auth, **`News`** configuration, newsletters, hashing/verification behaviours; depends on persistence abstractions, not EF types. |
| **Hermes.Infrastructure** | **`HermesDbContext`**, EF Core + **MySQL**, repository implementations **`NewsDataIoClient`** / **`INewsArticleProvider`**, Polly (where wired). |
| **Hermes.Api** | **`/api/v1/` path versioning**, controllers, JWT, FluentValidation, global exception mapper → RFC 7807 **Problem Details**, health (`/health/live`, `/health/ready`), CORS/OpenAPI/Serilog/OpenTelemetry knobs. **`GET /openapi/v1.json`**. |
| **Hermes.Notifications** | **`IEmailSender`**, **`NewsletterHtmlComposer`**, **`VerificationHtmlComposer`** and embedded Razor/static HTML snippets. |
| **Hermes.UnitTests** | xUnit/Moq coverage for services, JWT/refresh hashing, validators, **`NewsDataIoUrlBuilder`**, weekday mappers, newsletter scheduler pipeline, **`HermesDbContext`** helpers (often **EF InMemory**). |
| **Hermes.IntegrationTests** | Testcontainers-backed **`WebApplicationFactory`** probes (auth rotations, JWT failures, **`/users/*/news`** CRUD, verification routes, **`/notification-logs`**, probes when DB stops). Tagged **`Integration=Docker`**. |
| **Hermes.Worker** | Hangfire **MySqlStorage**, **`NewsletterScheduler`**, enqueue **`NotificationJobs`**, binds same application/infrastructure/email stack without serving public HTTP controllers. |
| **Hermes.WebFrontend** / **`Hermes.WebFrontend.Client`** | Blazor WASM shell: guarded routes (`GlobalAuthGuard`), **`AuthMessageHandler`, `AuthTokenStore`**, **`NewsSettingsPanel`/`NewsSubscriptionCard`**, **`/user-settings`**, **`/news-settings`** flows. |

---

## What is already implemented

### Authentication and users

- **Registration**, profile updates (**BCrypt**, **wrong-current-password typed problem**) and guarded **`/news`** and **`/users`** routes.
- **Login** emits short-lived JWT + opaque refresh (**hashed-at-rest**) with rotation + replay detection on **`POST /auth/refresh`** plus scoped logout (**revoke targeted/all refresh rows**).
- **E-mail change** resets verification until the inbox proves ownership again (**six-digit**, **`POST …/verify/code`** + resend workflows).
- **Tokens** persisted in browser storage for SPA calls; SPA refresh path uses unnamed **`HttpClient`**.

![Hermes login page in the auth layout.](Documentation/LoginPage.png)

![Registration page before submit (file name `RestisterPage.png`).](Documentation/RestisterPage.png)

![Registration page with fields filled.](Documentation/FilledOutRegisterPage.png)

### Auth flow (HTTP summary)

1. SPA collects credentials or launches **register**.
2. **Register:** `POST /api/v1/users` persists user (**hashed password**) → SPA immediately calls **`POST /api/v1/auth/login`**.
3. **Login:** SPA calls **`POST /api/v1/auth/login`** (skipping `/users`).
4. **API** verifies against **MySQL**, returns **JWT + refresh**, stores hashed refresh fingerprint server-side.
5. SPA persists both artifacts for subsequent Bearer calls (**`AuthMessageHandler`**).

![Modal for entering the e-mail verification code.](Documentation/VerificationPopup.png)

![MailHog web UI showing a Hermes verification e-mail.](Documentation/MailHogVerificationCode.png)

### Home (`/home`)

- **`AppHomeLayout`**: authenticated **dashboard**/`Welcome` rail, **`HermesTopNavigation`**, teaser cards—post-login routing target after **`RootRedirect`**.

![Hermes home after sign-in (`/home`).](Documentation/HomePage.png)

### Personalized news configuration (`News` entity)

- Fields per profile: keywords, enums for categories/langs/countries, **`SendOnWeekdays`**, **`SendAtTimes`**; worker maps these to NewsData payloads.
- **API** exposes list/get/create/update/delete (incl delete-all scope) guarded per **user**.
- **`NewsSettingsPanel`** paging defaults to **`pageSize = 20`**; pager appears when **`totalPages > 1`**; users can toggle **10/20/50** via **`Pro Seite`** alongside keyword/category filters, **`Sortierung`**, **`Suchen`** (implementation: `Hermes.WebFrontend/Hermes.WebFrontend.Client/Components/NewsSettingsPanel.razor`).

![Empty `/news-settings` state before the first profile exists.](Documentation/EmptyNewsSettings.png)

![Overview of digest profile cards.](Documentation/NewsCardOverview.png)

![Overview with filters, search, and sort applied.](Documentation/NewsCardOverviewWithFilters.png)

![Create/edit digest profile form (collapsed card).](Documentation/NewsForm.png)

![Profile form filled before save.](Documentation/NewsFormFilledOut.png)

### Notification logs

- Persist send attempts/results for observability (**status**, SMTP errors, timestamps); worker & API callers record entries.

### Third-party news API: NewsData.io

- Implemented through **`Hermes.Infrastructure`** (**`NewsDataIoUrlBuilder`**, **`NewsDataIoClient`**). Worker maps digest rows → **`NewsletterItemContent`** and templates.

### Web frontend highlights

- **Blazor WASM** + **`ApiBaseUrl`** injected into scoped **`HttpClient`**.
- **`/login`**, **`/register`**, **`/home`**, **`/user-settings`** (verification UI + password UX), **`/news-settings`** (card editor + paging shell).
- **CORS**: list the dev HTTPS/HTTP URIs emitted by **`Properties/launchSettings.json`** inside API **`Cors:AllowedOrigins`**.

![User settings with verified e-mail.](Documentation/UserSettingsVerifiedMail.png)

![User settings while e-mail is not yet verified.](Documentation/UserSettingsNotVerfiedMail.png)

### E-mail rendering (`Hermes.Notifications`)

- **`NewsletterHtmlComposer`** merges header/footer/repeat item partials; **`VerificationHtmlComposer`** handles codes + branding placeholders.
- Typical dev mail sink: **`MailHog`** on SMTP **1025** with UI **8025** (matching worker defaults).

![Rendered newsletter HTML preview (often viewed in MailHog or browser dev tools).](Documentation/NewsMailSneekPeek.png)

Sample raw MIME artefact from the same pipeline: **[`Documentation/ExampleMail.eml`](Documentation/ExampleMail.eml)**.

### Scheduled delivery (`Hermes.Worker`)

- **Hangfire recurring job** (default **cron minutely**) enqueues **`SendNewsDigestAsync`** per **`(user, news row)`**.
- Shares **Hangfire/MySQL schema** when **`ConnectionStrings:Hangfire`** matches default connection so API-triggered **`BackgroundJob`** runs hit the same queues.
- **SMTP** aligns with Notifications settings (**Mailhog** parity for local).

### API quality / ops

- **FluentValidation**, global exception normalization, **`/health/live`/`/ready`** (DB probes readiness).
- **CI:** `.github/workflows/ci-cd.yml` release-builds **`Hermes.slnx`**, merges coverlet artefacts (**65% merged line gate** today), attaches TRX/code-coverage, builds/pushes **API + Worker** images, scans with **Trivy**.

---

## Observability & OpenAPI

Hermes emits **structured Serilog**, optional OTLP exporters, and publishes **OpenAPI v1**:

- Logs add **CorrelationIdMiddleware** enrichment + **`Serilog.Enrichers.Span`** when tracing is active (**API `UseHermesSerilog`**, **Worker `UseHermesWorkerSerilog`**).
- **OpenTelemetry toggles**: API instruments ASP.NET (**health routes filtered** optionally), **`HttpClient`**, runtime counters; Worker instruments **`Microsoft.EntityFrameworkCore`**, runtime counters; both OTLP exporters honor **`OpenTelemetry:{ServiceName,OtlpEndpoint,OtlpHeaders}`**/`OTEL_EXPORTER_OTLP_ENDPOINT`.
- **`OpenApi`** controls **`GET /openapi/v1.json`**: auto-exposed outside Production; guarded or disabled in Production (**`DocumentationApiKey`**, **`DocumentationPathPrefix`**, **`X-Hermes-Documentation-Key` header** hides docs when unauthorized).

---

## Docker and deployment

- **`Docker/docker-compose.yml`** defines **MailHog**, **MySQL 8**, **`hermes-migrate`** (`Dockerfile.Migrate` runs **`dotnet ef database update`** and exits), **`hermes-api`** (published on **5165**), and **`hermes-worker`**. Compose waits for migrations to succeed before starting API/worker.
- **`Docker/.env`** is required (**MySQL passwords**, **`JWT_SIGNING_KEY`**, **`NEWS_DATA_IO_KEY`**, mail host ports, etc.); copy from your team template or populate locally before `compose up`.

---

## Testing

```bash
dotnet test Hermes.slnx                           # Runs unit tests + IntegrationTests spin-up
dotnet test Hermes.slnx --filter "Integration=Docker"
dotnet build Hermes.slnx -warnaserror              # Mirrors CI Roslyn posture without tests
```

**Integration** suite requires **Docker** (pulls **`mysql:8.4`**). **Unit tests** stay offline except where they purposely spin InMemory **`HermesDbContext`**.

---

## Building and running

**Backend (recommended):**

```bash
cd Docker
docker compose up -d --build
```

That **builds** the API/worker images, starts **MySQL** and **MailHog**, applies **database migrations** via **`hermes-migrate`** (once per `up`; API/worker start only after it completes), then starts **Hermes.Api** (**`localhost:5165`**) and **Hermes.Worker**. Configure **`Docker/.env`** first (JWT, DB credentials, **`NEWS_DATA_IO_KEY`**, etc.).

**Frontend (always separate from this compose file):**

```bash
dotnet run --project Hermes.WebFrontend/Hermes.WebFrontend/Hermes.WebFrontend.csproj
```

Point the client **`ApiBaseUrl`** at the API (e.g. **`http://localhost:5165/`**) and ensure the API **`Cors:AllowedOrigins`** includes your Blazor origin (see **`Properties/launchSettings.json`** for the HTTPS/HTTP ports you use locally).

OpenAPI (**`/openapi/v1.json`**) behaves as described under **Observability & OpenAPI** (Production key gate when configured).

Optional: **`dotnet`** runs of API/worker/MySQL remain possible without Compose—then supply connection strings, migrations, JWT, SMTP, and **`NewsDataIo:Key`** yourself as for any local ASP.NET host.
