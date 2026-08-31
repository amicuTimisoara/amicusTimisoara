using Amicus.Infrastructure;
using Amicus.Infrastructure.Identity;

namespace Amicus.Api.Setup;

/// <summary>
/// Identity wiring lives in the API rather than in Infrastructure on purpose:
/// <c>AddIdentityApiEndpoints</c> is part of the ASP.NET Core shared framework, and
/// pulling that into a class library would make the persistence layer depend on the
/// web stack.
/// </summary>
public static class IdentitySetup
{
    public static IServiceCollection AddAmicusIdentity(this IServiceCollection services)
    {
        services.AddAuthorization();

        services
            .AddIdentityApiEndpoints<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;

                // Students type these on a phone. Length carries far more real
                // strength than symbol classes, which mostly produce "Pa$$w0rd".
                options.Password.RequiredLength = 10;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireDigit = false;

                options.Lockout.MaxFailedAccessAttempts = 10;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AmicusDbContext>();

        return services;
    }
}
