using System.Text;
using AgenticTaskManager.Domain.Entities;
using AgenticTaskManager.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgenticTaskManager.Infrastructure.Services;

/// <summary>
/// Provides helper services for task management operations including statistics,
/// filtering, bulk operations, and data analysis.
/// </summary>
public class TaskHelperService : IDisposable
{
    private readonly ITaskRepository _repository;
    private readonly ILogger<TaskHelperService> _logger;
    private bool _disposed = false;

    public TaskHelperService(ITaskRepository repository, ILogger<TaskHelperService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Calculates comprehensive statistics for all tasks in the system.
    /// </summary>
    /// <returns>Task statistics including totals, completion rates, and percentages.</returns>
    public async Task<TaskStatistics> CalculateTaskStatisticsAsync()
    {
        try
        {
            _logger.LogInformation("Calculating task statistics");
            
            var tasks = await _repository.GetAllAsync();
            var taskList = tasks.ToList();

            var totalTasks = taskList.Count;
            var completedTasks = taskList.Count(t => t.IsCompleted);
            var pendingTasks = totalTasks - completedTasks;
            var completionPercentage = totalTasks > 0 ? (double)completedTasks / totalTasks * 100 : 0;

            var statistics = new TaskStatistics
            {
                TotalTasks = totalTasks,
                CompletedTasks = completedTasks,
                PendingTasks = pendingTasks,
                CompletionPercentage = Math.Round(completionPercentage, 2)
            };

            _logger.LogInformation("Task statistics calculated: {TotalTasks} total, {CompletedTasks} completed, {CompletionPercentage}% completion rate", 
                totalTasks, completedTasks, statistics.CompletionPercentage);

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating task statistics");
            return new TaskStatistics();
        }
    }

    /// <summary>
    /// Retrieves tasks filtered by priority level.
    /// </summary>
    /// <param name="priority">Priority level (1-10, where 10 is highest priority)</param>
    /// <returns>Tasks with the specified priority, ordered by creation date descending.</returns>
    public async Task<IEnumerable<TaskItem>> GetTasksByPriorityAsync(int priority)
    {
        if (priority < 1 || priority > 10)
        {
            throw new ArgumentException("Priority must be between 1 and 10", nameof(priority));
        }

        try
        {
            _logger.LogDebug("Retrieving tasks with priority {Priority}", priority);
            
            var tasks = await _repository.GetAllAsync();
            var filteredTasks = tasks.Where(t => t.Priority == priority)
                                   .OrderByDescending(t => t.CreatedAt)
                                   .ToList();

            _logger.LogInformation("Found {TaskCount} tasks with priority {Priority}", 
                filteredTasks.Count, priority);

            return filteredTasks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tasks by priority {Priority}", priority);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all overdue tasks that are not completed.
    /// </summary>
    /// <returns>Overdue tasks ordered by due date ascending.</returns>
    public async Task<IEnumerable<TaskItem>> GetOverdueTasksAsync()
    {
        try
        {
            _logger.LogDebug("Retrieving overdue tasks");
            
            var currentDate = DateTime.UtcNow;
            var tasks = await _repository.GetAllAsync();
            
            var overdueTasks = tasks.Where(t => t.DueDate.HasValue && 
                                              t.DueDate.Value < currentDate && 
                                              !t.IsCompleted)
                                   .OrderBy(t => t.DueDate)
                                   .ToList();

            _logger.LogInformation("Found {OverdueTaskCount} overdue tasks", overdueTasks.Count);

            return overdueTasks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue tasks");
            throw;
        }
    }

    /// <summary>
    /// Updates the priority for multiple tasks in a single operation.
    /// </summary>
    /// <param name="taskIds">Collection of task IDs to update</param>
    /// <param name="newPriority">New priority level (1-10)</param>
    /// <returns>True if all tasks were updated successfully, false otherwise.</returns>
    public async Task<bool> BulkUpdateTaskPriorityAsync(IEnumerable<int> taskIds, int newPriority)
    {
        if (taskIds == null || !taskIds.Any())
        {
            throw new ArgumentException("Task IDs collection cannot be null or empty", nameof(taskIds));
        }

        if (newPriority < 1 || newPriority > 10)
        {
            throw new ArgumentException("Priority must be between 1 and 10", nameof(newPriority));
        }

        try
        {
            _logger.LogInformation("Starting bulk priority update for {TaskCount} tasks to priority {Priority}", 
                taskIds.Count(), newPriority);

            var tasks = new List<TaskItem>();
            var taskIdList = taskIds.ToList();
            
            foreach (var taskId in taskIdList)
            {
                var task = await _repository.GetByIdAsync(taskId);
                if (task == null)
                {
                    _logger.LogWarning("Task with ID {TaskId} not found during bulk update", taskId);
                    return false;
                }
                
                task.Priority = newPriority;
                task.UpdatedAt = DateTime.UtcNow;
                tasks.Add(task);
            }

            // Update all tasks
            foreach (var task in tasks)
            {
                await _repository.UpdateAsync(task);
            }

            _logger.LogInformation("Successfully bulk updated {TaskCount} tasks with priority {Priority}", 
                tasks.Count, newPriority);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk priority update for tasks: {TaskIds}", 
                string.Join(", ", taskIds));
            return false;
        }
    }

    /// <summary>
    /// Analyzes task completion trends over a specified date range.
    /// </summary>
    /// <param name="startDate">Start date for analysis</param>
    /// <param name="endDate">End date for analysis</param>
    /// <returns>Daily completion counts within the specified date range.</returns>
    public async Task<IEnumerable<TaskCompletionTrend>> GetTaskCompletionTrendAsync(DateTime startDate, DateTime endDate)
    {
        if (startDate > endDate)
        {
            throw new ArgumentException("Start date cannot be later than end date");
        }

        try
        {
            _logger.LogDebug("Calculating task completion trend from {StartDate} to {EndDate}", 
                startDate, endDate);
            
            var tasks = await _repository.GetAllAsync();
            
            var completionTrend = tasks
                .Where(t => t.IsCompleted && 
                           t.UpdatedAt >= startDate && 
                           t.UpdatedAt <= endDate)
                .GroupBy(t => t.UpdatedAt.Date)
                .Select(g => new TaskCompletionTrend 
                { 
                    Date = g.Key, 
                    CompletedCount = g.Count() 
                })
                .OrderBy(x => x.Date)
                .ToList();

            _logger.LogInformation("Generated completion trend data for {DayCount} days with {TotalCompleted} total completions", 
                completionTrend.Count, completionTrend.Sum(t => t.CompletedCount));

            return completionTrend;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating task completion trend from {StartDate} to {EndDate}", 
                startDate, endDate);
            throw;
        }
    }

    /// <summary>
    /// Archives all completed tasks by updating their status.
    /// </summary>
    /// <returns>Number of tasks archived.</returns>
    public async Task<int> ArchiveCompletedTasksAsync()
    {
        try
        {
            _logger.LogInformation("Starting archival of completed tasks");
            
            var completedTasks = await _repository.GetCompletedTasksAsync();
            var archivedCount = 0;

            foreach (var task in completedTasks)
            {
                task.Status = AgenticTaskManager.Domain.Entities.TaskStatus.Archived;
                task.UpdatedAt = DateTime.UtcNow;
                
                await _repository.UpdateAsync(task);
                archivedCount++;
            }

            _logger.LogInformation("Successfully archived {ArchivedCount} completed tasks", archivedCount);
            return archivedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving completed tasks");
            throw;
        }
    }

    /// <summary>
    /// Exports tasks to a CSV file with proper resource management.
    /// </summary>
    /// <param name="tasks">Tasks to export</param>
    /// <param name="filePath">Target file path</param>
    public async Task ExportTasksToFileAsync(IEnumerable<TaskItem> tasks, string filePath)
    {
        if (tasks == null)
            throw new ArgumentNullException(nameof(tasks));
        
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        try
        {
            _logger.LogInformation("Exporting {TaskCount} tasks to file: {FilePath}", 
                tasks.Count(), filePath);

            await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            
            // Write CSV header
            await writer.WriteLineAsync("Id,Title,Description,Priority,IsCompleted,CreatedAt,DueDate,Status");
            
            foreach (var task in tasks)
            {
                var csvLine = $"{task.Id}," +
                             $"\"{EscapeCsvValue(task.Title)}\"," +
                             $"\"{EscapeCsvValue(task.Description)}\"," +
                             $"{task.Priority}," +
                             $"{task.IsCompleted}," +
                             $"{task.CreatedAt:yyyy-MM-dd HH:mm:ss}," +
                             $"{task.DueDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""}," +
                             $"{task.Status}";
                
                await writer.WriteLineAsync(csvLine);
            }

            await writer.FlushAsync();
            
            _logger.LogInformation("Successfully exported {TaskCount} tasks to {FilePath}", 
                tasks.Count(), filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting tasks to file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Validates task data with configurable strictness levels.
    /// </summary>
    /// <param name="task">Task to validate</param>
    /// <param name="strictMode">Whether to apply strict validation rules</param>
    /// <returns>Validation result with details</returns>
    public TaskValidationResult ValidateTask(TaskItem task, bool strictMode = false)
    {
        if (task == null)
        {
            return new TaskValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "Task cannot be null" 
            };
        }

        if (string.IsNullOrWhiteSpace(task.Title))
        {
            return new TaskValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "Task title is required" 
            };
        }

        if (strictMode)
        {
            if (string.IsNullOrWhiteSpace(task.Description))
            {
                return new TaskValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = "Task description is required in strict mode" 
                };
            }

            if (task.DueDate.HasValue && task.DueDate.Value <= DateTime.UtcNow)
            {
                return new TaskValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = "Due date must be in the future in strict mode" 
                };
            }
        }

        return new TaskValidationResult { IsValid = true };
    }

    private static string EscapeCsvValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        return value.Replace("\"", "\"\"");
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                _logger?.LogDebug("TaskHelperService disposed");
            }
            
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents comprehensive task statistics.
/// </summary>
public class TaskStatistics
{
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int PendingTasks { get; set; }
    public double CompletionPercentage { get; set; }
}

/// <summary>
/// Represents task completion trend data for a specific date.
/// </summary>
public class TaskCompletionTrend
{
    public DateTime Date { get; set; }
    public int CompletedCount { get; set; }
}

/// <summary>
/// Represents the result of task validation.
/// </summary>
public class TaskValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}
