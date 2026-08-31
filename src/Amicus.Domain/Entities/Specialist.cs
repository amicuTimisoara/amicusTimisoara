namespace Amicus.Domain.Entities;

/// <summary>
/// Someone giving advice at an event — a lawyer, a physician, an accountant, a
/// counsellor.
///
/// Specialists do not manage their own availability: an admin authors their
/// <see cref="SlotPattern"/>s. That is why <see cref="UserId"/> is nullable — a
/// specialist can be on the board without ever having an app account.
/// </summary>
public class Specialist
{
    public Guid Id { get; set; }

    /// <summary>Links to an identity user only if this specialist signs in.</summary>
    public Guid? UserId { get; set; }

    public required string FullName { get; set; }

    /// <summary>Shown to students, e.g. "Avocat", "Medic de familie", "Contabil".</summary>
    public required string Specialty { get; set; }

    public string? Bio { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<EventSpecialist> Events { get; set; } = [];
}
