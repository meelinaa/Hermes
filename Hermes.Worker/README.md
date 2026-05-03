# Hermes.Worker

**Hermes.Worker** is a **.NET Worker Service** that runs the **scheduled newsletter pipeline** for Hermes: it wakes up on a timer, finds user **news digest profiles** that are due, fetches articles from **NewsData.io**, composes **HTML email** via `Hermes.Notifications`, sends through **SMTP**, and records results in the database (`NotificationLog` and related persistence through `Hermes.Infrastructure`).

The API (`Hermes.Api`) and the Blazor frontend manage **who** you are, **what** digest profiles you want, **profile and password updates** (current-password checks; API may return a typed `ProblemDetails` **`type`** when the current password is wrong), and **e-mail verification** orchestration; the worker is the **always-on backend process** that actually performs **time-based newsletter delivery**. It is **not** a browser service worker—it has access to the database, API keys, and mail configuration server-side. Verification **e-mails** use the same mail composition/SMTP stack as digests where applicable; the verification **HTTP** API lives on the API host (see [`Hermes.Api/README.md`](../Hermes.Api/README.md)).

---

## Role in the solution

| Concern | Where it lives |
|--------|----------------|
| Due-time logic (weekdays, times, per-profile rows) | `Hermes.Application` (`INewsletterScheduleService`, `INewsletterDigestService`) |
| Database & EF Core | `Hermes.Infrastructure` (`HermesDbContext`, repositories) |
| News HTTP client (NewsData.io) | `Hermes.Infrastructure` (`NewsDataIoClient` implements `INewsArticleProvider`) |
| Email HTML + SMTP abstraction | `Hermes.Notifications` (`NewsletterHtmlComposer`, `IEmailSender` / `SmtpEmailSender`) |
| **When to run** (recurring tick + job queue) | **This project** (Hangfire + MySQL storage) |

The worker **reuses** the same application and infrastructure types as the API so business rules stay in one place.

---

## Scheduling model (Hangfire)

1. **Recurring job**  
   On startup, `Program.cs` registers a recurring Hangfire job (`NewsletterSchedulerRecurringJob.Id` in `Hermes.Application`) that invokes `NewsletterScheduler.RunAsync` **every minute** (`Cron.Minutely()`), using the host’s **local** timezone for the recurring schedule.

2. **Due detection**  
   `NewsletterScheduler` calls `INewsletterScheduleService.GetDueItemsAsync` with the current time. Each **matching** `(userId, newsId)` pair represents **one** digest profile row—**one email per row**, not merged per user.

3. **Background jobs**  
   For each due item, it **enqueues** `NotificationJobs.SendNewsDigestAsync`, which delegates to `INewsletterDigestService.SendAsync`. That method applies **idempotency** (no duplicate send for the same user/news **minute slot** in UTC, as documented in configuration comments).

4. **Shared storage with the API**  
   Hangfire uses **MySQL** (`Hangfire.MySql` / `MySqlStorage`) with a configurable connection string. By default, `ConnectionStrings:Hangfire` falls back to `ConnectionStrings:DefaultConnection`, so the API and worker can share the **same** Hangfire tables. The API registers `JobStorage` and `HangfireNewsletterSchedulerRunTrigger` so that after **news profile mutations**, it can **trigger** the same recurring job early (useful for quicker feedback while developing).

---

## Configuration

Settings are read from `appsettings.json`, `appsettings.{Environment}.json`, and environment variables. The **NewsData.io API key is read only from a `.env` file** (not from `appsettings`): place `.env` next to the worker project or publish folder, or use one line from `NEWSDATA.IO: <your-api-key>`, `NewsDataIo__ApiKey=<your-api-key>`, or `NEWSDATA_IO_API_KEY=<your-api-key>` (see `WorkerServiceCollectionHelper.TryReadNewsDataIoApiKeyFromEnvFile`). Docker Compose mounts `Hermes.Worker/.env` into the container as `/app/.env`.

