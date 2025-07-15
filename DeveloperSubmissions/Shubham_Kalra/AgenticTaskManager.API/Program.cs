using Microsoft.EntityFrameworkCore;
using AgenticTaskManager.Infrastructure.Persistence;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Infrastructure.Repositories;
using AgenticTaskManager.Application.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure HttpClient Factory
builder.Services.AddHttpClient();

// Configure Entity Framework with connection string validation
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("DefaultConnection string is not configured.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    }));

// Configure Authorization (basic - no JWT for now)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// Register application services
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<ITaskService, TaskService>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Security and middleware configuration
app.UseHttpsRedirection();
app.UseCors();

// Authorization (no authentication for now)
app.UseAuthorization();

// Health checks
app.MapHealthChecks("/health");

app.MapControllers();

app.Run();