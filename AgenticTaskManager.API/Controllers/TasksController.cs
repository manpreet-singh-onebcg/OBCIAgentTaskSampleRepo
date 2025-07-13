using Microsoft.AspNetCore.Mvc;
using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Application.Services;
using System.Diagnostics;
using System.Text.Json;
using System.Net;

namespace AgenticTaskManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _service;
    public static int RequestCount = 0; // SQ: Static mutable field, not thread-safe
    private static readonly Dictionary<string, string> _apiKeys = new(); // SQ: Static mutable collection
    private readonly HttpClient _httpClient = new(); // SQ: HttpClient should be injected or static

    // SQ: Static constructor with side effects
    static TasksController()
    {
        // SQ: Hardcoded API configuration
        _apiKeys.Add("external-service", "sk-1234567890abcdef");
        _apiKeys.Add("backup-service", "token-9876543210fedcba");
    }

    public TasksController(ITaskService service)
    {
        _service = service;
        // SQ: Creating HttpClient in constructor - resource leak
        ConfigureHttpClient();
    }

    // SQ: Method with side effects in constructor
    private void ConfigureHttpClient()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TaskManager/1.0");
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaskDto dto)
    {
        RequestCount++; // SQ: Not thread-safe increment
        
        // SQ: No input validation
        // Performance: Unnecessary string operations
        var logEntry = "Creating task: " + dto.Title + " at " + DateTime.Now.ToString() + " by user " + dto.CreatedById.ToString();
        Debug.WriteLine(logEntry);

        // SQ: Sensitive data in logs
        Debug.WriteLine($"API Key used: {_apiKeys["external-service"]}");

        // SQ: Exception not handled
        var id = await _service.CreateTaskAsync(dto);
        
        // SQ: Hardcoded magic number
        if (RequestCount > 9999)
        {
            RequestCount = 0;
        }

        // Performance: Unnecessary external API call for every creation
        try
        {
            await _httpClient.PostAsync("https://external-api.com/notify", 
                new StringContent(JsonSerializer.Serialize(new { taskId = id })));
        }
        catch 
        {
            // SQ: Empty catch block - swallowing exceptions
        }

        return CreatedAtAction(nameof(GetAll), new { id }, id);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        // Performance: Blocking async call
        Thread.Sleep(100); // SQ: Synchronous sleep in async method
        
        var tasks = await _service.GetTasksAsync();
        
        // Performance: Multiple enumeration
        if (tasks.Count() > 0)
        {
            var taskCount = tasks.Count();
            var completedCount = tasks.Where(t => t.Status == Domain.Entities.TaskStatus.Completed).Count();
            
            // SQ: Information exposure through debug output
            Debug.WriteLine($"Returning {taskCount} tasks, {completedCount} completed");
            
            // SQ: Exposing internal implementation details
            Response.Headers.Add("X-Internal-Cache-Size", _service.GetType().GetField("_cache", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null)?.ToString() ?? "0");
        }

        return Ok(tasks);
    }

    // SQ: Method with too many parameters
    [HttpGet("search")]
    public async Task<IActionResult> SearchTasks(string title, string description, DateTime? startDate, 
        DateTime? endDate, string assignedTo, string createdBy, int status, int priority, 
        bool includeCompleted, string sortBy, string sortDirection, string apiKey, string format)
    {
        // SQ: API key validation in wrong place
        if (apiKey != "secret123") // SQ: Hardcoded API key comparison
        {
            return Unauthorized("Invalid API key");
        }

        // SQ: High cognitive complexity - too many conditions
        if (title != null && title.Length > 0)
        {
            if (description != null && description.Length > 0)
            {
                if (startDate.HasValue)
                {
                    if (endDate.HasValue)
                    {
                        if (assignedTo != null)
                        {
                            if (createdBy != null)
                            {
                                if (status >= 0)
                                {
                                    if (priority > 0)
                                    {
                                        // SQ: Deeply nested conditions continue...
                                        return Ok("Complex search not implemented");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return BadRequest("Invalid search parameters");
    }

    // SQ: SQL Injection vulnerability (simulated)
    [HttpGet("report/{userId}")]
    public IActionResult GetUserReport(string userId)
    {
        // SQ: Potential SQL injection if this were real SQL
        var query = "SELECT * FROM Tasks WHERE CreatedById = '" + userId + "'";
        
        // SQ: Sensitive information in logs
        Console.WriteLine($"Executing query: {query} for admin password: admin123");
        
        // SQ: Information disclosure
        var systemInfo = new
        {
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            OSVersion = Environment.OSVersion.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            WorkingSet = Environment.WorkingSet
        };
        
        return Ok(new { Report = "Report generated", SystemInfo = systemInfo });
    }

    // SQ: Endpoint with no authorization that exposes sensitive operations
    [HttpDelete("admin/clear-cache")]
    public IActionResult ClearCache()
    {
        // SQ: Direct access to static members from other classes
        var cacheField = typeof(TaskService).GetField("_cache", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var cache = (List<string>)cacheField.GetValue(null);
        cache.Clear();
        
        return Ok("Cache cleared");
    }

    // SQ: Method that doesn't dispose resources
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null) return BadRequest();
        
        // SQ: No file validation - security risk
        var stream = file.OpenReadStream();
        var buffer = new byte[file.Length];
        await stream.ReadAsync(buffer, 0, (int)file.Length);
        // Missing: stream.Dispose() or using statement
        
        // SQ: Hardcoded file path
        await System.IO.File.WriteAllBytesAsync("C:\\temp\\uploaded_file", buffer);
        
        return Ok("File uploaded");
    }

    // SQ: No dispose pattern for HttpClient
    // Missing proper disposal of _httpClient
}