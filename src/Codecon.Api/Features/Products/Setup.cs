using Codecon.Api.Data;
using Codecon.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Net.Http.Headers;

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
            .AddHttpContextAccessor()
            .AddResponseCaching(); // 👈 Add response caching services
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
            .MapProductsV5(); // 👈 With etag caching
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
        app.MapGet("/v2", GetProductsByCategoryWithResponseCache)
            .WithName("GetCachedProducts-v2")
            .WithDescription("Get products by category - with response caching");
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
    
    private static IEndpointRouteBuilder MapProductsV5(this IEndpointRouteBuilder app)
    {
        //👇 With output caching ETag
        app.MapGet("/v5", GetProductsByCategoryETag)
            .WithName("GetCachedProducts-v5")
            .WithDescription("Get products by category - with ETag");

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

    private static async Task<Results<Ok<IEnumerable<Product>>, BadRequest<string>>> 
        GetProductsByCategoryWithResponseCache(
            [FromQuery] string? category,
            [FromServices] AppDbContext dbContext,
            [FromServices] ILogger<AppDbContext> logger,
            [FromServices] IMemoryCache memoryCache,
            [FromServices] IHttpContextAccessor context,
            CancellationToken cancellationToken)
    {
        if (context.HttpContext is not null)
        {
            context.HttpContext.Response.GetTypedHeaders().CacheControl = new ()
            {
                Public = true,
                MaxAge = TimeSpan.FromSeconds(20)
            };
            context.HttpContext.Response.Headers[HeaderNames.Vary] = "Accept-Encoding, category";
            // In controller 👇
            // [HttpGet]
            // [ResponseCache(Duration = 20, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "category" })]
            // public async Task<IActionResult> Get(string category) { ... }
            // 💁 Do not forget to add UseResponseCaching() in Program.cs
        }

        return await GetProductsByCategory(category, dbContext, logger, context, cancellationToken);
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
            var etag = $"\"{category}\"";
            context.HttpContext.Response.Headers.ETag = etag;
        }

        return await GetProductsByCategory(category, dbContext, logger, context, cancellationToken);
    }
}