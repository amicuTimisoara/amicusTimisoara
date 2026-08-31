namespace Amicus.Domain.Entities;

/// <summary>
/// A single bookable instant, materialised from a <see cref="SlotPattern"/> — or
/// added ad hoc, hence the nullable pattern link.
///
/// Slots are stored rather than computed on the fly for one reason that matters:
/// it lets the DATABASE own "this slot is taken", as a unique index over
/// <see cref="Booking.SlotId"/>. Two students tapping the same slot in the same
/// second is a race that an application-level "is it free?" check cannot
/// reliably win.
/// </summary>
public class Slot
{
    public Guid Id { get; set; }

    public Guid EventSpecialistId { get; set; }

    public EventSpecialist? EventSpecialist { get; set; }

    public Guid? SlotPatternId { get; set; }

    public SlotPattern? SlotPattern { get; set; }

    /// <summary>UTC instant. Clients render it in the event's zone.</summary>
    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    /// <summary>Lets an admin pull a slot off the board without deleting it,
    /// which would orphan the audit trail of a cancelled booking.</summary>
    public bool IsBlocked { get; set; }

    public ICollection<Booking> Bookings { get; set; } = [];
}
