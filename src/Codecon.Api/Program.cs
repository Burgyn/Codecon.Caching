using Asp.Versioning;
using Codecon.Api.Data;
using Codecon.Api.Features.Products;
using Microsoft.EntityFrameworkCore;
using Polly;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// Add database with retry logic
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
    });
});

// Add feature services
builder.Services.AddProducts();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    
    // Initialize the database with test data for development
    // Add retry logic for database initialization
    var retryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: 10,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (exception, timeSpan, retryCount, context) =>
            {
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                logger.LogWarning(exception, "Error connecting to PostgreSQL. Retrying in {RetryTimeSpan}. Attempt {RetryCount}", timeSpan, retryCount);
            });

    await retryPolicy.ExecuteAsync(async () => await DatabaseInitializer.InitializeAsync(app.Services));
}

app.UseHttpsRedirection();

// Map API endpoints
app.MapProducts();

app.Run();
