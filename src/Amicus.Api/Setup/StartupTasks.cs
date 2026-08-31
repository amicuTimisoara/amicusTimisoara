using Amicus.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Amicus.Api.Setup;

public static class StartupTasks
{
    public static async Task RunAsync(
        IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new AppRole { Name = role });
            }
        }

        // How the first admin comes to exist: register normally, put the address in
        // Bootstrap:AdminEmails, restart. Promoting an existing account rather than
        // creating one means no default credentials ship anywhere.
        var adminEmails = configuration.GetSection("Bootstrap:AdminEmails").Get<string[]>() ?? [];

        if (adminEmails.Length == 0)
        {
            return;
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        foreach (var email in adminEmails)
        {
            var user = await users.FindByEmailAsync(email);

            if (user is null)
            {
                logger.LogWarning(
                    "Bootstrap:AdminEmails names {Email}, which has no account yet. "
                    + "Register it, then restart to grant Admin.", email);
                continue;
            }

            if (!await users.IsInRoleAsync(user, AppRoles.Admin))
            {
                await users.AddToRoleAsync(user, AppRoles.Admin);
                logger.LogInformation("Granted Admin to {Email}.", email);
            }
        }
    }
}
