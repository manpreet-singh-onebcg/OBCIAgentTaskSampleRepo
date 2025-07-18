using AgenticTaskManager.Domain.Entities;

namespace AgenticTaskManager.Application.Interfaces;

public interface ITaskRepository
{
    Task AddAsync(TaskItem task);
    Task<List<TaskItem>> GetAllAsync();
}
