using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Domain.Entities;

namespace AgenticTaskManager.Application.Interfaces;

public interface ITaskService
{
    Task<TaskDto?> CreateAsync(TaskDto taskDto);
    Task<IEnumerable<TaskDto>> GetAllAsync();
    Task<TaskDto?> GetByIdAsync(int id);
    Task<TaskDto?> UpdateAsync(int id, TaskDto taskDto);
    Task<bool> DeleteAsync(int id);
    Task<IEnumerable<TaskDto>> SearchAsync(string? title, string? description, DateTime? startDate, DateTime? endDate, int page, int pageSize);
    Task<string> ProcessFileAsync(Stream fileStream, string fileName);
}