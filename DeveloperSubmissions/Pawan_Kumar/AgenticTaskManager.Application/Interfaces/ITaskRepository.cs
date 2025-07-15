using AgenticTaskManager.Domain.Entities;

namespace AgenticTaskManager.Application.Interfaces;

public interface ITaskRepository
{
    Task<TaskItem> AddAsync(TaskItem task);
    Task<IEnumerable<TaskItem>> GetAllAsync();
    Task<TaskItem?> GetByIdAsync(int id);
    Task<TaskItem?> UpdateAsync(TaskItem task);
    Task<bool> DeleteAsync(int id);
    Task<IEnumerable<TaskItem>> SearchAsync(string? title, string? description, DateTime? startDate, DateTime? endDate, int skip, int take);
    Task<IEnumerable<TaskItem>> GetTasksByActorIdAsync(int actorId);
    Task<IEnumerable<TaskItem>> GetCompletedTasksAsync();
}
