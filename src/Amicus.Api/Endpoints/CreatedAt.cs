namespace Amicus.Api.Endpoints;

public static class CreatedAt
{
    /// <summary>
    /// <c>Results.Created</c> with a literal path emits that path verbatim, ignoring
    /// <c>PathBase</c>. Behind nginx, which serves this app under <c>/amicus/</c> and
    /// strips the prefix before proxying, that produces a Location header pointing
    /// one level up — at a 404. Prefixing PathBase makes the header correct whether
    /// the app is mounted at the root or under a sub-path.
    /// </summary>
    public static IResult Path(HttpContext context, string path, object? value) =>
        Results.Created(context.Request.PathBase.Add(path).Value, value);
}
