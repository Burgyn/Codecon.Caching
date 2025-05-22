using Codecon.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Codecon.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .HasKey(p => p.Id);

        modelBuilder.Entity<Product>()
            .Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        modelBuilder.Entity<Product>()
            .Property(p => p.Description)
            .HasMaxLength(500);

        modelBuilder.Entity<Product>()
            .Property(p => p.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Product>()
            .Property(p => p.Category)
            .IsRequired()
            .HasMaxLength(50);

        // Configure PostgreSQL-specific behaviors
        modelBuilder.Entity<Product>()
            .ToTable("Products", tb => tb.HasComment("Products table for the caching demo"));

        // Note: We're deliberately not creating an index on Category
        // as per requirements, to demonstrate caching benefits later
    }
} 