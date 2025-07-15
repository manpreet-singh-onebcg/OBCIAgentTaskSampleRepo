namespace AgenticTaskManager.Domain.Entities;

public enum TaskStatus { Pending, New, InProgress, Completed, Failed, Archived }

public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskStatus Status { get; set; }
    public bool IsCompleted { get; set; }
    public int Priority { get; set; } = 1;
    public string? AgentComment { get; set; }
    public Guid CreatedById { get; set; }
    public Guid? AssignedToId { get; set; }
    public int? AssignedActorId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}