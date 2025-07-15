namespace AgenticTaskManager.Domain.Entities;

public enum ActorType { HumanUser, AIAgent }

public class Actor
{
    public int Id { get; set; }
    public Guid ActorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public ActorType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}