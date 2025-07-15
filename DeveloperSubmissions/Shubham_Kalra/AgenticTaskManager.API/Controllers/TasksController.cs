using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Application.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AgenticTaskManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TasksController> _logger;
    private readonly IConfiguration _configuration;

    public TasksController(
        ITaskService taskService,
        IHttpClientFactory httpClientFactory,
        ILogger<TasksController> logger,
        IConfiguration configuration)
    {
        _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaskDto dto)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid model state for task creation");
            return BadRequest(ModelState);
        }

        try
        {
            var userId = dto.CreatedById.ToString();
            _logger.LogInformation("Creating task: {Title} by user {UserId}", dto.Title, userId);

            var taskId = await _taskService.CreateTaskAsync(dto);
            
            // Optional: Send notification asynchronously without blocking the response
            _ = Task.Run(async () => await SendNotificationAsync(taskId));

            return CreatedAtAction(nameof(GetAll), new { id = taskId }, taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task");
            return StatusCode(500, "An error occurred while creating the task");
        }
    }

    private async Task SendNotificationAsync(Guid taskId)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "TaskManager/1.0");
            
            var notificationUrl = _configuration["ExternalServices:NotificationUrl"];
            if (!string.IsNullOrEmpty(notificationUrl))
            {
                var content = new StringContent($"{{\"taskId\": \"{taskId}\"}}", Encoding.UTF8, "application/json");
                await httpClient.PostAsync(notificationUrl, content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification for task {TaskId}", taskId);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var tasks = await _taskService.GetTasksAsync();
            
            var taskList = tasks.ToList();
            
            if (taskList.Count > 0)
            {
                var completedCount = taskList.Count(t => t.Status == Domain.Entities.TaskStatus.Completed);
                _logger.LogInformation("Returning {TaskCount} tasks, {CompletedCount} completed", 
                    taskList.Count, completedCount);
            }

            return Ok(taskList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tasks");
            return StatusCode(500, "An error occurred while retrieving tasks");
        }
    }


    [HttpGet("search")]
    public async Task<IActionResult> SearchTasks([FromQuery] TaskSearchParametersDto searchParams)
    {
        try
        {
            // Security: Validate API key from configuration (optimized)
            var validApiKey = _configuration["ApiSettings:ApiKey"];
            if (searchParams.ApiKey != validApiKey)
            {
                _logger.LogWarning("Invalid API key attempt in search");
                return Unauthorized("Invalid API key");
            }

            // Business Logic: Check if all complex criteria are provided (moved from DTO)
            bool hasValidComplexCriteria = !string.IsNullOrEmpty(searchParams.Title) &&
                                         !string.IsNullOrEmpty(searchParams.Description) &&
                                         searchParams.StartDate.HasValue &&
                                         searchParams.EndDate.HasValue &&
                                         !string.IsNullOrEmpty(searchParams.AssignedTo) &&
                                         !string.IsNullOrEmpty(searchParams.CreatedBy) &&
                                         searchParams.Status >= 0 &&
                                         searchParams.Priority > 0;

            if (hasValidComplexCriteria)
            {
                return Ok("Complex search not implemented");
            }

            _logger.LogWarning("Search parameters did not meet complex criteria requirements");
            return BadRequest("Invalid search parameters");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during task search");
            return StatusCode(500, "An error occurred while searching tasks");
        }
    }

    [HttpGet("report/{userId}")]
    public async Task<IActionResult> GetUserReport(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest("User ID is required");
            }
            var reportResult = await _taskService.GetUserReportAsync(userId);
            
            _logger.LogInformation("Generated report for user {UserId}", userId);
            return Ok(reportResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating user report for {UserId}", userId);
            return StatusCode(500, "An error occurred while generating the report");
        }
    }

    [HttpDelete("admin/clear-cache")]
    public async Task<IActionResult> ClearCache()
    {
        try
        {
            var result = await _taskService.ClearCacheAsync();
            
            if (result)
            {
                return Ok("Cache cleared");
            }
            else
            {
                _logger.LogWarning("Cache clearing operation failed");
                return StatusCode(500, "Cache field not accessible");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
            return StatusCode(500, "An error occurred while clearing the cache");
        }
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided or file is empty");
            }

            // Get file size limit from configuration
            var maxFileSizeMB = _configuration.GetValue<int>("FileUpload:MaxFileSizeMB", 10);
            var maxFileSize = maxFileSizeMB * 1024 * 1024; // Convert MB to bytes
            var allowedExtensions = _configuration.GetSection("FileUpload:AllowedExtensions").Get<string[]>() 
                ?? new[] { ".txt", ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
            
            if (file.Length > maxFileSize)
            {
                return BadRequest($"File size exceeds the maximum limit of {maxFileSizeMB}MB");
            }

            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest($"File type {fileExtension} is not allowed");
            }

            // Security: Ensure upload path is not null and create directory safely
            var uploadsPath = _configuration["FileUpload:Path"];
            if (!string.IsNullOrEmpty(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            } 
            var fileName = "uploaded_file"; 
            var filePath = Path.Combine(uploadsPath, fileName);

            await using var stream = file.OpenReadStream();
            await using var fileStream = new FileStream(filePath, FileMode.Create);
            await stream.CopyToAsync(fileStream);

            _logger.LogInformation("File uploaded: {FileName}", fileName);

            return Ok("File uploaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file");
            return StatusCode(500, "An error occurred while uploading the file");
        }
    }
}