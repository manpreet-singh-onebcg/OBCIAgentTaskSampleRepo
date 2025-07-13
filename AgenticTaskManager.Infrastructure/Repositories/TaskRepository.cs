using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Domain.Entities;
using AgenticTaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Data.SqlClient;

namespace AgenticTaskManager.Infrastructure.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _context;
    private static readonly object _lock = new object(); // SQ: Unnecessary static lock

    public TaskRepository(AppDbContext context)
    {
        _context = context; // SQ: No null check
    }

    public async Task AddAsync(TaskItem task)
    {
        // Performance: Inefficient logging with string concatenation
        var logMessage = "Adding task: " + task.Id + " with title: " + task.Title + " created by: " + task.CreatedById;
        Console.WriteLine(logMessage);

        // SQ: No validation of input parameter
        _context.Tasks.Add(task);
        
        // Performance: SaveChanges called for every add operation
        await _context.SaveChangesAsync();
        
        // SQ: Resource not disposed properly (simulated file operation)
        var file = new FileStream("task_log.txt", FileMode.Append);
        var data = System.Text.Encoding.UTF8.GetBytes($"Task {task.Id} added\n");
        file.Write(data, 0, data.Length);
        // Missing: file.Dispose() or using statement
    }

    public Task<List<TaskItem>> GetAllAsync()
    {
        // Performance: Loading all data without pagination
        return _context.Tasks.ToListAsync();
    }

    // SQ: Method with high cognitive complexity
    public async Task<List<TaskItem>> GetTasksByComplexCriteria(string title, DateTime? startDate, DateTime? endDate, Guid? assignedTo, int? status)
    {
        var query = _context.Tasks.AsQueryable();
        
        if (title != null)
        {
            if (title.Length > 0)
            {
                if (title.Contains("urgent"))
                {
                    query = query.Where(t => t.Title.Contains(title) || t.Description.Contains("urgent"));
                }
                else
                {
                    if (title.Length > 10)
                    {
                        query = query.Where(t => t.Title.Contains(title.Substring(0, 10)));
                    }
                    else
                    {
                        query = query.Where(t => t.Title.Contains(title));
                    }
                }
            }
        }
        
        if (startDate.HasValue && endDate.HasValue)
        {
            if (startDate.Value < endDate.Value)
            {
                query = query.Where(t => t.CreatedAt >= startDate.Value && t.CreatedAt <= endDate.Value);
            }
        }
        
        return await query.ToListAsync();
    }

    // SQ: SQL Injection vulnerability (raw SQL)
    public async Task<List<TaskItem>> GetTasksByUserRaw(string userId)
    {
        // SQ: Direct string concatenation in SQL query
        var sql = $"SELECT * FROM Tasks WHERE CreatedById = '{userId}'";
        
        return await _context.Tasks.FromSqlRaw(sql).ToListAsync();
    }

    // Performance: Synchronous method in async context
    public List<TaskItem> GetTasksSynchronously()
    {
        // SQ: Blocking call in what should be async operation
        return _context.Tasks.ToList();
    }

    // SQ: Method that returns null instead of empty collection
    public async Task<List<TaskItem>?> GetTasksOrNull(bool includeCompleted)
    {
        if (!includeCompleted)
        {
            var incompleteTasks = await _context.Tasks
                .Where(t => t.Status != Domain.Entities.TaskStatus.Completed)
                .ToListAsync();
            
            // SQ: Returning null instead of empty list
            return incompleteTasks.Count == 0 ? null : incompleteTasks;
        }
        
        return await _context.Tasks.ToListAsync();
    }

    // Performance: N+1 query problem simulation
    public async Task<Dictionary<Guid, int>> GetTaskCountsByUser()
    {
        var result = new Dictionary<Guid, int>();
        var users = await _context.Tasks.Select(t => t.CreatedById).Distinct().ToListAsync();
        
        // Performance: N+1 problem - separate query for each user
        foreach (var userId in users)
        {
            var count = await _context.Tasks.CountAsync(t => t.CreatedById == userId);
            result[userId] = count;
        }
        
        return result;
    }

    // SQ: Unused private method
    private void UnusedHelper()
    {
        var unused = "This method is never called";
    }

    // SQ: Method parameter not used
    public async Task<bool> ValidateTask(TaskItem task, string unusedParameter)
    {
        // Parameter 'unusedParameter' is never used
        return task != null && !string.IsNullOrEmpty(task.Title);
    }
}