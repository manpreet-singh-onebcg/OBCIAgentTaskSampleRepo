using Microsoft.EntityFrameworkCore;
using AgenticTaskManager.Infrastructure.Persistence;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Infrastructure.Repositories;
using AgenticTaskManager.Application.Services;
using AgenticTaskManager.Infrastructure.Configuration;
using AgenticTaskManager.Infrastructure.Security;
using AgenticTaskManager.Infrastructure.Utilities;

var builder = WebApplication.CreateBuilder(args);

try
{
    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    
    // Only add Swagger in development
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddSwaggerGen();
    }

    // Configure database with proper error handling
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Database connection string is not configured.");
    }

    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
        
        // Only enable sensitive data logging in development
        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
        }
    });

    // Register application services
    builder.Services.AddScoped<ITaskRepository, TaskRepository>();
    builder.Services.AddScoped<ITaskService, TaskService>();

    // Register infrastructure services manually
    builder.Services.AddSingleton<SecurityConfiguration>();
    builder.Services.AddScoped<SecurityHelper>();
    builder.Services.AddScoped<SecureUtilities>();
    
    // Register HttpClient for external API calls
    builder.Services.AddHttpClient();

    // Add CORS policy
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            }
            else
            {
                // Configure restrictive CORS for production
                policy.WithOrigins("https://yourdomain.com")
                      .WithMethods("GET", "POST", "PUT", "DELETE")
                      .WithHeaders("Content-Type", "Authorization");
            }
        });
    });

    // Add logging
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    
    if (builder.Environment.IsProduction())
    {
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
    }

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    // Security middleware
    app.UseHttpsRedirection();
    
    // Add security headers
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        
        if (!app.Environment.IsDevelopment())
        {
            context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        }
        
        await next();
    });

    app.UseCors();
    app.UseRouting();
    app.UseAuthorization();
    app.MapControllers();

    // Add health check endpoint
    app.MapGet("/health", () => new { Status = "Healthy", Timestamp = DateTime.UtcNow });

    app.Logger.LogInformation("Application configured successfully. Environment: {Environment}", 
        app.Environment.EnvironmentName);

    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"Application failed to start: {ex.Message}");
    if (builder.Environment.IsDevelopment())
    {
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
    }
    Environment.Exit(1);
}

// Make Program class public for testing
public partial class Program { }