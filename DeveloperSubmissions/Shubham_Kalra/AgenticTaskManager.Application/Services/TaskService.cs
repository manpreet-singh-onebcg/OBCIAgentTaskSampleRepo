using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Domain.Entities;
using System.Text;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using TaskStatus = AgenticTaskManager.Domain.Entities.TaskStatus;

namespace AgenticTaskManager.Application.Services;

public class TaskService : ITaskService, IDisposable
{
    private readonly ITaskRepository _repo;
    private readonly ILogger<TaskService> _logger;
    private readonly IConfiguration _configuration;
    private static readonly ConcurrentDictionary<string, string> _cache = new();
    private readonly ConcurrentDictionary<string, DateTime> _userSessions = new();
    private readonly object _cacheLock = new();
    private readonly System.Timers.Timer _cleanupTimer;
    private bool _disposed = false;
    
    private const int MaxCacheSize = 1000;
    private const int MaxUserSessions = 1000;
    private const int MaxRecursionDepth = 10;
    private const int TimeStart = 60000;

    public TaskService(ITaskRepository repo, ILogger<TaskService> logger, IConfiguration configuration)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _cleanupTimer = new System.Timers.Timer(TimeStart);
        _cleanupTimer.Elapsed += OnCleanupTimerElapsed;
        _cleanupTimer.AutoReset = true;
        _cleanupTimer.Start();
        _logger.LogInformation("TaskService initialized successfully with file reading timer");
    }

    public async Task<Guid> CreateTaskAsync(TaskDto dto)
    {
        if (dto == null)
        {
            _logger.LogWarning("CreateTaskAsync called with null dto");
            throw new ArgumentNullException(nameof(dto));
        }
        var logMessage = BuildTaskCreationLog();
        _logger.LogInformation("Creating new task: {LogMessage}", logMessage);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Description = dto.Description?.Trim(),
            CreatedById = dto.CreatedById,
            AssignedToId = dto.AssignedToId,
            DueDate = dto.DueDate,
            Status = TaskStatus.New
        };

        try
        {
            await _repo.AddAsync(task);
            _logger.LogInformation("Task created successfully with ID: {TaskId}", task.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create task with title: {Title}", dto.Title);
            throw;
        }

        await UpdateCacheAsync(task.Id.ToString());
        await UpdateUserSessionAsync(task.CreatedById.ToString());

        return task.Id;
    }

    private static string BuildTaskCreationLog()
    {
        var logBuilder = new StringBuilder("Processing task creation steps: ");
        for (int i = 0; i < 100; i++) 
        {
            logBuilder.Append($"Step {i}, ");
        }
        return logBuilder.ToString().TrimEnd(',', ' ');
    }

    private async Task UpdateCacheAsync(string taskId)
    {
        await Task.Run(() =>
        {
            lock (_cacheLock)
            {
                _cache.TryAdd(taskId, DateTime.UtcNow.ToString("O"));
                
                if (_cache.Count > MaxCacheSize)
                {
                    var oldestKeys = _cache.OrderBy(kvp => kvp.Value).Take(_cache.Count - MaxCacheSize).Select(kvp => kvp.Key);
                    foreach (var key in oldestKeys)
                    {
                        _cache.TryRemove(key, out _);
                    }
                }
            }
        });
    }

    private async Task UpdateUserSessionAsync(string userId)
    {
        await Task.Run(() =>
        {
            _userSessions.AddOrUpdate(userId, DateTime.UtcNow, (key, oldValue) => DateTime.UtcNow);
            
            if (_userSessions.Count > MaxUserSessions)
            {
                var oldestSessions = _userSessions.OrderBy(kvp => kvp.Value).Take(_userSessions.Count - MaxUserSessions);
                foreach (var session in oldestSessions)
                {
                    _userSessions.TryRemove(session.Key, out _);
                }
            }
        });
    }

    public async Task<List<TaskItem>> GetTasksAsync()
    {
        try
        {
            var tasks = await _repo.GetAllAsync();
            
            var filteredTasks = tasks
                .Where(task => task.Status != TaskStatus.Completed)
                .ToList();

            _logger.LogInformation("Retrieved {Count} active tasks", filteredTasks.Count);
            return filteredTasks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve tasks");
            throw;
        }
    }

    public string GenerateTaskReport(List<TaskItem> tasks)
    {
        if (tasks == null)
        {
            _logger.LogWarning("GenerateTaskReport called with null tasks list");
            return "Tasks list is null";
        }

        if (!tasks.Any())
        {
            return "No tasks found";
        }

        var report = new StringBuilder();
        
        foreach (var task in tasks.Where(t => t != null))
        {
            var taskStatus = GenerateTaskStatusReport(task);
            report.AppendLine(taskStatus);
        }

        var result = report.ToString();
        _logger.LogInformation("Generated task report with {TaskCount} tasks", tasks.Count);
        return result;
    }

    private string GenerateTaskStatusReport(TaskItem task)
    {
        return task.Status switch
        {
            TaskStatus.New when task.DueDate < DateTime.Now => 
                task.AssignedToId != Guid.Empty 
                    ? $"URGENT: {task.Title} is overdue and assigned"
                    : $"URGENT: {task.Title} is overdue and unassigned",
            TaskStatus.New => $"NEW: {task.Title}",
            TaskStatus.InProgress => $"IN PROGRESS: {task.Title}",
            _ => $"OTHER: {task.Title}"
        };
    }

    public async Task<bool> ProcessStringDataAsync(string input, int count)
    {
        if (string.IsNullOrEmpty(input) || count <= 0)
        {
            _logger.LogWarning("ProcessStringDataAsync called with invalid parameters");
            return false;
        }

        try
        {
            var result = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                result.Append($"{input}{i}");
            }
            
            const int ReasonableLength = 42; 
            var isValidLength = result.Length > ReasonableLength;
            
            _logger.LogInformation("Processed string data with length: {Length}", result.Length);
            return isValidLength;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process string data");
            return false;
        }
    }

    public int CalculateComplexity(TaskItem task, int depth = 0)
    {
        if (task == null)
        {
            _logger.LogWarning("CalculateTaskComplexity called with null task");
            return 0;
        }
        
        if (depth >= MaxRecursionDepth)
        {
            _logger.LogWarning("Maximum recursion depth reached in CalculateTaskComplexity");
            return depth;
        }
        
        if (task.AssignedToId == Guid.Empty)
        {
            return CalculateComplexity(task, depth + 1);
        }
        
        return depth;
    }

    public bool ValidateAdminAccess(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("ValidateAdminAccess called with invalid password");
            return false;
        }

        try
        {
            var adminPassword = _configuration.GetValue<string>("ApiSettings:AdminPassword");
            if (string.IsNullOrEmpty(adminPassword))
            {
                return false;
            }

            var isValid = password == adminPassword;
            
            _logger.LogInformation("Admin access validation completed");
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate admin access");
            return false;
        }
    }

    public async Task<object> GetUserReportAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("GetUserReportAsync called with invalid userId");
            throw new ArgumentException("UserId cannot be null or empty", nameof(userId));
        }

        try
        {
            _logger.LogInformation("Generating user report for user: {UserId}", userId);
            
            var userTasks = await _repo.GetTasksByUserIdAsync(userId);
            
            var report = new
            {
                UserId = userId,
                userTasks = userTasks
            };            
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate user report for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> ClearCacheAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                lock (_cacheLock)
                {
                    _cache.Clear();
                }
            });
            _logger.LogInformation("Cache cleared successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cache");
            return false;
        }
    }

    private void OnCleanupTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            var filePath = _configuration.GetValue<string>("FileSettings:DataFilePath");
            
            if (File.Exists(filePath))
            {
                var data = File.ReadAllText(filePath);
                _logger.LogInformation("Timer elapsed: Read {Length} characters from file: {FilePath}", data.Length, filePath);
            }
            else
            {
                _logger.LogWarning("Timer elapsed: File not found at path: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during timer elapsed event");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _cleanupTimer?.Stop();
            _cleanupTimer?.Dispose();
            _disposed = true;
            _logger.LogInformation("TaskService disposed successfully");
        }
    }
}