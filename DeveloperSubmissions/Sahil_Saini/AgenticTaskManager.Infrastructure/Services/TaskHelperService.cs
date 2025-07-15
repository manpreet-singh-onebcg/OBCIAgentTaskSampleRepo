using AgenticTaskManager.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace AgenticTaskManager.Infrastructure.Services;

/// <summary>
/// Service for task-related helper operations with proper design patterns and error handling
/// </summary>
public interface ITaskHelperService : IDisposable
{
    Task<string> ProcessTaskDataAsync(TaskItem task, TaskOperation operation, TaskProcessingOptions? options = null, CancellationToken cancellationToken = default);
    int CalculateTaskPriority(TaskItem task, int maxDepth = 10);
    Task<List<string>> GenerateTaskSummariesAsync(IEnumerable<TaskItem> tasks, CancellationToken cancellationToken = default);
    Task ExportTasksToFileAsync(IEnumerable<TaskItem> tasks, string filePath, CancellationToken cancellationToken = default);
    bool ValidateTaskData(TaskItem task);
}

/// <summary>
/// Implementation of task helper service with proper error handling, resource management, and thread safety
/// </summary>
public class TaskHelperService : ITaskHelperService
{
    // Constants to replace magic numbers
    private const int DefaultMaxLogEntries = 1000;
    private const int MaxRecursionDepth = 10;
    private const int MaxFormattingIterations = 1000;
    private const int DatabaseConnectionTimeoutMs = 30000;

    private readonly ILogger<TaskHelperService> _logger;
    private readonly IConfiguration _configuration;
    private readonly object _lockObject = new();
    private bool _disposed = false;

    // Thread-safe collections instead of static mutable fields
    private readonly List<string> _errorLog = new();
    private readonly Dictionary<string, object> _cache = new();

    public TaskHelperService(ILogger<TaskHelperService> logger, IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        InitializeService();
    }

    /// <summary>
    /// Initialize service with proper error handling and async operations
    /// </summary>
    private void InitializeService()
    {
        try
        {
            InitializeConfiguration();
            _logger.LogInformation("TaskHelperService initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize TaskHelperService");
            throw new InvalidOperationException("Service initialization failed", ex);
        }
    }

    /// <summary>
    /// Initialize configuration from IConfiguration instead of hardcoded values
    /// </summary>
    private void InitializeConfiguration()
    {
        lock (_lockObject)
        {
            // Use manual configuration parsing
            var maxRetries = GetConfigurationValue<int>("TaskHelper:MaxRetries", 5);
            var timeoutMs = GetConfigurationValue<int>("TaskHelper:TimeoutMs", DatabaseConnectionTimeoutMs);

            _cache["MaxRetries"] = maxRetries;
            _cache["TimeoutMs"] = timeoutMs;

            var configSummary = new StringBuilder();
            foreach (var item in _cache)
            {
                configSummary.Append($"{item.Key}={item.Value};");
            }

            _logger.LogDebug("Configuration loaded: {Config}", configSummary.ToString());
        }
    }

    /// <summary>
    /// Helper method to get configuration values with fallback to default
    /// </summary>
    private T GetConfigurationValue<T>(string key, T defaultValue) where T : struct
    {
        try
        {
            var value = _configuration[key];
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse configuration value for key {Key}, using default {DefaultValue}", key, defaultValue);
            return defaultValue;
        }
    }

    /// <summary>
    /// Process task data with reduced cognitive complexity using strategy pattern
    /// </summary>
    public async Task<string> ProcessTaskDataAsync(TaskItem task, TaskOperation operation, TaskProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (task == null)
        {
            _logger.LogWarning("ProcessTaskDataAsync called with null task");
            return "Task is null";
        }

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("ProcessTaskDataAsync cancelled");
            return "Operation cancelled";
        }

