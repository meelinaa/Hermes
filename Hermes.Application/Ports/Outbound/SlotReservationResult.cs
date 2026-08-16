using Hermes.Domain.Entities;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Represents the outcome status of an atomic slot reservation attempt.
/// </summary>
public enum SlotReservationStatus
{
    /// <summary>The slot was successfully reserved via a new Pending log entry.</summary>
    Reserved,

    /// <summary>An existing stale Pending lease was successfully reclaimed via atomic CAS update.</summary>
    Reclaimed,

    /// <summary>The slot was already successfully sent in a previous attempt. Dispatch should be skipped.</summary>
    AlreadySent,

    /// <summary>An active in-flight worker lease is currently executing this slot. Dispatch should back off.</summary>
    ActiveLeaseInProgress
}

/// <summary>
/// Encapsulates the result and active <see cref="NotificationLog"/> of an atomic slot reservation attempt.
/// </summary>
/// <param name="Status">The status outcome of the reservation.</param>
/// <param name="Log">The associated notification log entity (if acquired).</param>
public sealed record SlotReservationResult(SlotReservationStatus Status, NotificationLog? Log)
{
    /// <summary>
    /// Gets a value indicating whether the worker successfully acquired or reclaimed the execution lease.
    /// </summary>
    public bool IsAcquired => Status is SlotReservationStatus.Reserved or SlotReservationStatus.Reclaimed;

    /// <summary>Factory method creating a new reservation outcome.</summary>
    public static SlotReservationResult NewReservation(NotificationLog log) => new(SlotReservationStatus.Reserved, log);

    /// <summary>Factory method creating a reclaimed lease outcome.</summary>
    public static SlotReservationResult Reclaimed(NotificationLog log) => new(SlotReservationStatus.Reclaimed, log);

    /// <summary>Factory method creating an already sent outcome.</summary>
    public static SlotReservationResult AlreadySent() => new(SlotReservationStatus.AlreadySent, null);

    /// <summary>Factory method creating an active lease in-progress outcome.</summary>
    public static SlotReservationResult ActiveLease() => new(SlotReservationStatus.ActiveLeaseInProgress, null);
}
