namespace AgenticTaskManager.Domain.Entities;

public enum ActorType { HumanUser, AIAgent }

public class Actor
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ActorType Type { get; set; }
}