# Hermes API

REST API for user management, JWT-based authentication, personalized news configuration, and notification logs. JSON property names use **camelCase**. All HTTP routes are prefixed with **`/api/v1/`** (see [API versioning](#api-versioning)). In **Development**, OpenAPI metadata is exposed (see [OpenAPI](#openapi-and-documentation)).

## API versioning

- **Path versioning:** Every resource URL starts with **`/api/v1/`**. The `v1` segment is the API revision exposed to clients.
- **Breaking changes:** When URLs or response contracts change incompatibly, introduce **`/api/v2/`** (or the next major segment) and keep **`v1`** until consumers migrate; document deprecation in release notes.
- **Non-breaking changes:** New optional JSON fields, new endpoints under the same `v1` prefix, or tightened validation may ship without a new version when clients remain compatible.

---

## Authentication

- **Anonymous** endpoints: user registration, login, and token refresh.
- **Protected** endpoints: send a JWT in the header: `Authorization: Bearer <accessToken>`.
- Access tokens are short-lived; use **refresh** to obtain a new pair without re-entering credentials.
- Refresh tokens are opaque strings returned once by login/refresh; only a hash is stored server-side.

Configuration for signing and validation lives under the `Jwt` section (see `appsettings*.json`). Production secrets should be supplied via environment variables (e.g. `Jwt__SigningKey`), not committed to source control.

---

## Endpoints overview


| Method   | Path                                                 | Auth | Description                                                                                           |
| -------- | ---------------------------------------------------- | ---- | ----------------------------------------------------------------------------------------------------- |
| `POST`   | `/api/v1/auth/login`                                 | No   | Login; returns access + refresh tokens                                                                |
| `POST`   | `/api/v1/auth/refresh`                               | No   | Rotate refresh token for new access + refresh                                                         |
| `POST`   | `/api/v1/auth/logout`                                | Yes  | Revoke one refresh session or all sessions for the user                                               |
| `POST`   | `/api/v1/users`                                      | No   | Register a new user                                                                                   |
| `PUT`    | `/api/v1/users`                                      | Yes  | Update user (caller must match user id)                                                               |
| `POST`   | `/api/v1/users/{id}/verify`                          | Yes  | Queue verification e-mail for the authenticated user id (ownership checked)                            |
| `POST`   | `/api/v1/users/verify/code`                          | Yes  | Submit six-digit e-mail verification code (`userId` + `code` in body)                                 |
| `GET`    | `/api/v1/users/{id}`                                 | Yes  | Get user by id                                                                                        |
| `GET`    | `/api/v1/users/by-email/{email}`                     | Yes  | Get user by email (URL-encode `@` as `%40`)                                                           |
| `DELETE` | `/api/v1/users/{id}`                                 | Yes  | Delete user                                                                                           |
| `GET`    | `/api/v1/users/{userId}/news`                     | Yes  | Paged list (`page`, `pageSize`, `afterId`, `sort`, `q`, `category`) — see OpenAPI / notes below          |
| `GET`    | `/api/v1/users/{userId}/news/{newsId}`          | Yes  | Get one news row                                                                                      |
| `POST`   | `/api/v1/users/news`                             | Yes  | Create news configuration                                                                             |
| `PUT`    | `/api/v1/users/news`                             | Yes  | Update news (body must include `id`)                                                                  |
| `DELETE` | `/api/v1/users/{userId}/news/{newsId}`          | Yes  | Delete one news row                                                                                   |
| `DELETE` | `/api/v1/users/{userId}/news/all`               | Yes  | Delete all news for a user                                                                            |
| `POST`   | `/api/v1/users/{userId}/notification-logs`           | Yes  | Append a notification log entry                                                                       |


**Health (no auth):** `GET /health/live` (liveness), `GET /health/ready` (readiness, includes database).

---

## Errors

- Validation failures (FluentValidation) return **400** with `application/problem+json` (`ValidationProblemDetails`).
- Domain-specific failures are mapped to **404**, **403**, **409**, **400**, etc. via the global exception handler (see `Hosting/ApiApplicationPipelineExtensions.cs`).
- Unexpected errors return **500** with a generic problem body (no exception message in production-oriented responses).

**Typed problems (RFC 7807 `type`)**: clients can branch on `type` in the JSON body:


| Situation                                                                | HTTP    | `type` (constant: `Hermes.Domain.HermesProblemTypes`) |
| ------------------------------------------------------------------------ | ------- | ----------------------------------------------------- |
| Password change: `currentPassword` does not match the stored BCrypt hash | **400** | `https://hermes.dev/problems/wrong-current-password`  |
| Verification code wrong or expired                                       | **400** | *(no stable `type`; use status + `title`/`detail`)*   |


For wrong-current-password responses, `**detail`** carries the user-facing message; `**title**` is a short summary.

---

## JSON request and response shapes

The sections below show typical payloads. **Authoritative property lists** are the C# types in the referenced files (controllers include additional XML examples where noted).

### Auth

**Login**: request type `[LoginRequest](../Hermes.Application/DTOs/Login/LoginRequest.cs)` (namespace `Hermes.Application.DTOs.Login`).

```json
{
  "nameOrEmail": "user@example.com",
  "password": "your-plain-password"
}
```

**Login**: success response (anonymous object from `AuthController`; not a named DTO):

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

**Refresh**: request `[RefreshRequest](../Hermes.Application/DTOs/RefreshRequest.cs)`.

```json
{
  "refreshToken": "<opaque-refresh-token>"
}
```

**Refresh**: success response:

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

**Logout**: optional body `[LogoutRequest](../Hermes.Application/DTOs/LogoutRequest.cs)`. Requires `Authorization: Bearer`.

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

*More inline examples:* `[Controllers/AuthController.cs](Controllers/AuthController.cs)` (`<remarks>` on `Login`).

---

### Users

**Register** body is `[RegisterUserRequest](../Hermes.Hermes.Application/DTOs/User/RegisterUserRequest.cs)` (`name`, `email`, `password`; extra JSON properties are ignored). **Profile update** uses `[UserProfileUpdateRequest](../Hermes.Application/DTOs/User/UserProfileUpdateRequest.cs)` (`newPassword` / `currentPassword`, not the full `User` JSON). Success responses use `[UserResponse](../Hermes.Application/DTOs/User/UserResponse.cs)` for register, profile GET, profile PUT, and successful verification-code submission. Queued verification mail: `[SendVerificationMailResponse](../Hermes.Application/DTOs/User/SendVerificationMailResponse.cs)`.

**Register** (`POST /api/v1/users`): example (`password` is hashed server-side with BCrypt):

```json
{
  "name": "Max Mustermann",
  "email": "max@example.com",
  "password": "plain-password-here"
}
```

**Update** (`PUT /api/v1/users`): body type `[UserProfileUpdateRequest](../Hermes.Application/DTOs/User/UserProfileUpdateRequest.cs)`. Success body is **`UserResponse`** (current profile after save). Omit `newPassword` (or send empty) to keep the existing password. When `newPassword` is set, `**currentPassword`** is required; the API verifies it with **BCrypt** against the stored hash before persisting the new hash.

```json
{
  "id": 1,
  "name": "Max Mustermann",
  "email": "max@example.com",
  "newPassword": null,
  "currentPassword": null
}
```

Password change example:

```json
{
  "id": 1,
  "name": "Max Mustermann",
  "email": "max@example.com",
  "newPassword": "New_Secret_1!",
  "currentPassword": "Old_Secret_1!"
}
```

If the **e-mail address changes**, persistence resets `**isEmailVerified`** to `false` until the user completes verification again.

**Get user**: success body is **`UserResponse`**:

```json
{
  "name": "Max Mustermann",
  "email": "max@example.com",
  "userId": 1,
  "isEmailVerified": true
}
```

**E-mail verification**: queue mail for account id (same auth rules as other user routes):

`POST /api/v1/users/1/verify` → **200** with **`SendVerificationMailResponse`** `{ "userId": 1, "email": "max@example.com" }` (implementation queues the message with a time-limited code). The caller must be authorized to access the user id.

Confirm code: body `[UserVerificationCodeRequest](../Hermes.Application/DTOs/User/UserVerificationCodeRequest.cs)`:

```json
{
  "userId": 1,
  "code": 123456
}
```

→ **200** with **`UserResponse`** when the code matches and is not expired (includes updated `isEmailVerified`). **400** when the code is wrong or expired (problem body without a stable `type`).

*More inline examples:* `[Controllers/UsersController.cs](Controllers/UsersController.cs)` (`SetNewUser`, `UpdateUser`).

---

### News

Entity: `[News](../Hermes.Domain/Entities/News.cs)`. Enums: `[NewsCategory](../Hermes.Domain/Enums/NewsCategory.cs)`, `[Language](../Hermes.Domain/Enums/Language.cs)`, `[Country](../Hermes.Domain/Enums/Country.cs)`, `[Weekdays](../Hermes.Domain/Enums/Weekdays.cs)`.

The API uses `System.Text.Json` with **string enums** (`JsonStringEnumConverter`), so enum fields appear as enum member names in JSON (e.g. `"Technology"`, `"Monday"`).

**Create** (`POST /api/v1/users/news`): example (owner from JWT — omit `userId`/`id`):

```json
{
  "keywords": ["markets", "climate"],
  "category": ["Business", "Environment"],
  "languages": ["English", "German"],
  "countries": ["Germany", "Austria"],
  "sendOnWeekdays": ["Monday", "Wednesday", "Friday"],
  "sendAtTimes": ["08:00:00", "18:30:00"]
}
```

**Create**: success body [`CreateNewsResponse`](../Hermes.Application/DTOs/News/CreateNewsResponse.cs):

```json
{
  "userId": 1,
  "newsId": 42
}
```

**Update** (`PUT /api/v1/users/news`): same editable fields as create plus required `id`; owner remains the JWT user (no `userId` in JSON).

**List / get**: response items are [`NewsResponse`](../Hermes.Application/DTOs/News/NewsResponse.cs) (API projection of persisted `[News](../Hermes.Domain/Entities/News.cs)` rows).

*Path notes:* news collection is **`GET /api/v1/users/{userId}/news`** with optional query **`page`**, **`pageSize`** (capped by `Pagination:MaxPageSize`), **`afterId`** (cursor; ascending id only; incompatible with **`sort=-id`**), **`sort`** (`id` or `-id`), **`q`** (keyword substring, max 200 chars), **`category`** (`NewsCategory`). Response: **`items`** plus **`totalCount`**, **`totalPages`** (offset mode), **`hasNextPage`**, **`nextAfterId`** (cursor). A single row is **`GET|DELETE /api/v1/users/{userId}/news/{newsId}`**; bulk delete is **`DELETE /api/v1/users/{userId}/news/all`**. Create/update remain **`POST|PUT /api/v1/users/news`** (owner from JWT).

---

### Notification logs

Entity: `[NotificationLog](../Hermes.Domain/Entities/NotificationLog.cs)`. Status/channel enums: `[NotificationStatus](../Hermes.Domain/Enums/NotificationStatus.cs)`, `[DeliveryChannel](../Hermes.Domain/Enums/DeliveryChannel.cs)`.

**Create** (`POST /api/v1/users/{userId}/notification-logs`): body omits `userId` (route + bearer scope the row):

```json
{
  "newsId": null,
  "sentAt": "2026-03-29T13:00:00Z",
  "status": "Pending",
  "channel": "Email",
  "errorMessage": null,
  "retryCount": 0,
  "nextRetryAt": null
}
```

Success body: [`NotificationLogResponse`](../Hermes.Application/DTOs/NotificationLogs/NotificationLogResponse.cs).

*More inline notes:* `[Controllers/NotificationLogsController.cs](Controllers/NotificationLogsController.cs)`.

---

## OpenAPI and documentation

In the **Development** environment, OpenAPI is mapped for discovery tooling. Run the API with `ASPNETCORE_ENVIRONMENT=Development` and open `**GET /openapi/v1.json*`* (default document) in a browser or import it into an HTTP client.

Controller XML comments (`<summary>`, `<remarks>`) document individual routes and are the best place to keep examples aligned with code, especially `[AuthController](Controllers/AuthController.cs)` and `[UsersController](Controllers/UsersController.cs)`.

---

## Related configuration files


| Topic                               | File                                                                                         |
| ----------------------------------- | -------------------------------------------------------------------------------------------- |
| DI, JWT, CORS, DB, health, timeouts | `[Hosting/ApiServiceCollectionExtensions.cs](Hosting/ApiServiceCollectionExtensions.cs)`     |
| JWT validation rules                | `[Hosting/JwtAuthenticationExtensions.cs](Hosting/JwtAuthenticationExtensions.cs)`           |
| Middleware order, exception mapping | `[Hosting/ApiApplicationPipelineExtensions.cs](Hosting/ApiApplicationPipelineExtensions.cs)` |
| Base settings                       | `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json`            |


---

## Automated tests

Automated coverage lives in `**Hermes.UnitTests**` (fast, no Docker) and `**Hermes.IntegrationTests**` (trait `**Integration=Docker**`; **Testcontainers** MySQL + `**WebApplicationFactory`**).

### `Hermes.UnitTests` (selected areas relevant to this API)


| Area                              | Examples                                                                          |
| --------------------------------- | --------------------------------------------------------------------------------- |
| Auth / JWT                        | `AuthTokenServiceTests`, `JwtTokenIssuerTests`, `RefreshTokenHasherTests`         |
| Users / news                      | `UserServiceTests`, `NewsServiceTests`, `UpdateNewsRequestValidatorTests`                 |
| Verification mail pipeline        | `VerificationDigestServiceTests`                                                  |
| HTTP / controllers                | `ControllerUserExtensionsTests`                                                   |
| Notification / digest persistence | `HermesDbContextTests` (notification send window), `NewsletterDigestServiceTests` |

**Blazor WASM (bUnit):** `Hermes.WebFrontend.Client.Tests` — lightweight component checks (for example `HermesBrand`).


### `Hermes.IntegrationTests`


| Suite                 | What it covers                                                                                                                                                                                                                                                         |
| --------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Health**            | `HealthProbeIntegrationTests`: `/health/live`, `/health/ready`, DB probe behaviour                                                                                                                                                                                    |
|                       | `ReadinessProbeFailureIntegrationTests`: readiness when MySQL stops                                                                                                                                                                                                   |
| **Auth**              | `AuthIntegrationTests`: login, refresh + replay, **logout** (single session, revoke all, unknown targeted refresh → **401**), credential validation, JWT bearer rejection (`UsersController` as probe), malformed/expired/forged tokens                                       |
|                       | `AuthDtoContractIntegrationTests`: login/refresh JSON **camelCase** fields expected by the SPA; refresh response must omit `userId`                                                                                                                                         |
|                       | `AuthRefreshConcurrencyIntegrationTests`: parallel refresh with the same token — exactly one **200**, one failure                                                                                                                                                          |
| **Users**             | `UsersCrudIntegrationTests`: anonymous register (duplicate email → **409 Conflict**), profile GET (by id / by email), update, **password change success**, wrong `currentPassword` → **400** + `type`, delete + GET **404**, cross-account **403**, **401**/ **400** samples |
|                       | `UsersEmailVerificationIntegrationTests`: `POST …/users/{id}/verify` and `POST …/users/verify/code` (success → **`UserResponse`** / `isEmailVerified`, wrong code / expired → **400**)                                                          |
| **News**              | `NewsCrudIntegrationTests`: create contract (`userId` / `newsId` camelCase), create/list/get/update/delete, cross-user **403**, missing-news **404**, invalid JSON / binding **400**, **401** paths                                                                                                                    |
| **Notification logs** | `NotificationLogsIntegrationTests`: `POST …/notification-logs` happy path, extraneous body `userId` ignored, cross-user **403**, **401**/ malformed bearer                                                                                                              |


From the **repository root**:

```bash
dotnet test Hermes.slnx
```

Docker-backed tests only:

```bash
dotnet test Hermes.slnx --filter "Integration=Docker"
```

