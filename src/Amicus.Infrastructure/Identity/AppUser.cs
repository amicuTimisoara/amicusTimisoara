using Microsoft.AspNetCore.Identity;

namespace Amicus.Infrastructure.Identity;

/// <summary>
/// Credentials and profile for anyone who signs in.
///
/// ASP.NET Core Identity is used rather than a hand-rolled user table because it
/// gives both halves of what we need out of the box: local email + password, and
/// external logins (Google) through the same account via AspNetUserLogins.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
