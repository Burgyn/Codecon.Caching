using Codecon.Api.Data;
using Codecon.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Codecon.Api.Features.Products;

public static class Setup
{
    public static IServiceCollection AddProducts(this IServiceCollection services)
    {
        // 👇 Output cache policies
        // services.AddOutputCache(options =>
        // {
        //     options.AddPolicy("Products", 
        //         builder => 
        //             builder.Expire(TimeSpan.FromSeconds(20))
        //                 .Tag("Products"));
        // });
        
        return services
            .AddMemoryCache()
            .AddOutputCache() // 👈 Simply add the dependencies and use app.UseOutputCache() in Program.cs; 
            .AddHttpContextAccessor();
    }

    public static IEndpointRouteBuilder MapProducts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithOpenApi()
            .WithTags("Products");

        group
            .MapProductsV1() // 👈 Without caching
            .MapProductsV2() // 👈 With response cache (memory cache)
            .MapProductsV3() // 👈 With output cache
            .MapProductsV4(); // 👈 With etag caching
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
        //👇 With response caching (memory cache)
        app.MapGet("/v2", GetProductsByCategoryWithResponseCache)
            .WithName("GetCachedProducts-v2")
            .WithDescription("Get products by category - with response caching (memory cache)");
        return app;
    }
    
    private static IEndpointRouteBuilder MapProductsV3(this IEndpointRouteBuilder app)
    {
        //👇 With output caching
        app.MapGet("/v3", GetProductsByCategory)
            .WithName("GetCachedProducts-v3")
            .WithDescription("Get products by category - with output caching")
            .CacheOutput(policy =>
                policy
                    .Expire(TimeSpan.FromSeconds(20))
                    .Tag("products"));
        return app;
    }
    
    private static IEndpointRouteBuilder MapProductsV4(this IEndpointRouteBuilder app)
    {
        //👇 With output caching ETag
        app.MapGet("/v4", GetProductsByCategoryETag)
            .WithName("GetCachedProducts-v4")
            .WithDescription("Get products by category - with output caching ETag")
            .CacheOutput(policy => policy
                .Expire(TimeSpan.FromSeconds(20))
                .Tag("Products"));
            //.CacheOutput("Products"); 👈 use defined policy

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
            .ToListAsync(cancellationToken);

        logger.LogInformation("Found {Count} products in category '{Category}'", products.Count, category);

        return TypedResults.Ok(products.Take(100));
    }

    private static async Task<Results<Ok<IEnumerable<Product>>, BadRequest<string>>> GetProductsByCategoryWithResponseCache(
        [FromQuery] string? category,
        [FromServices] AppDbContext dbContext,
        [FromServices] ILogger<AppDbContext> logger,
        [FromServices] IMemoryCache memoryCache,
        [FromServices] IHttpContextAccessor context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return TypedResults.BadRequest("Category parameter is required");
        }

        // Create a cache key based on the category
        var cacheKey = $"products_by_category_{category}";

        // Try to get from cache first
        if (memoryCache.TryGetValue(cacheKey, out List<Product> cachedProducts))
        {
            logger.LogInformation("Cache hit for category '{Category}'. Returning {Count} products from cache", 
                category, cachedProducts.Count);
            return TypedResults.Ok(cachedProducts.Take(100));
        }

        // Cache miss, fetch from database
        logger.LogInformation("Cache miss for category '{Category}'. Fetching from database", category);

        var products = await dbContext.Products
            .Where(p => p.Category.StartsWith(category))
            .ToListAsync(cancellationToken);

        logger.LogInformation("Found {Count} products in category '{Category}'", products.Count, category);

        // Store in cache for 20 seconds
        memoryCache.Set(cacheKey, products, TimeSpan.FromSeconds(20));

        return TypedResults.Ok(products.Take(100));
    }
    
    private static async Task<Results<Ok<IEnumerable<Product>>, BadRequest<string>>> GetProductsByCategoryETag(
        [FromQuery] string? category,
        [FromServices] AppDbContext dbContext,
        [FromServices] ILogger<AppDbContext> logger,
        [FromServices] IHttpContextAccessor context,
        CancellationToken cancellationToken)
    {
        if (context.HttpContext != null)
        {
            var etag = $"\"{Guid.NewGuid():n}\"";
            context.HttpContext.Response.Headers.ETag = etag;
        }

        return await GetProductsByCategory(category, dbContext, logger, context, cancellationToken);
    }
}