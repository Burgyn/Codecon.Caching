using Asp.Versioning;
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
        return services;
    }

    public static IEndpointRouteBuilder MapProducts(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/products")
            .WithApiVersionSet(versionSet)
            .WithOpenApi()
            .WithTags("Products");

        // Map V1 endpoints
        group.MapGroup("v{version:apiVersion}")
            .MapProductsV1();

        return app;
    }

    private static IEndpointRouteBuilder MapProductsV1(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", GetProductsByCategory)
            .WithName("GetProductsByCategory")
            .WithDescription("Get products by category")
            .WithOpenApi();
        return app;
    }
    
    private static async Task<Results<Ok<List<Product>>, BadRequest<string>>> GetProductsByCategory(
        [FromQuery] string? category,
        [FromServices] AppDbContext dbContext,
        [FromServices] ILogger<AppDbContext> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return TypedResults.BadRequest("Category parameter is required");
        }

        logger.LogInformation("Fetching products in category '{Category}'", category);
        
        var products = await dbContext.Products
            .Where(p => p.Category == category)
            .ToListAsync(cancellationToken);

        logger.LogInformation("Found {Count} products in category '{Category}'", products.Count, category);

        return TypedResults.Ok(products);
    }
} 