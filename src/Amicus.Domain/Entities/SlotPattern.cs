namespace Amicus.Domain.Entities;

/// <summary>
/// An admin-authored recurring rule: "this specialist sees students on Tuesdays
/// 14:00–18:00, in 30-minute slots".
///
/// <see cref="StartTime"/> and <see cref="EndTime"/> are LOCAL to the event's
/// <see cref="Event.TimeZoneId"/> — an admin thinks in wall-clock time, not UTC.
/// Expanding a pattern produces <see cref="Slot"/> rows; the pattern itself is
/// never booked.
/// </summary>
public class SlotPattern
{
    public Guid Id { get; set; }

    public Guid EventSpecialistId { get; set; }

    public EventSpecialist? EventSpecialist { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int SlotDurationMinutes { get; set; } = 30;

    /// <summary>Gap left between consecutive slots, so a session running over
    /// does not immediately collide with the next student.</summary>
    public int BreakMinutes { get; set; }
}
