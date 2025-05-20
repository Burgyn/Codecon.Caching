using AutoBogus;
using Codecon.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Codecon.Api.Data;

public static class DatabaseInitializer
{
    private static readonly string[] Categories = 
    {
        "Electronics", "Clothing", "Home & Kitchen", "Books", "Sports", 
        "Toys", "Beauty", "Automotive", "Health", "Garden", "Furniture",
        "Jewelry", "Office", "Food", "Tools", "Baby", "Pet Supplies"
    };
    
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        logger.LogInformation("Ensuring database is created");
        await dbContext.Database.EnsureCreatedAsync();

        if (!await dbContext.Products.AnyAsync())
        {
            logger.LogInformation("Database is empty. Seeding with test data");
            
            // Configure AutoBogus
            var faker = new AutoFaker<Product>()
                .RuleFor(p => p.Id, f => 0) // Let EF handle IDs
                .RuleFor(p => p.Name, f => f.Commerce.ProductName())
                .RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
                .RuleFor(p => p.Price, f => Math.Round(f.Random.Decimal(1, 2000), 2))
                .RuleFor(p => p.Category, f => f.PickRandom(Categories));

            // Generate 150,000 products as requested
            const int batchSize = 5000;
            const int totalProducts = 850_000;
            
            for (int i = 0; i < totalProducts; i += batchSize)
            {
                logger.LogInformation("Generating products {Start} to {End}", i, Math.Min(i + batchSize, totalProducts));
                var products = faker.Generate(Math.Min(batchSize, totalProducts - i));
                
                await dbContext.Products.AddRangeAsync(products);
                await dbContext.SaveChangesAsync();
            }
            
            logger.LogInformation("Finished seeding database with {Count} products", totalProducts);
        }
        else
        {
            var count = await dbContext.Products.CountAsync();
            logger.LogInformation("Database already contains {Count} products", count);
        }
    }
} 