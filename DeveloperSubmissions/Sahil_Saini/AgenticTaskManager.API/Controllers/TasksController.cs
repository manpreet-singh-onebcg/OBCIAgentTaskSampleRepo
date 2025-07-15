using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AgenticTaskManager.API.DTOs;
using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Domain.Entities;
using AgenticTaskManager.Infrastructure.Configuration;
using AgenticTaskManager.Infrastructure.Security;
using DomainTaskStatus = AgenticTaskManager.Domain.Entities.TaskStatus;

namespace AgenticTaskManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _service;
    private readonly SecurityConfiguration _config;
    private readonly SecurityHelper _securityHelper;
    private readonly ILogger<TasksController> _logger;
    private readonly HttpClient _httpClient;

    public TasksController(
        ITaskService service, 
        SecurityConfiguration config,
        SecurityHelper securityHelper,
        ILogger<TasksController> logger,
        HttpClient httpClient)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _securityHelper = securityHelper ?? throw new ArgumentNullException(nameof(securityHelper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaskDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (dto == null)
        {
            _logger.LogWarning("Create task called with null DTO");
            return BadRequest("Task data is required");
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return BadRequest("Task title is required");
        }

        try
        {
            _logger.LogInformation("Creating task with title: {TaskTitle} for user: {UserId}", 
                dto.Title, dto.CreatedById);

            var id = await _service.CreateTaskAsync(dto);
            
            await NotifyExternalServiceAsync(id);

            _logger.LogInformation("Successfully created task with ID: {TaskId}", id);
            return CreatedAtAction(nameof(GetAll), new { id }, id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create task for user: {UserId}", dto.CreatedById);
            return StatusCode(500, "An error occurred while creating the task");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50; // Limit max page size
            
            var tasks = await _service.GetTasksAsync(page, pageSize);
            
            if (tasks?.Any() == true)
            {
                var taskCount = tasks.Count;
                var completedCount = 0;
                
                // Fixed: Single pass enumeration to avoid multiple enumeration performance issue
                foreach (var task in tasks)
                {
                    if (task.Status == DomainTaskStatus.Completed)
                        completedCount++;
                }
                
                _logger.LogDebug("Returning {TaskCount} tasks, {CompletedCount} completed (Page {Page}, PageSize {PageSize})", 
                    taskCount, completedCount, page, pageSize);
                
                Response.Headers.Append("X-Total-Count", taskCount.ToString());
                Response.Headers.Append("X-Completed-Count", completedCount.ToString());
                Response.Headers.Append("X-Page", page.ToString());
                Response.Headers.Append("X-Page-Size", pageSize.ToString());
            }

            return Ok(tasks ?? new List<TaskItem>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve tasks for page {Page}, pageSize {PageSize}", page, pageSize);
            return StatusCode(500, "An error occurred while retrieving tasks");
        }
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchTasks([FromQuery] TaskSearchRequest searchRequest)
    {
        if (!ValidateApiKey(searchRequest.ApiKey))
        {
            _logger.LogWarning("Invalid API key provided for search operation");
            return Unauthorized("Invalid API key");
        }

        try
        {
            var validationResult = ValidateSearchParameters(searchRequest);
            if (validationResult != null)
            {
                return validationResult;
            }

            _logger.LogInformation("Performing task search with parameters: {SearchParams}", 
                JsonSerializer.Serialize(new { searchRequest.Title, searchRequest.Status, searchRequest.Priority }));

            await Task.Delay(1);
            return Ok("Search functionality to be implemented with secure parameters");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform task search");
            return StatusCode(500, "An error occurred during search");
        }
    }

    [HttpGet("report/{userId:guid}")]
    public async Task<IActionResult> GetUserReport(Guid userId)
    {
        try
        {
            if (userId == Guid.Empty)
            {
                return BadRequest("Valid user ID is required");
            }

            _logger.LogInformation("Generating report for user: {UserId}", userId);

            var report = await GenerateUserReportAsync(userId);
            
            return Ok(new { 
                Report = report,
                GeneratedAt = DateTime.UtcNow,
                UserId = userId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate report for user: {UserId}", userId);
            return StatusCode(500, "An error occurred while generating the report");
        }
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is required");
        }

        const long maxFileSize = 10 * 1024 * 1024;
        var allowedExtensions = new[] { ".txt", ".csv", ".json" };
        
        if (file.Length > maxFileSize)
        {
            return BadRequest($"File size cannot exceed {maxFileSize / (1024 * 1024)}MB");
        }

        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest($"Only {string.Join(", ", allowedExtensions)} files are allowed");
        }

        try
        {
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            Directory.CreateDirectory(uploadsDir);
            
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsDir, fileName);

            await using var fileStream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(fileStream);

            _logger.LogInformation("File uploaded successfully: {FileName} -> {FilePath}", 
                file.FileName, fileName);

            return Ok(new { 
                Message = "File uploaded successfully", 
                FileName = fileName,
                Size = file.Length 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file: {FileName}", file.FileName);
            return StatusCode(500, "An error occurred while uploading the file");
        }
    }

    private bool ValidateApiKey(string? providedApiKey)
    {
        if (string.IsNullOrEmpty(providedApiKey))
            return false;

        try
        {
            var expectedApiKey = _config.GetApiKey("ExternalService");
            
            if (providedApiKey.Length != expectedApiKey.Length)
                return false;

            var providedBytes = System.Text.Encoding.UTF8.GetBytes(providedApiKey);
            var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expectedApiKey);

            int result = 0;
            for (int i = 0; i < expectedBytes.Length; i++)
            {
                result |= providedBytes[i] ^ expectedBytes[i];
            }

            return result == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate API key");
            return false;
        }
    }

    private IActionResult? ValidateSearchParameters(TaskSearchRequest request)
    {
        var errors = new List<string>();

        if (!string.IsNullOrEmpty(request.Title) && request.Title.Length > 100)
            errors.Add("Title cannot exceed 100 characters");

        if (!string.IsNullOrEmpty(request.Description) && request.Description.Length > 500)
            errors.Add("Description cannot exceed 500 characters");

        if (request.StartDate.HasValue && request.EndDate.HasValue && 
            request.StartDate > request.EndDate)
            errors.Add("Start date cannot be after end date");

        if (request.Status < 0 || request.Status > 10)
            errors.Add("Status must be between 0 and 10");

        if (request.Priority < 0 || request.Priority > 10)
            errors.Add("Priority must be between 0 and 10");

        return errors.Any() ? BadRequest(string.Join("; ", errors)) : null;
    }

    private async Task NotifyExternalServiceAsync(Guid taskId)
    {
        try
        {
            var externalApiUrl = _config.GetApiKey("ExternalServiceUrl");
            if (string.IsNullOrEmpty(externalApiUrl))
            {
                _logger.LogDebug("External service URL not configured, skipping notification");
                return;
            }

            var payload = JsonSerializer.Serialize(new { TaskId = taskId, NotifiedAt = DateTime.UtcNow });
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{externalApiUrl}/notify", content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Successfully notified external service for task: {TaskId}", taskId);
            }
            else
            {
                _logger.LogWarning("External service notification failed with status: {StatusCode}", 
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify external service for task: {TaskId}", taskId);
        }
    }

    private async Task<string> GenerateUserReportAsync(Guid userId)
    {
        await Task.Delay(1);
        return $"Report for user {userId} generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
    }
}