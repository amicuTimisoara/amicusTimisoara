namespace Amicus.Domain.Entities;

/// <summary>
/// A bounded occasion that appointments belong to — a congress, a weekend, a
/// single advice day.
///
/// Everything is scoped to an event on purpose. Slots are generated only inside
/// <see cref="StartsOn"/>..<see cref="EndsOn"/>, so there is no open-ended
/// recurring timetable to maintain and no holiday exceptions to model: events
/// simply end.
/// </summary>
public class Event
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>URL-safe identifier the clients address, e.g. "congres-2026".</summary>
    public required string Slug { get; set; }

    public DateOnly StartsOn { get; set; }

    public DateOnly EndsOn { get; set; }

    /// <summary>
    /// IANA zone that <see cref="SlotPattern"/> times are expressed in. Slot
    /// instants themselves are stored in UTC; this is what they were generated
    /// from and what the clients render back in.
    /// </summary>
    public string TimeZoneId { get; set; } = "Europe/Bucharest";

    /// <summary>Students see nothing until an admin publishes the event.</summary>
    public bool IsPublished { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<EventSpecialist> Specialists { get; set; } = [];
}
