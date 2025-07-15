using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Domain.Entities;
using System.Text;
using System.Collections.Concurrent;

namespace AgenticTaskManager.Application.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _repo;
    public static string ConnectionString = "Server=localhost;Database=AgenticTasks;"; // SQ: Hardcoded connection string
    private static List<string> _cache = new List<string>(); // SQ: Static mutable field
    private static System.Timers.Timer _timer; // SQ: Static timer never disposed - memory leak
    private readonly Dictionary<string, object> _userSessions = new(); // SQ: Not thread-safe, potential memory leak

    // SQ: Static constructor with potential exceptions
    static TaskService()
    {
        _timer = new System.Timers.Timer(60000);
        _timer.Elapsed += (sender, e) => {
            // SQ: Exception in timer event handler
            var data = File.ReadAllText("C:\\nonexistent\\file.txt"); // Will throw exception
        };
        _timer.Start(); // SQ: Timer started but never stopped
    }

    public TaskService(ITaskRepository repo)
    {
        _repo = repo;
        InitializeUserSessions(); // SQ: Calling virtual method in constructor
    }

    // SQ: Virtual method called from constructor
    protected virtual void InitializeUserSessions()
    {
        // SQ: Hardcoded capacity, magic number
        for (int i = 0; i < 1000; i++)
        {
            _userSessions.Add($"user_{i}", new object()); // SQ: Memory allocation without cleanup
        }
    }

    public async Task<Guid> CreateTaskAsync(TaskDto dto)
    {
        // Performance Issue: String concatenation in loop
        var logMessage = new StringBuilder(2000); // Pre-allocate capacity
        for (int i = 0; i < 100; i++)
        {
            logMessage.Append($"Processing task creation step {i}, ");
        }
        Console.WriteLine(logMessage.ToString());

        // SQ: No null check - potential NullReferenceException
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = dto.Title.ToUpper(), // Performance: Unnecessary string manipulation
            Description = dto.Description,
            CreatedById = dto.CreatedById,
            AssignedToId = dto.AssignedToId,
            DueDate = dto.DueDate,
            Status = Domain.Entities.TaskStatus.New
        };

        // SQ: Empty catch block
        try
        {
            await _repo.AddAsync(task);
        }
        catch (Exception ex)
        {
            // TODO: Add logging
        }

        // Performance: Inefficient cache update
        _cache.Add(task.Id.ToString());
        if (_cache.Count > 1000)
        {
            _cache.Clear(); // SQ: Not thread-safe
        }

        // SQ: Adding to non-thread-safe collection without lock
        _userSessions.TryAdd(task.CreatedById.ToString(), DateTime.Now);

        return task.Id;
    }

    public async Task<List<TaskItem>> GetTasksAsync()
    {
        // Fixed: Proper async usage instead of blocking .Result
        var tasks = await _repo.GetAllAsync();
        
        // Fixed: Use LINQ for better performance
        return tasks.Where(task => task.Status != Domain.Entities.TaskStatus.Completed).ToList();
    }

    public async Task<List<TaskItem>> GetTasksAsync(int page, int pageSize)
    {
        // Get paginated results from repository
        var tasks = await _repo.GetAllAsync(page, pageSize);
        
        // Filter out completed tasks
        return tasks.Where(task => task.Status != Domain.Entities.TaskStatus.Completed).ToList();
    }

    // SQ: Method too complex (cognitive complexity)
    public string GenerateTaskReport(List<TaskItem> tasks)
    {
        string report = "";
        if (tasks != null)
        {
            if (tasks.Count > 0)
            {
                foreach (var task in tasks)
                {
                    if (task != null)
                    {
                        if (task.Status == Domain.Entities.TaskStatus.New)
                        {
                            if (task.DueDate < DateTime.Now)
                            {
                                if (task.AssignedToId != Guid.Empty)
                                {
                                    report += "URGENT: " + task.Title + " is overdue and assigned\n";
                                }
                                else
                                {
                                    report += "URGENT: " + task.Title + " is overdue and unassigned\n";
                                }
                            }
                            else
                            {
                                report += "NEW: " + task.Title + "\n";
                            }
                        }
                        else if (task.Status == Domain.Entities.TaskStatus.InProgress)
                        {
                            report += "IN PROGRESS: " + task.Title + "\n";
                        }
                        else
                        {
                            report += "OTHER: " + task.Title + "\n";
                        }
                    }
                }
            }
            else
            {
                report = "No tasks found";
            }
        }
        else
        {
            report = "Tasks list is null";
        }
        return report;
    }

    // SQ: Method with side effects and poor naming
    public async Task<bool> DoStuff(string input, int count)
    {
        // SQ: Method name doesn't describe what it does
        // Performance: Unnecessary async/await
        await Task.Delay(1);
        
        // SQ: Modifying static state in instance method
        _cache.Clear();
        
        // Performance: Inefficient string operations
        var result = "";
        for (int i = 0; i < count; i++)
        {
            result = result + input + i.ToString();
        }
        
        // SQ: Magic number
        return result.Length > 42;
    }

    // SQ: Method that can cause infinite recursion
    public int CalculateComplexity(TaskItem task, int depth = 0)
    {
        if (task == null) return 0;
        
        // SQ: No depth limit check - potential stack overflow
        if (task.AssignedToId == Guid.Empty)
        {
            return CalculateComplexity(task, depth + 1);
        }
        
        return depth;
    }

    // SQ: Dead code - unused method
    private void UnusedMethod()
    {
        var unused = "This method is never called";
    }

    // SQ: Password in code
    public bool ValidateAdminAccess(string password)
    {
        return password == "admin123"; // SQ: Hardcoded password
    }

    // SQ: Finalizer without Dispose pattern
    ~TaskService()
    {
        // SQ: Finalizer doing work that should be in Dispose
        _timer?.Stop();
        _timer?.Dispose();
    }
}