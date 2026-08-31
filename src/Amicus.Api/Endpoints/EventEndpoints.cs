using System.Security.Claims;
using Amicus.Api.Contracts;
using Amicus.Domain;
using Amicus.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Amicus.Api.Endpoints;

public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEvents(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/events").RequireAuthorization();

        group.MapGet("/", async (AmicusDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Events
                .Where(e => e.IsPublished)
                .OrderBy(e => e.StartsOn)
                .Select(e => new EventSummary(
                    e.Id, e.Slug, e.Name, e.StartsOn, e.EndsOn, e.TimeZoneId))
                .ToListAsync(ct)))
            .WithName("ListEvents")
            .WithSummary("Published events, soonest first.");

        group.MapGet("/{slug}", async (
            string slug, AmicusDbContext db, CancellationToken ct) =>
        {
            var detail = await db.Events
                .Where(e => e.Slug == slug && e.IsPublished)
                .Select(e => new EventDetail(
                    new EventSummary(e.Id, e.Slug, e.Name, e.StartsOn, e.EndsOn, e.TimeZoneId),
                    e.Specialists
                        .Where(es => es.Specialist!.IsActive)
                        .OrderBy(es => es.Specialist!.FullName)
                        .Select(es => new SpecialistSummary(
                            es.Id,
                            es.SpecialistId,
                            es.Specialist!.FullName,
                            es.Specialist.Specialty,
                            es.Specialist.Bio,
                            es.Location))
                        .ToList()))
                .FirstOrDefaultAsync(ct);

            return detail is null ? Results.NotFound() : Results.Ok(detail);
        })
            .WithName("GetEvent")
            .WithSummary("One published event and the specialists attending it.");

        group.MapGet("/{slug}/board", async (
            string slug, AmicusDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = user.Id();

            var @event = await db.Events
                .Where(e => e.Slug == slug && e.IsPublished)
                .Select(e => new { e.Id })
                .FirstOrDefaultAsync(ct);

            if (@event is null)
            {
                return Results.NotFound();
            }

            // Projected straight to the wire shape. There is no path here that can
            // load who holds a slot: only whether SOMEONE does, and whether it is
            // the caller. That is the privacy guarantee, enforced by the query.
            var rows = await db.Slots
                .Where(s => s.EventSpecialist!.EventId == @event.Id)
                .OrderBy(s => s.StartsAt)
                .Select(s => new
                {
                    s.EventSpecialistId,
                    Specialist = new SpecialistSummary(
                        s.EventSpecialist!.Id,
                        s.EventSpecialist.SpecialistId,
                        s.EventSpecialist.Specialist!.FullName,
                        s.EventSpecialist.Specialist.Specialty,
                        s.EventSpecialist.Specialist.Bio,
                        s.EventSpecialist.Location),
                    Slot = new BoardSlot(
                        s.Id,
                        s.StartsAt,
                        s.EndsAt,
                        !s.IsBlocked
                            && !s.Bookings.Any(b => b.Status != BookingStatus.Cancelled),
                        s.Bookings.Any(b =>
                            b.Status != BookingStatus.Cancelled && b.StudentUserId == userId)),
                })
                .ToListAsync(ct);

            var boards = rows
                .GroupBy(r => r.EventSpecialistId)
                .Select(g => new SpecialistBoard(
                    g.First().Specialist,
                    g.Select(r => r.Slot).ToList()))
                .OrderBy(b => b.Specialist.FullName)
                .ToList();

            return Results.Ok(boards);
        })
            .WithName("GetEventBoard")
            .WithSummary("The shared slot board: what is taken and when, never by whom.");

        return app;
    }
}
