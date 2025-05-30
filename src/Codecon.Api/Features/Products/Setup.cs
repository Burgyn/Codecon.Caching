using Codecon.Api.Data;
using Codecon.Api.Models;
using Delta;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Net.Http.Headers;
using ZiggyCreatures.Caching.Fusion;

namespace Codecon.Api.Features.Products;

public static class Setup
{
    public static IServiceCollection AddProducts(this WebApplicationBuilder builder)
    {
        // 👇 Output cache policies
        // builder.Services.AddOutputCache(options =>
        // {
        //     options.AddPolicy("Products",
        //         builder =>
        //             builder.Expire(TimeSpan.FromSeconds(50))
        //                 .Tag("products")
        //                 .AddNoCacheByRequestHeader());
        // });

        builder.Services
            .AddOutputCache() // 👈 Simply add the dependencies and use app.UseOutputCache() in Program.cs;
            .AddHttpContextAccessor()
            .AddResponseCaching(); // 👈 Add response caching services

        //👇 Add FusionCache services (as HybridCache) with Redis as second-level cache
        builder.Services
            .AddFusionCache()
            .AsHybridCache();

        return builder.Services;
    }

    public static IEndpointRouteBuilder MapProducts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithOpenApi()
            .WithTags("Products");

        group
            .MapProductsV1() // 👈 Without caching
            .MapProductsV2() // 👈 With response cache
            .MapProductsV3() // 👈 With output cache
            .MapProductsV4() // 👈 With hybrid cache
            .MapProductsV5() // 👈 With etag caching
            .MapProductsUpdate() // 👈 Edit endpoint
            .MapCacheClear(); // 👈 Clear cache endpoint
        return app;
    }

    private static IEndpointRouteBuilder MapProductsV1(this IEndpointRouteBuilder app)
    {
        //👇 Without caching
        app.MapGet("/v1", GetProductsByCategory)
            .WithName("GetProductsByCategory-v1")
            .WithDescription("Get products by category - without caching");
        return app;
    }

    private static IEndpointRouteBuilder MapProductsV2(this IEndpointRouteBuilder app)
    {
        //👇 With response caching
        // 👉 The simplest and most effective caching method
        // 👉 Uses HTTP header `Cache-Control`
        // 👉 Data is cached on the client side (browser)
        // 👉 UseResponseCaching() middleware for server-side caching
        // 👉 The disadvantage is the inability to reasonably invalidate
        // 👉 Limited use. Only GET, HEAD requests, without authorization, …
        // 👉 ⚠️ Cache-Control: public/private → controls if cache is shared or browser-only
        //   (e.g. Cache-Control: public/private, max-age=50)
        app.MapGet("/v2", GetProductsByCategoryWithResponseCache)
            .WithName("GetCachedProducts-v2")
            .WithDescription("Get products by category - with response caching");
        return app;
    }

    private static IEndpointRouteBuilder MapProductsV3(this IEndpointRouteBuilder app)
    {
        //👇 With output caching
        // 👉 More modern replacement for response caching since .NET 7
        // 👉 Data is cached on the server side
        // 👉 We have more control through built-in and custom policies
        // 👉 Cache invalidation using IOutputCacheStore
        // 👉 Invalidation based on tags
        // 👉 Simple .CacheOutput() and app.UseOutputCache();
        // 👉 Controllers -> [OutputCache]
        app.MapGet("/v3", GetProductsByCategory)
            .WithName("GetCachedProducts-v3")
            .WithDescription("Get products by category - with output caching")
            // .CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(50))) // 👈 Simple add policy
            .CacheOutput(policy =>
                policy
                    .Expire(TimeSpan.FromSeconds(50))
                    .Tag("products")
                    .AddNoCacheByRequestHeader());
        // .CacheOutput("Products") // 👈 Or use the predefined policy
        return app;
    }

    private static IEndpointRouteBuilder MapProductsV4(this IEndpointRouteBuilder app)
    {
        //👇 With hybrid cache
        // 👉 Hybrid cache unifies the API over IMemoryCache and IDistributedCache interfaces
        // 👉 Brings support for L1 and L2 cache
        // 👉 Allows tagging cache entries and invalidation based on tags
        //  ⚠️ Cannot invalidate yet. Currently possible only thanks to FusionCache
        // 👉 FusionCache -> OpenSource cache
        //   👉 Services.AddFusionCache().AsHybridCache()
        //   👉 🛡️ Cache Stampede, 💣 Fail-Safe, ⏱ Soft/Hard Timeouts,
        //   👉 ↩️ Auto-Recovery, 🔀 L1+L2, 📢 Backplane,
        //   👉 🦅 Eager Refresh, Ⓜ️ Microsoft HybridCache, …
        app.MapGet("/v4", GetProductsByCategoryWithHybridCache)
            .WithName("GetCachedProducts-v4")
            .WithDescription("Get products by category - with Hybrid Cache");

        return app;
    }

    private static IEndpointRouteBuilder MapProductsV5(this IEndpointRouteBuilder app)
    {
        //👇 With Delta ETag caching
        // 👉 ETag cache use 304 Not Modified response and headers ETag / If-None-Match
        // Client               Server
        //  │                    │
        //  ├── GET /item ─────▶ │
        //  │                    ├── 200 OK + ETag: "abc123"
        //  │ ◀──────────────────┘
        //  ├── GET /item
        //  │    If-None-Match: "abc123"
        //  │                    ├── 304 Not Modified
        //  │ ◀──────────────────┘
        // 🙋 "Frequency of updates to data is relatively low compared to reads"
        // 👉 The Delta framework uses DB change tracking
        //    (transaction log or row version in MS SQL, and track_commit_timestamp in Postgres).
        // 👇 Super easy to use:
        //   👉 Add the connection string to DI: services.AddScoped(_ => new NpgsqlConnection(connectionString));
        //   👉 Apply the Delta endpoint filter to your
        //      endpoint, endpoint group, or the entire application (as middleware) using .UseDelta();
        app.MapGet("/v5", GetProductsByCategory)
            .WithName("GetCachedProducts-v5")
            .WithDescription("Get products by category - with ETag (Delta)")
            .UseDelta(); // 👈 Use Delta middleware

        return app;
    }

    private static async Task<Results<Ok<IEnumerable<Product>>, BadRequest<string>>> GetProductsByCategory(
        [FromQuery] string? category,
        [FromServices] AppDbContext dbContext,
        [FromServices] ILogger<AppDbContext> logger,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return TypedResults.BadRequest("Category parameter is required");
        }

        logger.LogInformation("Fetching products in category '{Category}'", category);

        var products = await dbContext.Products
            .Where(p => p.Category.StartsWith(category))
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);

        logger.LogInformation("Found {Count} products in category '{Category}'", products.Count, category);

        return TypedResults.Ok(products.Take(100));
    }

    private static async Task<Results<Ok<IEnumerable<Product>>, BadRequest<string>>>
        GetProductsByCategoryWithResponseCache(
            [FromQuery] string? category,
            [FromServices] AppDbContext dbContext,
            [FromServices] ILogger<AppDbContext> logger,
            HttpResponse response,
            CancellationToken cancellationToken)
    {
        response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = TimeSpan.FromSeconds(50)
        };
        response.Headers[HeaderNames.Vary] = "Accept-Encoding";
        // In controller 👇
        // [HttpGet]
        // [ResponseCache(Duration = 20, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "Accept-Encoding" })]
        // public async Task<IActionResult> Get(string category) { ... }
        // 💁 Do not forget to add UseResponseCaching() in Program.cs
        return await GetProductsByCategory(category, dbContext, logger, response, cancellationToken);
    }

    private static async Task<Results<Ok<IEnumerable<Product>>, BadRequest<string>>>
        GetProductsByCategoryWithHybridCache(
            [FromQuery] string? category,
            [FromServices] AppDbContext dbContext,
            [FromServices] ILogger<AppDbContext> logger,
            [FromServices] HybridCache cache,
            HttpRequest request,
            HttpResponse response,
            CancellationToken cancellationToken)
    {
        // 👇 If the request contains a "no-cache" header, don't use HybridCache
        if (request.Headers.TryGetValue(HeaderNames.CacheControl, out var value) &&
            value.ToString().Contains("no-cache"))
        {
            return await GetProductsByCategory(category, dbContext, logger, response, cancellationToken);
        }

        logger.LogInformation("Fetching products from hybrid cache for category '{Category}'", category);

        // 👇 Use HybridCache to cache results
        return await cache.GetOrCreateAsync(
            $"products:{category}", // 👈 It isn't good practice to use the user input as a key. It's only for demo purpose.
            async (token) =>
                await GetProductsByCategory(category, dbContext, logger, response,
                    token), // 👈 Use factory method to get the data.
            tags: ["products"], // 👈 Tag entry
            cancellationToken: cancellationToken);
    }

    private static IEndpointRouteBuilder MapProductsUpdate(this IEndpointRouteBuilder app)
    {
        app.MapPut("/update/{id}", UpdateProduct)
            .WithName("UpdateProduct")
            .WithDescription("Update a product");

        return app;
    }

    private static IEndpointRouteBuilder MapCacheClear(this IEndpointRouteBuilder app)
    {
        app.MapPost("/clear-cache", ClearAllCache)
            .WithName("ClearCache")
            .WithDescription("Clear all product caches");

        return app;
    }

    private static async Task<Results<Ok<string>, BadRequest<string>>> ClearAllCache(
        [FromServices] IOutputCacheStore cacheStore,
        [FromServices] ILogger<AppDbContext> logger,
        [FromServices] HybridCache hybridCache,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Clearing all product caches");

        try
        {
            await EvictProductCaches(cacheStore, hybridCache, null, cancellationToken);
            return TypedResults.Ok("All product caches cleared successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing product caches");
            return TypedResults.BadRequest("Error clearing caches: " + ex.Message);
        }
    }

    private static async Task EvictProductCaches(
        IOutputCacheStore cacheStore,
        HybridCache hybridCache,
        int? productId,
        CancellationToken cancellationToken)
    {
        // 👇 Evict by general products tag
        await cacheStore.EvictByTagAsync("products", cancellationToken);

        // 👇 If a specific product ID is provided, also evict that product's tag
        if (productId.HasValue)
        {
            await cacheStore.EvictByTagAsync($"products:{productId}", cancellationToken);
        }

        // 👇 Evict HybridCache by tag
        await hybridCache.RemoveByTagAsync(["products"], cancellationToken: cancellationToken);
    }

    private static async Task<Results<Ok<Product>, NotFound, BadRequest<string>>> UpdateProduct(
        int id,
        [FromBody] UpdateProductRequest request,
        [FromServices] AppDbContext dbContext,
        [FromServices] ILogger<AppDbContext> logger,
        [FromServices] IOutputCacheStore cacheStore,
        [FromServices] HybridCache hybridCache,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Updating product with ID: {Id}", id);

        var product = await dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product == null)
        {
            logger.LogWarning("Product with ID {Id} not found", id);
            return TypedResults.NotFound();
        }

        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.Category = request.Category;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Product with ID {Id} updated successfully", id);
        }
        finally
        {
            await EvictProductCaches(cacheStore, hybridCache, id, cancellationToken);
        }

        return TypedResults.Ok(product);
    }

    public record UpdateProductRequest(
        string Name,
        string? Description,
        decimal Price,
        string Category);
}
