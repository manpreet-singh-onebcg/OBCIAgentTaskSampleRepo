using AgenticTaskManager.Domain.Entities;

namespace AgenticTaskManager.Application.Interfaces;

public interface ITaskRepository
{
    Task AddAsync(TaskItem task);
    Task<List<TaskItem>> GetAllAsync();
    Task<TaskItem?> GetByIdAsync(Guid id);
    Task<List<TaskItem>> SearchAsync(object searchParameters);
    Task<List<TaskItem>> GetTasksByUserIdAsync(string userId);
}
