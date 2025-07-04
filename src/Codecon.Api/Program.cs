using Codecon.Api.Data;
using Codecon.Api.Features.Products;
using Microsoft.EntityFrameworkCore;
using Polly;
using Scalar.AspNetCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddResponseCompression();

builder.Services.AddHttpLogging();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        b => b
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Add database with retry logic
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(50),
            errorCodesToAdd: null);
    });
});

builder.Services.AddScoped(_ => new NpgsqlConnection(connectionString));

// Add feature services
builder.AddProducts();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    await InitializeDatabaseAsync(app);
}

app.UseCors("AllowAll");
app.UseHttpLogging();
app.UseOutputCache(); //👈 Add Output cache middleware
// app.UseResponseCaching(); //👈 Add response caching middleware
app.UseResponseCompression();

//👇 Map API endpoints
app.MapProducts();

app.Run();
return;

static async Task InitializeDatabaseAsync(WebApplication app)
{
    var retryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: 10,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (exception, timeSpan, retryCount, context) =>
            {
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                logger.LogWarning(exception,
                    "Error connecting to PostgreSQL. Retrying in {RetryTimeSpan}. Attempt {RetryCount}", timeSpan,
                    retryCount);
            });

    await retryPolicy.ExecuteAsync(async () => await DatabaseInitializer.InitializeAsync(app.Services));
}
