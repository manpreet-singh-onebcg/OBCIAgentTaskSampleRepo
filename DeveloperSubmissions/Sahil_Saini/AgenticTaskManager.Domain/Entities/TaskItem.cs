namespace AgenticTaskManager.Domain.Entities;

public enum TaskStatus { New, InProgress, Completed, Failed }

public class TaskItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskStatus Status { get; set; }
    public string? AgentComment { get; set; }
    public Guid CreatedById { get; set; }
    public Guid AssignedToId { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}