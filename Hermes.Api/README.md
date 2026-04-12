# Hermes API

REST API for user management, JWT-based authentication, personalized news configuration, and notification logs. JSON property names use **camelCase**. In **Development**, OpenAPI metadata is exposed (see [OpenAPI](#openapi-and-documentation)).

## Authentication

- **Anonymous** endpoints: user registration, login, and token refresh.
- **Protected** endpoints: send a JWT in the header: `Authorization: Bearer <accessToken>`.
- Access tokens are short-lived; use **refresh** to obtain a new pair without re-entering credentials.
- Refresh tokens are opaque strings returned once by login/refresh; only a hash is stored server-side.

Configuration for signing and validation lives under the `Jwt` section (see `appsettings*.json`). Production secrets should be supplied via environment variables (e.g. `Jwt__SigningKey`), not committed to source control.

---

## Endpoints overview

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/api/v1/auth/login` | No | Login; returns access + refresh tokens |
| `POST` | `/api/v1/auth/refresh` | No | Rotate refresh token for new access + refresh |
| `POST` | `/api/v1/auth/logout` | Yes | Revoke one refresh session or all sessions for the user |
| `POST` | `/api/v1/users` | No | Register a new user |
| `PUT` | `/api/v1/users` | Yes | Update user (caller must match user id) |
| `GET` | `/api/v1/users/{id}` | Yes | Get user by id |
| `GET` | `/api/v1/users/by-email/{email}` | Yes | Get user by email (URL-encode `@` as `%40`) |
| `DELETE` | `/api/v1/users/{id}` | Yes | Delete user |
| `GET` | `/api/v1/users/news/{userId}/list` | Yes | List all news rows for a user |
| `GET` | `/api/v1/users/news/userId={userId}/newsId={newsId}` | Yes | Get one news row |
| `POST` | `/api/v1/users/news` | Yes | Create news configuration |
| `PUT` | `/api/v1/users/news` | Yes | Update news (body must include `id`) |
| `DELETE` | `/api/v1/users/news/userId={userId}/newsId={newsId}` | Yes | Delete one news row |
| `DELETE` | `/api/v1/users/news/userId={userId}/delete/all` | Yes | Delete all news for a user |
| `POST` | `/api/v1/users/{userId}/notification-logs` | Yes | Append a notification log entry |

**Health (no auth):** `GET /health/live` (liveness), `GET /health/ready` (readiness, includes database).

---

## Errors

- Validation failures (FluentValidation) return **400** with `application/problem+json` (`ValidationProblemDetails`).
- Domain-specific failures are mapped to **404**, **403**, **409**, etc. via the global exception handler (see `Hosting/ApiApplicationPipelineExtensions.cs`).
- Unexpected errors return **500** with a generic problem body (no exception message in production-oriented responses).

---

## JSON request and response shapes

The sections below show typical payloads. **Authoritative property lists** are the C# types in the referenced files (controllers include additional XML examples where noted).

### Auth

**Login** — request type: [`LoginRequest`](../Hermes.Domain/Models/LoginRequest.cs) (namespace `Hermes.Application.Models`).

```json
{
  "nameOrEmail": "user@example.com",
  "password": "your-plain-password"
}
```

**Login** — success response (anonymous object from `AuthController`; not a named DTO):

```json
{
  "success": true,
  "userId": 1,
  "accessToken": "<jwt>",
  "tokenType": "Bearer",
  "expiresAt": "2026-03-29T12:00:00.0000000Z",
  "refreshToken": "<opaque-refresh-token>",
  "refreshTokenExpiresAt": "2026-04-28T12:00:00.0000000+00:00"
}
```

**Refresh** — request: [`RefreshRequest`](../Hermes.Application/Models/RefreshRequest.cs).

```json
{
  "refreshToken": "<opaque-refresh-token>"
}
```

**Refresh** — success response:

```json
{
  "success": true,
  "accessToken": "<jwt>",
  "tokenType": "Bearer",
  "expiresAt": "2026-03-29T12:15:00.0000000Z",
  "refreshToken": "<new-opaque-refresh-token>",
  "refreshTokenExpiresAt": "2026-04-28T12:15:00.0000000+00:00"
}
```

**Logout** — optional body: [`LogoutRequest`](../Hermes.Application/Models/LogoutRequest.cs). Requires `Authorization: Bearer`.

```json
{}
```

Revoke only the current refresh session:

```json
{
  "refreshToken": "<opaque-refresh-token>"
}
```

Success: **204 No Content**.

*More inline examples:* [`Controllers/AuthController.cs`](Controllers/AuthController.cs) (`<remarks>` on `Login`).

---

### Users

Entity shape for register/update: [`User`](../Hermes.Domain/Entities/User.cs). Response DTO for lookups/register: [`UserScope`](../Hermes.Domain/DTOs/UserScope.cs).

**Register** (`POST /api/v1/users`) — example (password is sent in `passwordHash` and hashed server-side):

```json
{
  "id": 0,
  "name": "Max Mustermann",
  "email": "max@example.com",
  "passwordHash": "plain-password-here",
  "isEmailVerified": false,
  "twoFactorCode": null,
  "twoFactorExpiry": null
}
```

**Update** (`PUT /api/v1/users`):

```json
{
  "id": 1,
  "name": "Max Mustermann",
  "email": "max@example.com",
  "passwordHash": "only-if-changing-password",
  "isEmailVerified": true,
  "twoFactorCode": null,
  "twoFactorExpiry": null
}
```

**Get user** — success body is `UserScope`:

```json
{
  "name": "Max Mustermann",
  "email": "max@example.com",
  "userId": 1
}
```

*More inline examples:* [`Controllers/UsersController.cs`](Controllers/UsersController.cs) (`SetNewUser`, `UpdateUser`).

---

### News

Entity: [`News`](../Hermes.Domain/Entities/News.cs). Enums: [`NewsCategory`](../Hermes.Domain/Enums/NewsCategory.cs), [`Language`](../Hermes.Domain/Enums/Language.cs), [`Country`](../Hermes.Domain/Enums/Country.cs), [`Weekdays`](../Hermes.Domain/Enums/Weekdays.cs).

The API uses `System.Text.Json` with **string enums** (`JsonStringEnumConverter`), so enum fields appear as enum member names in JSON (e.g. `"Technology"`, `"Monday"`).

**Create** (`POST /api/v1/users/news`) — example:

```json
{
  "id": 0,
  "userId": 0,
  "keywords": ["markets", "climate"],
  "category": ["Business", "Environment"],
  "languages": ["English", "German"],
  "countries": ["Germany", "Austria"],
  "sendOnWeekdays": ["Monday", "Wednesday", "Friday"],
  "sendAtTimes": ["08:00:00", "18:30:00"]
}
```

Omit `userId` or set `0` to use the authenticated user’s id.

**Create** — success body [`NewsScope`](../Hermes.Domain/DTOs/NewsScope.cs):

```json
{
  "userId": 1,
  "newsId": 42
}
```

**Update** (`PUT /api/v1/users/news`) — same shape as create but **`id` must be set** to the existing news row (validated via FluentValidation).

**List / get** — response is an array or single [`News`](../Hermes.Domain/Entities/News.cs) entity.

*Path notes:* list uses `/api/v1/users/news/{userId}/list`. Single-item routes use literal segments, e.g. `GET`/`DELETE` `…/userId=1/newsId=5`.

---

### Notification logs

Entity: [`NotificationLog`](../Hermes.Domain/Entities/NotificationLog.cs). Status/channel enums: [`NotificationStatus`](../Hermes.Domain/Enums/NotificationStatus.cs), [`DeliveryChannel`](../Hermes.Domain/Enums/DeliveryChannel.cs).

**Create** (`POST /api/v1/users/{userId}/notification-logs`):

```json
{
  "id": 0,
  "userId": 0,
  "sentAt": "2026-03-29T13:00:00Z",
  "status": "Pending",
  "channel": "Email",
  "errorMessage": null,
  "retryCount": 0,
  "nextRetryAt": null
}
```

`userId` in the body should be `0` or match `{userId}` in the URL.

*More inline notes:* [`Controllers/NotificationLogsController.cs`](Controllers/NotificationLogsController.cs).

---

## OpenAPI and documentation

In the **Development** environment, OpenAPI is mapped for discovery tooling. Run the API with `ASPNETCORE_ENVIRONMENT=Development` and open **`GET /openapi/v1.json`** (default document) in a browser or import it into an HTTP client.

Controller XML comments (`<summary>`, `<remarks>`) document individual routes and are the best place to keep examples aligned with code—especially [`AuthController`](Controllers/AuthController.cs) and [`UsersController`](Controllers/UsersController.cs).

---

## Related configuration files

| Topic | File |
|--------|------|
| DI, JWT, CORS, DB, health, timeouts | [`Hosting/ApiServiceCollectionExtensions.cs`](Hosting/ApiServiceCollectionExtensions.cs) |
| JWT validation rules | [`Hosting/JwtAuthenticationExtensions.cs`](Hosting/JwtAuthenticationExtensions.cs) |
| Middleware order, exception mapping | [`Hosting/ApiApplicationPipelineExtensions.cs`](Hosting/ApiApplicationPipelineExtensions.cs) |
| Base settings | `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json` |