| Section | Purpose |
|--------|---------|
| `ConnectionStrings:DefaultConnection` | MySQL for Hermes app data (required). |
| `ConnectionStrings:Hangfire` | Optional; if omitted, Hangfire uses `DefaultConnection`. |
| `.env` (NewsData.io key) | Required for newsletter article fetches; not configured via `appsettings`. |
| `Email` | SMTP host, port, SSL, credentials, from/reply-to (see `EmailSettings`). |
| `MailHog` | `BaseUrl` for logging the web UI hint; `SendSchedulerTestMailEachMinute` sends a tiny test mail each tick when `true` (local dev with [MailHog](https://github.com/mailhog/MailHog)). |

Local development often uses **MailHog** on **SMTP port 1025** and the web UI on **8025** (see `appsettings.json` defaults).

---

## How to run

**Prerequisites:** .NET SDK (same as the solution, **.NET 10**), a reachable **MySQL** instance, and (for full behavior) a **NewsData.io** key and SMTP (or MailHog).

From the worker project directory:

```bash
dotnet run --project Hermes.Worker/Hermes.Worker.csproj
```

Ensure `ConnectionStrings:DefaultConnection` matches your database and run **EF migrations** if the schema is not up to date (same as for `Hermes.Api`). Hangfire will create its tables in MySQL when the worker starts.

**Typical dev setup:** run **MySQL**, **MailHog**, **Hermes.Api**, **Hermes.WebFrontend**, and **Hermes.Worker** together so you can change profiles in the UI and see digests appear in MailHog.

---

## Automated tests

Worker-facing behaviour is covered mainly by **`Hermes.UnitTests`**: e.g. **`NewsletterSchedulerTests`**, **`NewsletterScheduleServiceTests`**, **`NewsletterDigestServiceTests`**, plus persistence helpers such as **`HermesDbContextTests`** where they touch notification-window queries. Run the full suite from the repository root:

```bash
dotnet test Hermes.slnx
```

**`Hermes.IntegrationTests`** exercises **`Hermes.Api`** against real MySQL (Docker). The worker is a separate host and is not started by that suite; add worker-level integration coverage when you need full pipeline tests.

---

## Operations and production notes

- **Tick frequency:** The code uses **every minute** so due times align with minute granularity. For production you might switch to a less aggressive cron or a dedicated queue strategy; the **due-selection logic** in application services should remain the source of truth.
- **Time zones:** The scheduler uses the **host’s** `TimeZoneInfo.Local` for the recurring slot; ensure deployment machines or containers use the intended timezone, or evolve the design toward explicit user/timezone handling in the domain.
- **Secrets:** Do not commit API keys or SMTP passwords; use **user secrets**, environment variables, or a secret manager.
- **Observability:** Structured logs are emitted from `NewsletterScheduler` and `NotificationJobs`; notification outcomes are also persisted for auditing.

---

## Project layout (this folder)

| Path | Responsibility |
|------|----------------|
| `Program.cs` | Host builder, Hangfire `JobStorage.Current`, recurring job registration, `host.Run()`. |
| `Hosting/WorkerServiceCollectionExtensions.cs` | DI: EF Core, email, NewsData.io HTTP client, digest/schedule services, Hangfire server + MySQL storage. |
| `Hosting/WorkerServiceCollectionHelper.cs` | `.env` key loading, `EmailSettings` binding, MailHog hint logging. |
| `Scheduling/NewsletterScheduler.cs` | Minutely entry point: resolve due items, enqueue digest jobs. |
| `Jobs/NotificationJobs.cs` | Hangfire-invokable wrapper around `INewsletterDigestService`. |
| `MailHog/MailHogSchedulerTestMail.cs` | Optional test message for SMTP/MailHog verification. |

For REST routes and OpenAPI, see [`Hermes.Api/README.md`](../Hermes.Api/README.md). For the Blazor client, see [`Hermes.WebFrontend/README.md`](../Hermes.WebFrontend/README.md). For the overall product and repository map, see the [root `README.md`](../README.md).
