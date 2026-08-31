namespace Amicus.Api.Contracts;

public sealed record EventSummary(
    Guid Id, string Slug, string Name, DateOnly StartsOn, DateOnly EndsOn, string TimeZoneId);

public sealed record SpecialistSummary(
    Guid EventSpecialistId, Guid SpecialistId, string FullName, string Specialty,
    string? Bio, string? Location);

public sealed record EventDetail(
    EventSummary Event, IReadOnlyList<SpecialistSummary> Specialists);

/// <summary>
/// One cell of the shared board.
///
/// Carries availability and nothing else. <c>IsMine</c> is the caller's own
/// booking, which they are entitled to know; there is deliberately no field for
/// who holds a slot, because the board is visible to every student and some of
/// these specialists are physicians and lawyers.
/// </summary>
public sealed record BoardSlot(
    Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsAvailable, bool IsMine);

public sealed record SpecialistBoard(
    SpecialistSummary Specialist, IReadOnlyList<BoardSlot> Slots);

public sealed record CreateBookingRequest(Guid SlotId, string? Topic);

public sealed record BookingDetail(
    Guid Id, Guid SlotId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status,
    string? Topic, string CheckInCode, string EventSlug, string EventName,
    string SpecialistName, string Specialty, string? Location);

public sealed record CreateEventRequest(
    string Name, string Slug, DateOnly StartsOn, DateOnly EndsOn, string? TimeZoneId);

public sealed record CreateSpecialistRequest(string FullName, string Specialty, string? Bio);

public sealed record AssignSpecialistRequest(Guid SpecialistId, string? Location);

public sealed record CreateSlotPatternRequest(
    DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime,
    int SlotDurationMinutes, int BreakMinutes);

public sealed record GenerateSlotsResult(int Created, int AlreadyPresent, int RemovedStale);

public sealed record CheckInRequest(string Code);

public sealed record CheckInResult(
    Guid BookingId, DateTimeOffset StartsAt, string SpecialistName, string Status);
