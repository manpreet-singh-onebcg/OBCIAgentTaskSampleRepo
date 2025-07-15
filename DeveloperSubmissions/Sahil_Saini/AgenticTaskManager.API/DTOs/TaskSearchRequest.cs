using System.ComponentModel.DataAnnotations;

namespace AgenticTaskManager.API.DTOs;

/// <summary>
/// Data transfer object for task search requests
/// </summary>
public class TaskSearchRequest
{
    [StringLength(100)]
    public string? Title { get; set; }
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    
    [StringLength(50)]
    public string? AssignedTo { get; set; }
    
    [StringLength(50)]
    public string? CreatedBy { get; set; }
    
    [Range(0, 10)]
    public int Status { get; set; }
    
    [Range(0, 10)]
    public int Priority { get; set; }
    
    public bool IncludeCompleted { get; set; }
    
    [StringLength(20)]
    public string? SortBy { get; set; }
    
    [StringLength(4)]
    public string? SortDirection { get; set; }
    
    [Required]
    public string ApiKey { get; set; } = string.Empty;
    
    [StringLength(10)]
    public string? Format { get; set; }
}