namespace Amicus.Domain.Entities;

/// <summary>
/// A specialist's appearance at one event. Separate from <see cref="Specialist"/>
/// so the same person can return for later events without being re-created, and
/// so per-appearance details (where they sit) do not leak onto their profile.
/// </summary>
public class EventSpecialist
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Event? Event { get; set; }

    public Guid SpecialistId { get; set; }

    public Specialist? Specialist { get; set; }

    /// <summary>Where the student physically goes, e.g. "Sala 2".</summary>
    public string? Location { get; set; }

    public ICollection<SlotPattern> Patterns { get; set; } = [];

    public ICollection<Slot> Slots { get; set; } = [];
}
