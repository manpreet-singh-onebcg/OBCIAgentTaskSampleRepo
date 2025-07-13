using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Domain.Entities;

namespace AgenticTaskManager.Application.Interfaces;

public interface ITaskService
{
    Task<Guid> CreateTaskAsync(TaskDto taskDto);
    Task<List<TaskItem>> GetTasksAsync();
}