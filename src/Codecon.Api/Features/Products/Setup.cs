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
        // 👉 Najjednoduchšia a najefektívnejšia metóda kešovania
        // 👉 Využíva HTTP header `Cache-Control`
        // 👉 Dáta sa kešujú u klienta (browser)
        // 👉 UseResponseCaching() middleware pre kešovanie na strane servera
        // 👉 Nevýhodou je nemožnosť rozumného invalidovania
        // 👉 Obmedzené použitie. Len GET, HEAD request, bez autorizácie, …
        app.MapGet("/v2", GetProductsByCategoryWithResponseCache)
            .WithName("GetCachedProducts-v2")
            .WithDescription("Get products by category - with response caching");
        return app;
    }

    private static IEndpointRouteBuilder MapProductsV3(this IEndpointRouteBuilder app)
    {
        //👇 With output caching
        // 👉 Modernejšia náhrada za response caching od .NET 7
        // 👉 Dáta sa kešujú na strane servera
        // 👉 Máme to viac pod kontrolou pomocou vstavaných a vlastných policy
        // 👉 Invalidácia cache pomocou IOutputCacheStore
        // 👉 Invalidácia na základe tagov
        // 👉 Jednoduché .CacheOutput() a app.UseOutputCache();
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
        // 👉 Hybrid cache zjednocuje API nad IMemoryCache a IDistributedCache rozhraniami
        // 👉 Prináša podporu pre L1 a L2 keš
        // 👉 Umožňuje tagovať záznamy v keši a jej invalidáciu na základe tagov
        //  ⚠️ Invalidovať ešte nedokáže. Aktuálne možné len vďaka FusionCache
        // 👉 FusionCache -> OpenSource cache
        //   👉 Services.AddFusionCache().AsHybridCache()
        //   👉 🛡️ Cache Stampede, 💣 Fail-Safe, 📢 Backplane,
        //   👉 ↩️ Auto-Recovery, ⏱ Soft/Hard Timeouts, 🔀 L1+L2,
        //   👉 🦅 Eager Refresh, Ⓜ️ Microsoft HybridCache, …
        app.MapGet("/v4", GetProductsByCategoryWithHybridCache)
            .WithName("GetCachedProducts-v4")
            .WithDescription("Get products by category - with Hybrid Cache");

        return app;
    }

    private static async Task<Results<Ok<IEnumerable<Product>>, BadRequest<string>>> GetProductsByCategory(
        [FromQuery] string? category,
        [FromServices] AppDbContext dbContext,
        [FromServices] ILogger<AppDbContext> logger,
        [FromServices] IHttpContextAccessor context,
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
            [FromServices] IHttpContextAccessor context,
            CancellationToken cancellationToken)
    {
        if (context.HttpContext is not null)
        {
            context.HttpContext.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromSeconds(50)
            };
            context.HttpContext.Response.Headers[HeaderNames.Vary] = "Accept-Encoding";
            // In controller 👇
            // [HttpGet]
            // [ResponseCache(Duration = 20, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "Accept-Encoding" })]
            // public async Task<IActionResult> Get(string category) { ... }
            // 💁 Do not forget to add UseResponseCaching() in Program.cs
        }

        return await GetProductsByCategory(category, dbContext, logger, context, cancellationToken);
    }

    private static async Task<Results<Ok<IEnumerable<Product>>, BadRequest<string>>>
        GetProductsByCategoryWithHybridCache(
            [FromQuery] string? category,
            [FromServices] AppDbContext dbContext,
            [FromServices] ILogger<AppDbContext> logger,
            [FromServices] IHttpContextAccessor context,
            [FromServices] HybridCache cache,
            HttpRequest request,
            CancellationToken cancellationToken)
    {
        // 👇 If the request contains a "no-cache" header, don't use HybridCache
        if (request.Headers.TryGetValue(HeaderNames.CacheControl, out var value) &&
            value.ToString().Contains("no-cache"))
        {
            return await GetProductsByCategory(category, dbContext, logger, context, cancellationToken);
        }

        logger.LogInformation("Fetching products from hybrid cache for category '{Category}'", category);

        // 👇 Use HybridCache to cache results
        return await cache.GetOrCreateAsync(
            $"products:{category}", // 👈 It isn't good practice to use the user input as a key. It's only for demo purpose.
            async (token) => await GetProductsByCategory(category, dbContext, logger, context, token), // 👈 Use factory method to get the data.
            tags: ["products"], // 👈 Tag entry
            cancellationToken: cancellationToken);
    }

    private static IEndpointRouteBuilder MapProductsV5(this IEndpointRouteBuilder app)
    {
        //👇 With Delta ETag caching
        app.MapGet("/v5", GetProductsByCategory)
            .WithName("GetCachedProducts-v5")
            .WithDescription("Get products by category - with ETag (Delta)")
            .UseDelta(); // 👈 Use Delta middleware

        return app;
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
            await EvictProductCaches(cacheStore, null, cancellationToken);
            // 👇 Evict HybridCache by tag
            await hybridCache.RemoveByTagAsync(["products"], cancellationToken: cancellationToken);
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
    }

    private static async Task<Results<Ok<Product>, NotFound, BadRequest<string>>> UpdateProduct(
        int id,
        [FromBody] UpdateProductRequest request,
        [FromServices] AppDbContext dbContext,
        [FromServices] ILogger<AppDbContext> logger,
        [FromServices] IOutputCacheStore cacheStore,
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
            await EvictProductCaches(cacheStore, id, cancellationToken);
        }

        return TypedResults.Ok(product);
    }

    public record UpdateProductRequest(
        string Name,
        string? Description,
        decimal Price,
        string Category);
}
