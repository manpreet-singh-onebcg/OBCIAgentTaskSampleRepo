using System.ComponentModel.DataAnnotations;

namespace AgenticTaskManager.Application.DTOs;

public class TaskDto
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;
    
    public string Status { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime? UpdatedAt { get; set; }
    
    [Required]
    public Guid CreatedById { get; set; }
    
    public Guid? AssignedToId { get; set; }
    
    public DateTime? DueDate { get; set; }
}