using System.Security.Claims;

namespace Amicus.Api.Endpoints;

public static class CurrentUser
{
    /// <summary>
    /// The signed-in user's id. Throws rather than returning null: every call site
    /// sits behind RequireAuthorization, so a missing subject is a bug in the
    /// pipeline, not a case to handle.
    /// </summary>
    public static Guid Id(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var id)
            ? id
            : throw new InvalidOperationException(
                "Authenticated principal carries no usable subject claim.");
    }
}
