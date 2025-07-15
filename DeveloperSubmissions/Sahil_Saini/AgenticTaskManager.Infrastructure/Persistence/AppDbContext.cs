using Microsoft.EntityFrameworkCore;
using AgenticTaskManager.Domain.Entities;

namespace AgenticTaskManager.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<TaskItem> Tasks { get; set; }
    public DbSet<Actor> Actors { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}