        try
        {
            return operation switch
            {
                TaskOperation.Validate => await ValidateTaskAsync(task, options),
                TaskOperation.Format => await FormatTaskAsync(task, options),
                _ => "Unknown operation"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing task data for task {TaskId} with operation {Operation}", task.Id, operation);
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Validate task with clear validation rules (extracted from complex method)
    /// </summary>
    private async Task<string> ValidateTaskAsync(TaskItem task, TaskProcessingOptions? options)
    {
        await Task.Delay(1); // Simulate async validation

        var validationRules = new TaskValidationRules();

        if (!validationRules.HasValidTitle(task))
            return "Invalid: No title";

        if (!validationRules.HasValidDescription(task))
            return "Invalid: No description";

        var isStrictMode = options?.StrictMode ?? false;

        if (isStrictMode)
        {
            return validationRules.IsOverdue(task) 
                ? "Invalid: Due date in past" 
                : "Valid";
        }

        return "Valid (lenient)";
    }

    /// <summary>
    /// Format task with efficient string operations
    /// </summary>
    private async Task<string> FormatTaskAsync(TaskItem task, TaskProcessingOptions? options)
    {
        await Task.Delay(1); // Simulate async formatting

        var iterations = Math.Min(options?.Iterations ?? 100, MaxFormattingIterations);
        var result = new StringBuilder(iterations * 50); // Pre-allocate capacity

        for (int i = 0; i < iterations; i++)
        {
            result.AppendLine($"Task: {task.Title} - Iteration {i}");
        }

        return result.ToString();
    }

    /// <summary>
    /// Calculate task priority with proper recursion depth limiting
    /// </summary>
    public int CalculateTaskPriority(TaskItem task, int maxDepth = MaxRecursionDepth)
    {
        return CalculateTaskPriorityInternal(task, 0, maxDepth);
    }

    /// <summary>
    /// Internal method with depth tracking to prevent stack overflow
    /// </summary>
    private int CalculateTaskPriorityInternal(TaskItem task, int currentDepth, int maxDepth)
    {
        if (task == null) 
        {
            _logger.LogWarning("CalculateTaskPriority called with null task");
            return 0;
        }

        if (currentDepth >= maxDepth)
        {
            _logger.LogWarning("Maximum recursion depth reached for task priority calculation");
            return currentDepth * 10;
        }

        if (task.AssignedToId == Guid.Empty)
        {
            return CalculateTaskPriorityInternal(task, currentDepth + 1, maxDepth);
        }

        return currentDepth * 10;
    }

    /// <summary>
    /// Generate task summaries with efficient object creation and proper async operations
    /// </summary>
    public async Task<List<string>> GenerateTaskSummariesAsync(IEnumerable<TaskItem> tasks, CancellationToken cancellationToken = default)
    {
        if (tasks == null)
        {
            _logger.LogWarning("GenerateTaskSummariesAsync called with null tasks");
            return new List<string>();
        }

        var summaries = new List<string>();
        var taskList = tasks.ToList(); // Enumerate once

        // Pre-create reusable objects outside the loop
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        foreach (var task in taskList)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("GenerateTaskSummariesAsync cancelled");
                break;
            }

            try
            {
                var formatter = new { task.Title, Date = DateTime.UtcNow };
                var json = JsonSerializer.Serialize(formatter, jsonOptions);
                var summary = $"Task: {task.Title} - {json}";
                
                summaries.Add(summary);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate summary for task {TaskId}", task.Id);
                summaries.Add($"Task: {task.Title} - Error generating summary");
            }
        }

        await Task.Delay(1, cancellationToken); // Simulate async work
        return summaries;
    }

    /// <summary>
    /// Export tasks to file with proper resource management and async operations
    /// </summary>
    public async Task ExportTasksToFileAsync(IEnumerable<TaskItem> tasks, string filePath, CancellationToken cancellationToken = default)
    {
        if (tasks == null)
            throw new ArgumentNullException(nameof(tasks));

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(stream, Encoding.UTF8);

            // Write header
            await writer.WriteLineAsync("Id,Title,Description,Status,DueDate");

            foreach (var task in tasks)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("ExportTasksToFileAsync cancelled");
                    break;
                }

                var line = $"{task.Id},{EscapeCsvField(task.Title)},{EscapeCsvField(task.Description)},{task.Status},{task.DueDate:yyyy-MM-dd}";
                await writer.WriteLineAsync(line);
            }

            await writer.FlushAsync();
            _logger.LogInformation("Successfully exported tasks to file: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export tasks to file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Validate task data with clear business rules
    /// </summary>
    public bool ValidateTaskData(TaskItem task)
    {
        if (task == null)
        {
            _logger.LogWarning("ValidateTaskData called with null task");
            return false;
        }

        var rules = new TaskValidationRules();
        var isValid = rules.HasValidTitle(task) && 
                     rules.HasValidDescription(task) && 
                     !rules.IsOverdue(task);

        _logger.LogDebug("Task {TaskId} validation result: {IsValid}", task.Id, isValid);
        return isValid;
    }

    /// <summary>
    /// Helper method to properly escape CSV fields
    /// </summary>
    private static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    /// <summary>
    /// Proper disposal pattern implementation
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected disposal method
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                lock (_lockObject)
                {
                    _errorLog.Clear();
                    _cache.Clear();
                }
                
                _logger.LogDebug("TaskHelperService disposed");
            }

            _disposed = true;
        }
    }
}

/// <summary>
/// Enumeration for task operations to replace string-based operations
/// </summary>
public enum TaskOperation
{
    Validate,
    Format
}

/// <summary>
/// Options class to replace dictionary parameters
/// </summary>
public class TaskProcessingOptions
{
    public bool StrictMode { get; set; } = false;
    public int Iterations { get; set; } = 100;
}

/// <summary>
/// Validation rules extracted into a separate class for better maintainability
/// </summary>
public class TaskValidationRules
{
    public bool HasValidTitle(TaskItem task)
    {
        return !string.IsNullOrWhiteSpace(task.Title);
    }

    public bool HasValidDescription(TaskItem task)
    {
        return !string.IsNullOrWhiteSpace(task.Description);
    }

    public bool IsOverdue(TaskItem task)
    {
        return task.DueDate < DateTime.UtcNow;
    }
}
