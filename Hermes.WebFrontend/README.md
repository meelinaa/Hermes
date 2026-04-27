# Hermes Web Frontend

Blazor **Web App** (.NET 10) with **Interactive WebAssembly**: the server host serves HTML and static assets; interactive UI runs in the browser as **WebAssembly** and calls **Hermes.Api** over HTTP (JWT + refresh token).

Full API documentation: [`../Hermes.Api/README.md`](../Hermes.Api/README.md).

### Role and scope

This frontend is for **defining and maintaining settings the Hermes worker needs** (scheduler / digest delivery) and for making **authentication observable** — e.g. **JWT**, refresh tokens, and session via the login and registration flows and the API client.

It also covers the **end-user experience**: **setting up and editing email digest preferences** (topics, cadence, content), **defining the outgoing mail** (what gets sent and how it reads), and choosing the **name users want to be addressed by**, wired to the API and worker.

---

## Projects in this folder

| Project | Role |
|--------|------|
| **Hermes.WebFrontend** | ASP.NET Core host: `Program.cs`, root `App.razor`, `Routes`, **MainLayout**, error/not-found pages, static assets under `wwwroot/` (global CSS, Swiss tokens). |
| **Hermes.WebFrontend.Client** | Blazor **WebAssembly** assembly: pages, layouts, components, `Program.cs` (DI, `HttpClient`, local storage), client `wwwroot/appsettings*.json`. |

The router loads routes from **both** assemblies; the default layout is **MainLayout** (full-viewport poster background + content slot for `@Body`).

---

## Prerequisites

- **.NET SDK** (solution targets .NET 10)
- **Hermes.Api** running (often `http://localhost:5165/` in docs) and a reachable **MySQL** database
- **CORS:** the API only allows configured origins (`Cors:AllowedOrigins` in `Hermes.Api`). For local Blazor debugging, add the actual frontend origin (e.g. `http://localhost:5269` from `Properties/launchSettings.json`).

---

## Configuration (client)

Files: `Hermes.WebFrontend.Client/wwwroot/appsettings.json` and optionally `appsettings.Development.json`.

| Key | Meaning |
|-----|---------|
| **`ApiBaseUrl`** | API base URL **with** trailing `/`, e.g. `http://localhost:5165/`. If empty, the client falls back to its own origin (`BaseAddress`). |
| **`Session:IdleTimeoutDays`** | Idle window for the client session (used with `AuthSessionService` / token handling). |

The scoped WASM `HttpClient` sets `BaseAddress` from this config. A separate **named** `HttpClient` without the auth handler is used for anonymous calls (e.g. refresh).

---

## Run locally

From `Hermes.WebFrontend/Hermes.WebFrontend`:

```bash
dotnet run
```

`Properties/launchSettings.json` profiles typically use **HTTPS** (`https://localhost:7016`) and **HTTP** (`http://localhost:5269`).

**Order:** start the API (and DB) first, then the frontend. If the browser console shows CORS errors, add the real frontend origin under `Hermes.Api` → `Cors:AllowedOrigins`.

---

## Routing and layouts

| Route | Page | Layout / notes |
|-------|------|----------------|
| `/` | `RootRedirect` | MainLayout; redirects to login or home depending on session. |
| `/login` | Login | **AuthLayout** (split form + `AuthSwissPoster`). |
| `/register` | Register | AuthLayout; `POST api/v1/users` then login, tokens in local storage, navigate to `/home`. |
| `/home` | Home | **AppHomeLayout** (top nav + main area); Swiss poster content. |
| `/news-settings`, `/news-settings/new` | News configuration | AppHomeLayout; list/edit via `NewsSettingsPanel` / `NewsSubscriptionCard`. |
| `/user-settings` | Profile | AppHomeLayout; name, email, password change (`PUT api/v1/users`). |

Error pages: server components under `Hermes.WebFrontend/Components/Pages/` (e.g. `/Error`, status-code reexecute to not-found).

---

## Authentication (overview)

- **`AuthTokenStore`**: access and refresh tokens in **Blazored.LocalStorage**; load/persist helpers.
- **`AuthMessageHandler`**: adds `Authorization: Bearer` on outgoing API requests for the scoped `HttpClient`.
- **`AuthSessionService`**: session checks, refresh, idle timeout; uses the **named** `HttpClient` without Bearer for `POST api/v1/auth/refresh`.
- **`GlobalAuthGuard`**: wired in `App.razor`; on navigation, public paths (`/`, `/login`, `/register`, `/Error`) vs. protected routes; redirect to `/login` when there is no token.
- **`AuthLogoutService`**: sign-out including API logout and clearing tokens locally.

Public API endpoints include registration, login, and refresh. Protected calls use the JWT from the store.

---

## UI / design

- **Design tokens:** `wwwroot/css/swiss-tokens.css` (colors, typography, spacing).
- **Global styles:** `app.css`, `swiss-hermes.css`, `hermes-app-pages.css`.
- **MainLayout:** full-viewport “hermes” poster (fixed background) with color animation on accent layers; content above.
- **Home:** extra “rail” with vertical type and the same accent color animation.
- **Login/Register:** `AuthSwissPoster` with animated panel background (same palette as the poster).
- **`prefers-reduced-motion`:** disables poster color animations.

Components include `HermesBrand`, `HermesTopNavigation`, `NewsSettingsPanel`, `NewsSubscriptionCard`.

---

## Notable client services

| Service | Purpose |
|---------|---------|
| `UserProfileRefreshNotifier` | Singleton: after saving profile, notify other views (e.g. reload home welcome line). |
| `NewsSubscriptionListCache` | Caches news lists to reduce redundant API calls. |

---

## Known limitations

- **Email verification** is not fully wired in the product UI (backend may already expose fields for it).

---

## Folder layout

```
Hermes.WebFrontend/
├── README.md                 ← this file
├── Hermes.WebFrontend/       ← server host
│   ├── Components/           App.razor, Routes, Layout/MainLayout, Pages (Error, NotFound)
│   ├── wwwroot/              global CSS, tokens
│   └── Program.cs
└── Hermes.WebFrontend.Client/
    ├── Components/           auth, news, navigation, …
    ├── Layout/               AuthLayout, AppHomeLayout
    ├── Pages/                Login, Register, Home, UserSettings, NewsSettings, RootRedirect
    ├── Services/             auth, news, user, …
    ├── wwwroot/appsettings*.json
    └── Program.cs
```

---

## Build

```bash
dotnet build Hermes.WebFrontend/Hermes.WebFrontend/Hermes.WebFrontend.csproj
```

The client assembly builds as a dependency. For CI, building the solution (`Hermes.slnx`) is usually enough.
