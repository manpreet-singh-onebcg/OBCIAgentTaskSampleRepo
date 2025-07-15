namespace AgenticTaskManager.Application.DTOs;

public class TaskSearchParametersDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? AssignedTo { get; set; }
    public string? CreatedBy { get; set; }
    public int Status { get; set; } = -1;
    public int Priority { get; set; } = 0;
    public bool IncludeCompleted { get; set; } = true;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
    public string? ApiKey { get; set; }
    public string? Format { get; set; }
}
