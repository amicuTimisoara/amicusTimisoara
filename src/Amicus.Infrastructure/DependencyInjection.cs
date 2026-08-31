using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Amicus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAmicusPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Postgres is not configured. See README.md.");

        services.AddDbContext<AmicusDbContext>(options => options
            .UseNpgsql(connectionString)
            // Postgres convention. Without this, EF emits "StartsAt" and every
            // hand-written query needs quoting.
            .UseSnakeCaseNamingConvention());

        return services;
    }
}
