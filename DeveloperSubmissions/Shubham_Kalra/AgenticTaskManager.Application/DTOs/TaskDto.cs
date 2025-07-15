namespace AgenticTaskManager.Application.DTOs;

public class TaskDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid CreatedById { get; set; }
    public Guid AssignedToId { get; set; }
    public DateTime DueDate { get; set; }
}