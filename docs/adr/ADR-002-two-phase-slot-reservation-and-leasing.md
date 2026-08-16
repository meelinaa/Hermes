# ADR-002: Two-Phase Slot Reservation, Leases, and Distributed Idempotency

## Status
**Accepted**

## Context
When scheduling recurring newsletter digests across multiple background worker replicas, race conditions could cause duplicate emails to be delivered to users if jobs are enqueued concurrently or re-attempted following transient network failures.

## Decision
We implemented a **Two-Phase Slot Reservation & Optimistic Leasing Protocol** backed by relational ACID constraints rather than distributed Redis locks:
1. **Slot Reservation**: The `notification_logs` table enforces a composite unique database index `UX_notification_logs_slot_reservation` over `(UserId, NewsId, Channel, ScheduledSlotUtc)`.
2. **Atomic Lease Granting**: When a worker processes a scheduled digest, it attempts to atomically insert a reservation record in `Pending` state (`TryReserveSlotAsync`). If a slot collision occurs (HTTP 409 / Duplicate Key), the duplicate execution is cleanly discarded.
3. **Lease Heartbeat & Reaper**: Worker nodes obtain a time-bounded lease. If a worker crashes mid-execution, a recurring Reaper worker detects expired leases and safely resets the job state for controlled retry.

## Consequences
- **Positive**:
  - Guaranteed exactly-once email delivery per scheduled slot without distributed two-phase commit overhead.
  - Relational auditability: `notification_logs` doubles as delivery log and concurrency barrier.
- **Negative**:
  - Requires database write operations on every digest schedule dispatch.
