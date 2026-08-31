namespace Amicus.Domain.Entities;

/// <summary>
/// A student holding a slot.
///
/// Privacy: the slot board is shared with every student so nobody double-books,
/// but it exposes only whether a slot is taken — never this row. Some
/// specialists are physicians or lawyers, so "who is seeing whom" is readable by
/// the student themselves, that student's specialist, and admins. Nobody else.
/// </summary>
public class Booking
{
    public Guid Id { get; set; }

    public Guid SlotId { get; set; }

    public Slot? Slot { get; set; }

    public Guid StudentUserId { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Booked;

    /// <summary>
    /// What the student wants to discuss, in their own words. Deliberately short
    /// and optional: this is an advice appointment, not a medical or legal
    /// record. The less we store, the less there is to protect.
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    /// Opaque token behind the student's QR code. Random and per-booking, so a
    /// leaked code identifies nobody and grants nothing beyond one check-in.
    /// </summary>
    public required string CheckInCode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public DateTimeOffset? CheckedInAt { get; set; }
}
