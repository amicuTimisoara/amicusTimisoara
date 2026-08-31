using Amicus.Domain;
using Amicus.Domain.Entities;

namespace Amicus.Domain.Tests;

public class SlotPlannerTests
{
    // Romania: EET (UTC+2) in winter, EEST (UTC+3) in summer. Clocks go forward
    // on the last Sunday of March at 03:00 local, and back on the last Sunday of
    // October at 04:00 local.
    private const string Bucharest = "Europe/Bucharest";

    private static Event EventOver(string startsOn, string endsOn) => new()
    {
        Name = "Test event",
        Slug = "test-event",
        StartsOn = DateOnly.Parse(startsOn),
        EndsOn = DateOnly.Parse(endsOn),
        TimeZoneId = Bucharest,
    };

    private static SlotPattern Pattern(
        DayOfWeek day, string start, string end, int minutes = 30, int breakMinutes = 0) => new()
        {
            DayOfWeek = day,
            StartTime = TimeOnly.Parse(start),
            EndTime = TimeOnly.Parse(end),
            SlotDurationMinutes = minutes,
            BreakMinutes = breakMinutes,
        };

    [Fact]
    public void Fills_the_window_with_back_to_back_slots()
    {
        // Tue 2026-09-01 only.
        var slots = SlotPlanner.Expand(
            EventOver("2026-09-01", "2026-09-01"),
            Pattern(DayOfWeek.Tuesday, "14:00", "16:00"));

        Assert.Equal(4, slots.Count);
        Assert.Equal(30, (slots[1].StartsAt - slots[0].StartsAt).TotalMinutes);
        Assert.All(slots, s => Assert.Equal(30, (s.EndsAt - s.StartsAt).TotalMinutes));
    }

    [Fact]
    public void Leaves_out_a_trailing_slot_that_would_overrun_the_window()
    {
        // 14:00-15:20 fits 14:00 and 14:30; a third would end 15:30, past the end.
        var slots = SlotPlanner.Expand(
            EventOver("2026-09-01", "2026-09-01"),
            Pattern(DayOfWeek.Tuesday, "14:00", "15:20"));

        Assert.Equal(2, slots.Count);
    }

    [Fact]
    public void Break_pushes_the_next_slot_out_without_shortening_it()
    {
        // 30-minute slots with a 15-minute break: 14:00, 14:45, 15:30 (ends 16:00).
        var slots = SlotPlanner.Expand(
            EventOver("2026-09-01", "2026-09-01"),
            Pattern(DayOfWeek.Tuesday, "14:00", "16:00", breakMinutes: 15));

        Assert.Equal(3, slots.Count);
        Assert.Equal(45, (slots[1].StartsAt - slots[0].StartsAt).TotalMinutes);
        Assert.All(slots, s => Assert.Equal(30, (s.EndsAt - s.StartsAt).TotalMinutes));
    }

    [Fact]
    public void Repeats_on_every_matching_weekday_and_ignores_the_others()
    {
        // 2026-09-01 is a Tuesday; the range covers three Tuesdays.
        var slots = SlotPlanner.Expand(
            EventOver("2026-09-01", "2026-09-16"),
            Pattern(DayOfWeek.Tuesday, "14:00", "15:00"));

        Assert.Equal(6, slots.Count);
        Assert.All(slots, s => Assert.Equal(
            DayOfWeek.Tuesday,
            TimeZoneInfo.ConvertTime(
                s.StartsAt, TimeZoneInfo.FindSystemTimeZoneById(Bucharest)).DayOfWeek));
    }

    [Fact]
    public void Produces_nothing_when_the_weekday_never_falls_in_range()
    {
        var slots = SlotPlanner.Expand(
            EventOver("2026-09-01", "2026-09-03"),
            Pattern(DayOfWeek.Sunday, "14:00", "16:00"));

        Assert.Empty(slots);
    }

    [Fact]
    public void Winter_wall_clock_converts_at_plus_two()
    {
        // 2026-01-06 is a Tuesday, EET.
        var slots = SlotPlanner.Expand(
            EventOver("2026-01-06", "2026-01-06"),
            Pattern(DayOfWeek.Tuesday, "14:00", "14:30"));

        Assert.Equal(
            new DateTimeOffset(2026, 1, 6, 12, 0, 0, TimeSpan.Zero),
            Assert.Single(slots).StartsAt);
    }

