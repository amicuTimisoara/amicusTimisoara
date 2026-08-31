using Amicus.Api.Setup;
using Amicus.Infrastructure;
using Amicus.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAmicusPersistence(builder.Configuration);
builder.Services.AddAmicusIdentity();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<AmicusDbContext>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// /register, /login, /refresh, /manage/info — email + password, bearer tokens.
// Google sign-in is the next increment; see README.
app.MapGroup("/auth").MapIdentityApi<AppUser>();

// Liveness AND readiness: it round-trips the database, so a green /health means
// the API can actually serve, not merely that the process is up.
app.MapHealthChecks("/health");

await EnsureRolesExistAsync(app.Services);

app.Run();

static async Task EnsureRolesExistAsync(IServiceProvider services)
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
}
