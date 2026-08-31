using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Amicus.Api.Setup;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    /// <summary>Everything, per client IP.</summary>
    public int GlobalPermitsPerMinute { get; set; } = 600;

    /// <summary>
    /// Sign-in and registration, per client IP. Much tighter than the global
    /// limit: these are the endpoints worth guessing passwords against, and the
    /// ones worth spamming to create junk accounts.
    /// </summary>
    public int AuthPermitsPerMinute { get; set; } = 30;
}

public static class RateLimitSetup
{
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddAmicusRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitOptions.SectionName)
            .Get<RateLimitOptions>() ?? new RateLimitOptions();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Tells a well-behaved client when to come back instead of hammering.
            limiter.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context => RateLimitPartition.GetFixedWindowLimiter(
                    ClientKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.GlobalPermitsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                    }));

            limiter.AddPolicy(AuthPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.AuthPermitsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                    }));
        });

        return services;
    }

    // Falls back to a constant rather than throwing when there is no remote IP,
    // which happens for in-process test requests: one shared bucket is the safe
    // reading, not no limit at all.
    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
