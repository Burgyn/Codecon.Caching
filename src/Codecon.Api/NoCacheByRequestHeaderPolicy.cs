using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Net.Http.Headers;

namespace Codecon.Api;

public class NoCacheByRequestHeaderPolicy : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        var headers = context.HttpContext.Request.Headers;

        if (headers.TryGetValue(HeaderNames.CacheControl, out var values) &&
            values.ToString().Contains("no-cache", StringComparison.OrdinalIgnoreCase))
        {
            context.AllowCacheLookup = false;
            context.AllowCacheStorage = false;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}

public static class NoCacheByRequestHeaderPolicyExtensions
{
    public static OutputCachePolicyBuilder AddNoCacheByRequestHeader(this OutputCachePolicyBuilder builder)
    {
        builder.AddPolicy<NoCacheByRequestHeaderPolicy>();
        return builder;
    }
}