    [Fact]
    public void Summer_wall_clock_converts_at_plus_three()
    {
        // 2026-07-07 is a Tuesday, EEST. Same 14:00 local, an hour earlier in UTC.
        var slots = SlotPlanner.Expand(
            EventOver("2026-07-07", "2026-07-07"),
            Pattern(DayOfWeek.Tuesday, "14:00", "14:30"));

        Assert.Equal(
            new DateTimeOffset(2026, 7, 7, 11, 0, 0, TimeSpan.Zero),
            Assert.Single(slots).StartsAt);
    }

    [Fact]
    public void Skips_start_times_inside_the_spring_forward_gap()
    {
        // 2026-03-29: 03:00 -> 04:00, so local 03:00 and 03:30 never happen.
        var slots = SlotPlanner.Expand(
            EventOver("2026-03-29", "2026-03-29"),
            Pattern(DayOfWeek.Sunday, "02:00", "05:00"));

        // Candidates 02:00 02:30 03:00 03:30 04:00 04:30; the middle two vanish.
        Assert.Equal(4, slots.Count);

        var zone = TimeZoneInfo.FindSystemTimeZoneById(Bucharest);
        var localHours = slots
            .Select(s => TimeZoneInfo.ConvertTime(s.StartsAt, zone).TimeOfDay)
            .ToList();

        Assert.DoesNotContain(new TimeSpan(3, 0, 0), localHours);
        Assert.DoesNotContain(new TimeSpan(3, 30, 0), localHours);

        // Real duration is preserved even across the jump.
        Assert.All(slots, s => Assert.Equal(30, (s.EndsAt - s.StartsAt).TotalMinutes));
    }

    [Fact]
    public void A_slot_ending_exactly_on_the_gap_boundary_survives()
    {
        // 02:30-03:00 local is a real 30 minutes: local 03:00 is that instant,
        // merely relabelled 04:00. Rejecting it would lose a bookable slot.
        var slots = SlotPlanner.Expand(
            EventOver("2026-03-29", "2026-03-29"),
            Pattern(DayOfWeek.Sunday, "02:30", "03:00"));

        var slot = Assert.Single(slots);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.Zero), slot.StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero), slot.EndsAt);
    }

    [Fact]
    public void Ambiguous_autumn_hour_resolves_to_the_first_pass()
    {
        // 2026-10-25: 04:00 -> 03:00, so local 03:00 happens twice. A student
        // reading the timetable turns up for the first one (still on EEST, +3).
        var slots = SlotPlanner.Expand(
            EventOver("2026-10-25", "2026-10-25"),
            Pattern(DayOfWeek.Sunday, "03:00", "03:30"));

        Assert.Equal(
            new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero),
            Assert.Single(slots).StartsAt);
    }

    [Fact]
    public void ExpandAll_drops_the_later_of_two_overlapping_patterns()
    {
        var @event = EventOver("2026-09-01", "2026-09-01");
        var patterns = new[]
        {
            Pattern(DayOfWeek.Tuesday, "14:00", "16:00"),  // 14:00 14:30 15:00 15:30
            Pattern(DayOfWeek.Tuesday, "15:00", "16:00"),  // 15:00 15:30 — both collide
        };

        var slots = SlotPlanner.ExpandAll(@event, patterns);

        Assert.Equal(4, slots.Count);
        Assert.Equal(slots.Count, slots.Select(s => s.StartsAt).Distinct().Count());
        for (var i = 1; i < slots.Count; i++)
        {
            Assert.True(slots[i].StartsAt >= slots[i - 1].EndsAt, "slots must not overlap");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Rejects_a_non_positive_duration(int minutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SlotPlanner.Expand(
            EventOver("2026-09-01", "2026-09-01"),
            Pattern(DayOfWeek.Tuesday, "14:00", "16:00", minutes)));
    }

    [Fact]
    public void Rejects_a_window_that_ends_before_it_starts()
    {
        Assert.Throws<ArgumentException>(() => SlotPlanner.Expand(
            EventOver("2026-09-01", "2026-09-01"),
            Pattern(DayOfWeek.Tuesday, "16:00", "14:00")));
    }

    [Fact]
    public void Rejects_an_event_whose_end_precedes_its_start()
    {
        Assert.Throws<ArgumentException>(() => SlotPlanner.Expand(
            EventOver("2026-09-10", "2026-09-01"),
            Pattern(DayOfWeek.Tuesday, "14:00", "16:00")));
    }
}
