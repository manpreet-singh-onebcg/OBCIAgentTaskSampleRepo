using Microsoft.EntityFrameworkCore;
using AgenticTaskManager.Infrastructure.Persistence;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Infrastructure.Repositories;
using AgenticTaskManager.Application.Services;

var builder = WebApplication.CreateBuilder(args);

// SQ: No error handling for service configuration
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SQ: No validation of connection string
// Performance: No connection pooling configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<ITaskService, TaskService>();

// SQ: No error handling during build
var app = builder.Build();

// Performance: Swagger enabled in all environments (security risk in production)
app.UseSwagger();
app.UseSwaggerUI();

// SQ: Missing important middleware
// Missing: app.UseAuthentication();
// Missing: app.UseHttpsRedirection();
// Missing: app.UseCors();
// Missing: Error handling middleware

// SQ: No request logging or monitoring
// SQ: No rate limiting
// SQ: No security headers

app.UseAuthorization();
app.MapControllers();

// SQ: No graceful shutdown handling
// Performance: No health checks configured
app.Run();