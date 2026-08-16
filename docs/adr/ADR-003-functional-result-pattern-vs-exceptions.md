# ADR-003: Functional Result Pattern (`FluentResults`) vs. RFC 7807 Exceptions

## Status
**Accepted**

## Context
In modern C# web applications, controlling operational flow with exceptions (e.g. `UserNotFoundException`, `InvalidCredentialsException`) incurs high performance overhead from stack trace allocations and hides expected domain branches from the compiler type system.

## Decision
We adopted a **Hybrid Error Handling Architecture**:
1. **Expected Business Failures (Result Pattern)**: All application services and inbound ports return `Result<T>` or `Result` via `FluentResults`. Errors are modeled as strongly typed `DomainError` subclasses (`UserNotFoundError`, `DuplicateEmailError`, `InvalidCurrentPasswordError`, `VerificationCodeMismatchError`, `TokenCompromisedError`).
2. **Controller Pattern Matching**: API controllers invoke `this.ToProblemResult(result.Errors.First())` to project `DomainError` types into RFC 7807 `ProblemDetails` HTTP responses with appropriate status codes (400, 401, 403, 404, 409).
3. **Exceptional System Failures (`IExceptionHandler`)**: True unexpected runtime exceptions (database connection drops, serialization corruptions, unhandled hardware faults) propagate to ASP.NET Core's `GlobalExceptionHandler` which logs the diagnostic trace and produces an RFC 7807 500 Internal Server Error without leaking internal stack traces.

## Consequences
- **Positive**:
  - High performance: zero stack trace generation on routine validation and authentication branching.
  - Compile-time safety: calling code is forced to handle success and failure paths explicitly.
  - Consistent RESTful API responses adhering to RFC 7807.
- **Negative**:
  - Requires developers to return `Result` objects and wrap values rather than throwing convenience exceptions.
