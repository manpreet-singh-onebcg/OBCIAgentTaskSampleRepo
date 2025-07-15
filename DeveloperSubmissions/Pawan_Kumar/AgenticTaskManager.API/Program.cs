using Microsoft.EntityFrameworkCore;
using AgenticTaskManager.Infrastructure.Persistence;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Infrastructure.Repositories;
using AgenticTaskManager.Application.Services;
using AgenticTaskManager.API.Filters;
using Microsoft.AspNetCore.HttpOverrides;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

try
{
    // Configure services with error handling
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<GlobalExceptionFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = false;
    });
    
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    
    // Add health checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>();

    // Validate and configure connection string with connection pooling
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Database connection string 'DefaultConnection' not found.");
    }

    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(30);
        });
        
        // Enable sensitive data logging only in development
        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
        }
    });

    // Configure dependency injection
    builder.Services.AddScoped<ITaskRepository, TaskRepository>();
    builder.Services.AddScoped<ITaskService, TaskService>();
    
    // Configure security headers
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline with proper security
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        app.UseExceptionHandler("/error");
        app.UseHsts();
    }

    // Security middleware
    app.UseForwardedHeaders();
    app.UseHttpsRedirection();
    
    // Add security headers
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        await next();
    });

    // Health checks endpoint
    app.MapHealthChecks("/health");
    
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    // Log startup errors
    Console.WriteLine($"Application failed to start: {ex.Message}");
    throw;
}

// Make Program class accessible to integration tests
public partial class Program { }