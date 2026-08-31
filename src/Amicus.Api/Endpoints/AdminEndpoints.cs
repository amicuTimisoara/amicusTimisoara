using Amicus.Api.Contracts;
using Amicus.Domain;
using Amicus.Domain.Entities;
using Amicus.Infrastructure;
using Amicus.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amicus.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin")
            .RequireAuthorization(policy => policy.RequireRole(AppRoles.Admin));

        group.MapPost("/events", async (
            [FromBody] CreateEventRequest request, AmicusDbContext db,
            TimeProvider clock, CancellationToken ct) =>
        {
            if (request.EndsOn < request.StartsOn)
            {
                return Results.BadRequest(new { error = "endsOn precedes startsOn." });
            }

            var timeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId)
                ? "Europe/Bucharest"
                : request.TimeZoneId;

            // Rejected here rather than at slot-generation time, which is when a bad
            // zone would otherwise surface — long after the admin left this screen.
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _))
            {
                return Results.BadRequest(new { error = $"Unknown time zone '{timeZoneId}'." });
            }

            var @event = new Event
            {
                Id = Guid.CreateVersion7(),
                Name = request.Name.Trim(),
                Slug = request.Slug.Trim().ToLowerInvariant(),
                StartsOn = request.StartsOn,
                EndsOn = request.EndsOn,
                TimeZoneId = timeZoneId,
                IsPublished = false,
                CreatedAt = clock.GetUtcNow(),
            };

            db.Events.Add(@event);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/events/{@event.Slug}", new EventSummary(
                @event.Id, @event.Slug, @event.Name,
                @event.StartsOn, @event.EndsOn, @event.TimeZoneId));
        })
            .WithSummary("Create an event. Unpublished, so students cannot see it yet.");

        group.MapPost("/events/{eventId:guid}/publish", async (
            Guid eventId, AmicusDbContext db, CancellationToken ct) =>
        {
            var @event = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);

            if (@event is null)
            {
                return Results.NotFound();
            }

            @event.IsPublished = true;
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
            .WithSummary("Make an event visible to students.");

        group.MapPost("/specialists", async (
            [FromBody] CreateSpecialistRequest request, AmicusDbContext db,
            TimeProvider clock, CancellationToken ct) =>
        {
            var specialist = new Specialist
            {
                Id = Guid.CreateVersion7(),
                FullName = request.FullName.Trim(),
                Specialty = request.Specialty.Trim(),
                Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim(),
                IsActive = true,
                CreatedAt = clock.GetUtcNow(),
            };

            db.Specialists.Add(specialist);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/admin/specialists/{specialist.Id}", specialist.Id);
        })
            .WithSummary("Add a specialist. No account is created — they do not need one.");

        group.MapPost("/events/{eventId:guid}/specialists", async (
            Guid eventId, [FromBody] AssignSpecialistRequest request,
            AmicusDbContext db, CancellationToken ct) =>
        {
            var exists = await db.Events.AnyAsync(e => e.Id == eventId, ct)
                && await db.Specialists.AnyAsync(s => s.Id == request.SpecialistId, ct);

            if (!exists)
            {
                return Results.NotFound(new { error = "No such event or specialist." });
            }

            if (await db.EventSpecialists.AnyAsync(
                    es => es.EventId == eventId && es.SpecialistId == request.SpecialistId, ct))
            {
                return Results.Conflict(
                    new { error = "That specialist is already assigned to this event." });
            }

            var assignment = new EventSpecialist
            {
                Id = Guid.CreateVersion7(),
                EventId = eventId,
                SpecialistId = request.SpecialistId,
                Location = string.IsNullOrWhiteSpace(request.Location)
                    ? null
                    : request.Location.Trim(),
            };

            db.EventSpecialists.Add(assignment);
            await db.SaveChangesAsync(ct);

            return Results.Created(
                $"/admin/event-specialists/{assignment.Id}", assignment.Id);
        })
            .WithSummary("Put a specialist on an event's roster.");

        group.MapPost("/event-specialists/{eventSpecialistId:guid}/patterns", async (
            Guid eventSpecialistId, [FromBody] CreateSlotPatternRequest request,
            AmicusDbContext db, CancellationToken ct) =>
        {
            var assignment = await db.EventSpecialists
                .Include(es => es.Event)
                .FirstOrDefaultAsync(es => es.Id == eventSpecialistId, ct);

            if (assignment?.Event is null)
            {
                return Results.NotFound();
            }

            var pattern = new SlotPattern
            {
                Id = Guid.CreateVersion7(),
                EventSpecialistId = eventSpecialistId,
                DayOfWeek = request.DayOfWeek,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                SlotDurationMinutes = request.SlotDurationMinutes,
                BreakMinutes = request.BreakMinutes,
            };

            // Validated by expanding it: the planner already owns every rule about
            // what a sane pattern is, so it is the single source of truth rather
            // than a second copy of the checks living here.
            try
            {
                SlotPlanner.Expand(assignment.Event, pattern);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            db.SlotPatterns.Add(pattern);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/admin/patterns/{pattern.Id}", pattern.Id);
        })
            .WithSummary("Assign a recurring availability rule to a specialist.");

        group.MapPost("/events/{eventId:guid}/generate-slots", async (
            Guid eventId, AmicusDbContext db, CancellationToken ct) =>
        {
            var @event = await db.Events
                .Include(e => e.Specialists).ThenInclude(es => es.Patterns)
                .FirstOrDefaultAsync(e => e.Id == eventId, ct);

            if (@event is null)
            {
                return Results.NotFound();
            }

            var created = 0;
            var alreadyPresent = 0;
            var removedStale = 0;

            foreach (var assignment in @event.Specialists)
            {
                var planned = SlotPlanner.ExpandAll(@event, assignment.Patterns);
                var plannedStarts = planned.Select(p => p.StartsAt).ToHashSet();

                var existing = await db.Slots
                    .Where(s => s.EventSpecialistId == assignment.Id)
                    .Select(s => new { s.Id, s.StartsAt, HasBookings = s.Bookings.Any() })
                    .ToListAsync(ct);

                var existingStarts = existing.Select(e => e.StartsAt).ToHashSet();

                foreach (var slot in planned)
                {
                    if (existingStarts.Contains(slot.StartsAt))
                    {
                        alreadyPresent++;
                        continue;
                    }

                    db.Slots.Add(new Slot
                    {
                        Id = Guid.CreateVersion7(),
                        EventSpecialistId = assignment.Id,
                        StartsAt = slot.StartsAt,
                        EndsAt = slot.EndsAt,
                    });

                    created++;
                }

                // An edited pattern leaves slots behind that it no longer produces.
                // Those are dropped ONLY if nobody ever booked them — a slot with any
                // booking history stays, so a student's record is never silently
                // deleted by an admin retiming the day.
                var stale = existing
                    .Where(e => !plannedStarts.Contains(e.StartsAt) && !e.HasBookings)
                    .Select(e => e.Id)
                    .ToList();

                if (stale.Count > 0)
                {
                    removedStale += await db.Slots
                        .Where(s => stale.Contains(s.Id))
                        .ExecuteDeleteAsync(ct);
                }
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(new GenerateSlotsResult(created, alreadyPresent, removedStale));
        })
            .WithSummary(
                "Expand every pattern into bookable slots. Safe to re-run: existing "
                + "slots are left alone and booked ones are never removed.");

        return app;
    }
}
