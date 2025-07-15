using Microsoft.AspNetCore.Mvc;
using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Application.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace AgenticTaskManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ILogger<TasksController> _logger;

    public TasksController(ITaskService taskService, ILogger<TasksController> logger)
    {
        _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody][Required] TaskDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Input validation
            if (dto == null)
            {
                return BadRequest("Task data is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.Title) || dto.Title.Length > 200)
            {
                return BadRequest("Title is required and must be less than 200 characters.");
            }

            _logger.LogInformation("Creating task with title: {Title}", dto.Title);

            var result = await _taskService.CreateAsync(dto);
            
            if (result == null)
            {
                return StatusCode(500, "Failed to create task.");
            }

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task");
            return StatusCode(500, "An error occurred while creating the task.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            _logger.LogInformation("Retrieving all tasks");
            
            var tasks = await _taskService.GetAllAsync();
            
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tasks");
            return StatusCode(500, "An error occurred while retrieving tasks.");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            if (id <= 0)
            {
                return BadRequest("Invalid task ID.");
            }

            var task = await _taskService.GetByIdAsync(id);
            
            if (task == null)
            {
                return NotFound($"Task with ID {id} not found.");
            }

            return Ok(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving task with ID {TaskId}", id);
            return StatusCode(500, "An error occurred while retrieving the task.");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody][Required] TaskDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id <= 0)
            {
                return BadRequest("Invalid task ID.");
            }

            if (dto == null)
            {
                return BadRequest("Task data is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.Title) || dto.Title.Length > 200)
            {
                return BadRequest("Title is required and must be less than 200 characters.");
            }

            _logger.LogInformation("Updating task with ID: {TaskId}", id);

            var result = await _taskService.UpdateAsync(id, dto);
            
            if (result == null)
            {
                return NotFound($"Task with ID {id} not found.");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task with ID {TaskId}", id);
            return StatusCode(500, "An error occurred while updating the task.");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            if (id <= 0)
            {
                return BadRequest("Invalid task ID.");
            }

            _logger.LogInformation("Deleting task with ID: {TaskId}", id);

            var success = await _taskService.DeleteAsync(id);
            
            if (!success)
            {
                return NotFound($"Task with ID {id} not found.");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting task with ID {TaskId}", id);
            return StatusCode(500, "An error occurred while deleting the task.");
        }
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchTasks(
        [FromQuery] string? title = null,
        [FromQuery] string? description = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            // Input validation
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 10;

            if (startDate.HasValue && endDate.HasValue && startDate > endDate)
            {
                return BadRequest("Start date cannot be after end date.");
            }

            _logger.LogInformation("Searching tasks with filters - Title: {Title}, Page: {Page}, PageSize: {PageSize}", 
                title, page, pageSize);

            var tasks = await _taskService.SearchAsync(title, description, startDate, endDate, page, pageSize);
            
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching tasks");
            return StatusCode(500, "An error occurred while searching tasks.");
        }
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile([Required] IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("File is required.");
            }

            // File validation
            const long maxFileSize = 5 * 1024 * 1024; // 5MB
            if (file.Length > maxFileSize)
            {
                return BadRequest("File size cannot exceed 5MB.");
            }

            var allowedExtensions = new[] { ".txt", ".csv", ".json" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest("Only .txt, .csv, and .json files are allowed.");
            }

            _logger.LogInformation("Processing file upload: {FileName}", file.FileName);

            using var stream = file.OpenReadStream();
            var result = await _taskService.ProcessFileAsync(stream, file.FileName);
            
            return Ok(new { Message = "File processed successfully", Result = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file upload");
            return StatusCode(500, "An error occurred while processing the file.");
        }
    }
}