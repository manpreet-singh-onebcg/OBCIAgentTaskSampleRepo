using System;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Domain.Entities;
using AgenticTaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticTaskManager.Infrastructure.Repositories;

public class TaskRepository : ITaskRepository, IDisposable
{
    private readonly AppDbContext _context;
    private readonly ILogger<TaskRepository> _logger;
    private bool _disposed = false;

    public TaskRepository(AppDbContext context, ILogger<TaskRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TaskItem> AddAsync(TaskItem task)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));

        try
        {
            _logger.LogInformation("Adding task with title: {Title}", task.Title);
            
            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();
            
            return task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding task");
            throw;
        }
    }

    public async Task<IEnumerable<TaskItem>> GetAllAsync()
    {
        try
        {
            return await _context.Tasks
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all tasks");
            throw;
        }
    }

    public async Task<TaskItem?> GetByIdAsync(int id)
    {
        try
        {
            return await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving task with ID {TaskId}", id);
            throw;
        }
    }

    public async Task<TaskItem?> UpdateAsync(TaskItem task)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));

        try
        {
            _logger.LogInformation("Updating task with ID: {TaskId}", task.Id);
            
            _context.Tasks.Update(task);
            await _context.SaveChangesAsync();
            
            return task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task with ID {TaskId}", task.Id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null)
                return false;

            _logger.LogInformation("Deleting task with ID: {TaskId}", id);
            
            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting task with ID {TaskId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<TaskItem>> SearchAsync(string? title, string? description, DateTime? startDate, DateTime? endDate, int skip, int take)
    {
        try
        {
            var query = _context.Tasks.AsQueryable();

            if (!string.IsNullOrWhiteSpace(title))
            {
                query = query.Where(t => t.Title.Contains(title));
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                query = query.Where(t => t.Description.Contains(description));
            }

            if (startDate.HasValue)
            {
                query = query.Where(t => t.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(t => t.CreatedAt <= endDate.Value);
            }

            return await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching tasks");
            throw;
        }
    }

    public async Task<IEnumerable<TaskItem>> GetTasksByActorIdAsync(int actorId)
    {
        try
        {
            return await _context.Tasks
                .Where(t => t.AssignedActorId == actorId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tasks for actor {ActorId}", actorId);
            throw;
        }
    }

    public async Task<IEnumerable<TaskItem>> GetCompletedTasksAsync()
    {
        try
        {
            return await _context.Tasks
                .Where(t => t.IsCompleted)
                .OrderByDescending(t => t.UpdatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving completed tasks");
            throw;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed state (managed objects)
                // Note: We don't dispose the context here as it's injected via DI
                // The DI container is responsible for its lifecycle
            }
            _disposed = true;
        }
    }
}