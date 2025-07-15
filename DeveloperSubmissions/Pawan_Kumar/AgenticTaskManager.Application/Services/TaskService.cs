using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgenticTaskManager.Application.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _repository;
    private readonly ILogger<TaskService> _logger;

    public TaskService(ITaskRepository repository, ILogger<TaskService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TaskDto?> CreateAsync(TaskDto dto)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                _logger.LogWarning("Task creation failed: Title is null or empty");
                return null;
            }

            var task = new TaskItem
            {
                Title = dto.Title,
                Description = dto.Description,
                Status = Domain.Entities.TaskStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                CreatedById = dto.CreatedById
            };

            var createdTask = await _repository.AddAsync(task);
            
            return new TaskDto
            {
                Id = createdTask.Id,
                Title = createdTask.Title,
                Description = createdTask.Description,
                Status = createdTask.Status.ToString(),
                CreatedAt = createdTask.CreatedAt,
                CreatedById = createdTask.CreatedById
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task");
            return null;
        }
    }

    public async Task<IEnumerable<TaskDto>> GetAllAsync()
    {
        var tasks = await _repository.GetAllAsync();
        
        return tasks.Select(t => new TaskDto
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            Status = t.Status.ToString(),
            CreatedAt = t.CreatedAt,
            CreatedById = t.CreatedById
        });
    }

    public async Task<TaskDto?> GetByIdAsync(int id)
    {
        try
        {
            var task = await _repository.GetByIdAsync(id);
            
            if (task == null)
                return null;

            return new TaskDto
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                Status = task.Status.ToString(),
                CreatedAt = task.CreatedAt,
                CreatedById = task.CreatedById
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving task by ID {TaskId}", id);
            return null;
        }
    }

    public async Task<TaskDto?> UpdateAsync(int id, TaskDto dto)
    {
        try
        {
            var existingTask = await _repository.GetByIdAsync(id);
            if (existingTask == null)
                return null;

            existingTask.Title = dto.Title;
            existingTask.Description = dto.Description;
            existingTask.UpdatedAt = DateTime.UtcNow;

            var updatedTask = await _repository.UpdateAsync(existingTask);
            
            if (updatedTask == null)
                return null;

            return new TaskDto
            {
                Id = updatedTask.Id,
                Title = updatedTask.Title,
                Description = updatedTask.Description,
                Status = updatedTask.Status.ToString(),
                CreatedAt = updatedTask.CreatedAt,
                CreatedById = updatedTask.CreatedById
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task {TaskId}", id);
            return null;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            return await _repository.DeleteAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting task {TaskId}", id);
            return false;
        }
    }

    public async Task<IEnumerable<TaskDto>> SearchAsync(string? title, string? description, DateTime? startDate, DateTime? endDate, int page, int pageSize)
    {
        try
        {
            // Ensure minimum values and handle 0-based vs 1-based page indexing
            var normalizedPage = Math.Max(0, page);
            var normalizedPageSize = Math.Max(1, pageSize);
            var skip = normalizedPage * normalizedPageSize;
            
            var tasks = await _repository.SearchAsync(title, description, startDate, endDate, skip, normalizedPageSize);
            
            return tasks.Select(t => new TaskDto
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status.ToString(),
                CreatedAt = t.CreatedAt,
                CreatedById = t.CreatedById
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching tasks");
            return Enumerable.Empty<TaskDto>();
        }
    }

    public async Task<string> ProcessFileAsync(Stream fileStream, string fileName)
    {
        try
        {
            using var reader = new StreamReader(fileStream);
            var content = await reader.ReadToEndAsync();
            
            _logger.LogInformation("Processed file {FileName} with {ContentLength} characters", fileName, content.Length);
            
            return $"File '{fileName}' processed successfully. Content length: {content.Length} characters.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file {FileName}", fileName);
            throw;
        }
    }
}