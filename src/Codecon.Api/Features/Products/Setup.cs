using Codecon.Api.Data;
using Codecon.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            .AddOutputCache() // 👈 Simply add the dependencies and use app.UseOutputCache() in Program.cs; 
            .AddHttpContextAccessor();
    }

    public static IEndpointRouteBuilder MapProducts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithOpenApi()
            .WithTags("Products");

        group
            .MapProductsV1() //  👈 Without caching
            .MapProductsV2() //  👈 With output cache
            .MapProductsV3(); // 👈 With etag caching
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
        //👇 With output caching
        app.MapGet("/v2", GetProductsByCategory)
            .WithName("GetCachedProducts-v2")
            .WithDescription("Get products by category - with output caching")
            .CacheOutput(policy =>
                policy
                    .Expire(TimeSpan.FromSeconds(20))
                    .Tag("products"));
        return app;
    }
    
    private static IEndpointRouteBuilder MapProductsV3(this IEndpointRouteBuilder app)
    {
        //👇 With output caching ETag
        app.MapGet("/v3", GetProductsByCategoryETag)
            .WithName("GetCachedProducts-v3")
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