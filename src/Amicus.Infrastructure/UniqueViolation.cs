using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Amicus.Infrastructure;

/// <summary>
/// Translates a Postgres unique-violation into something the API layer can branch
/// on, so endpoints do not need a Npgsql reference just to tell "someone beat me
/// to it" apart from a real failure.
/// </summary>
public static class UniqueViolation
{
    private const string UniqueViolationSqlState = "23505";

    /// <summary>
    /// True when <paramref name="exception"/> is a unique violation on
    /// <paramref name="constraintName"/> specifically. Matching the constraint by
    /// name matters: a bare 23505 check would swallow an unrelated collision — a
    /// duplicate check-in code, say — and report it as a taken slot.
    /// </summary>
    public static bool On(Exception exception, string constraintName) =>
        exception is DbUpdateException { InnerException: PostgresException postgres }
        && postgres.SqlState == UniqueViolationSqlState
        && postgres.ConstraintName == constraintName;

    /// <summary>The partial unique index that makes a slot bookable exactly once.</summary>
    public const string LiveBookingPerSlot = "ux_booking_live_slot";
}
