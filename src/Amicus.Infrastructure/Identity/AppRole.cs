using Microsoft.AspNetCore.Identity;

namespace Amicus.Infrastructure.Identity;

public class AppRole : IdentityRole<Guid>
{
}

/// <summary>Role names, as constants so a typo is a compile error.</summary>
public static class AppRoles
{
    public const string Student = "Student";
    public const string Specialist = "Specialist";
    public const string Admin = "Admin";

    public static readonly string[] All = [Student, Specialist, Admin];
}
