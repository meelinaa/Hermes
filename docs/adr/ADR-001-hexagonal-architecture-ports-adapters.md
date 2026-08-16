# ADR-001: Hexagonal Architecture (Ports & Adapters) Isolation

## Status
**Accepted**

## Context
Hermes is an enterprise-grade notification platform requiring high maintainability, independent testability, and decoupled integration with external providers (MySQL, Redis, MailHog, NewsData.io, Hangfire). Traditional layered N-Tier architectures often introduce leaky database abstractions (e.g. `DbContext` in controllers) and tight coupling to specific infrastructure packages.

## Decision
We adopted **Hexagonal Architecture (Ports and Adapters)** with Domain-Driven Design (DDD) tactical patterns:
1. **Core Purity**: The `Hermes.Domain` layer contains zero external dependencies (no Entity Framework, no ASP.NET, no Redis). Aggregates enforce invariants and emit Domain Events.
2. **Ports**: `Hermes.Application` declares *Inbound Ports* (Use Case interfaces called by API controllers/Workers) and *Outbound Ports* (Repository/Storage interfaces fulfilled by adapters).
3. **Adapters**: `Hermes.Infrastructure`, `Hermes.Api`, and `Hermes.Worker` reside in the outer hexagonal ring and plug into ports via Dependency Injection.
4. **CI/CD Architecture Guardrails**: Automated architecture rules (`NetArchTest.Rules`) execute in unit test suites to prevent illegal layer dependencies at build time.

## Consequences
- **Positive**:
  - Independent unit testing with ephemeral mocks without booting databases or HTTP servers.
  - Flexibility to replace storage providers or external APIs without impacting core business rules.
  - Zero domain leakage into HTTP or ORM layers.
- **Negative**:
  - Requires additional DTO mappings and segregated interface declarations (ISP).
