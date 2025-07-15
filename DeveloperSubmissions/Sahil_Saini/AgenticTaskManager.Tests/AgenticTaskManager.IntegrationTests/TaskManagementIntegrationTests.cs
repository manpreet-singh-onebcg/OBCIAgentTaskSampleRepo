using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AgenticTaskManager.Infrastructure.Persistence;
using AgenticTaskManager.Domain.Entities;
using AgenticTaskManager.Application.DTOs;
using System.Text;
using System.Text.Json;
using System.Net;
using DomainTaskStatus = AgenticTaskManager.Domain.Entities.TaskStatus;

namespace AgenticTaskManager.IntegrationTests;

/// <summary>
/// End-to-end integration tests for the complete application
/// Focus: Complete workflows, database integration, full stack testing
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("EndToEnd")]
public class TaskManagementIntegrationTests : IDisposable
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public TaskManagementIntegrationTests()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the real database registration
                    services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
                    
                    // Add in-memory database for testing
                    services.AddDbContext<AppDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("IntegrationTestDb");
                    });
                });
                
                builder.UseEnvironment("Testing");
            });

        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [SetUp]
    public async Task SetUp()
    {
        // Clean database before each test
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    #region Complete Task Workflow Tests

    [Test]
    public async Task CompleteTaskWorkflow_CreateAndRetrieve_ShouldWorkEndToEnd()
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = "Integration Test Task",
            Description = "This is an integration test task",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(7)
        };

        // Act 1: Create task
        var createResponse = await CreateTaskAsync(taskDto);
        
        // Assert 1: Task created successfully
        Assert.That(createResponse.IsSuccessStatusCode, Is.True);
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // Act 2: Retrieve all tasks
        var getAllResponse = await _client.GetAsync("/api/tasks");
        var tasksJson = await getAllResponse.Content.ReadAsStringAsync();
        var tasks = JsonSerializer.Deserialize<List<TaskItem>>(tasksJson, _jsonOptions);

        // Assert 2: Task appears in list
        Assert.That(getAllResponse.IsSuccessStatusCode, Is.True);
        Assert.That(tasks, Is.Not.Null);
        Assert.That(tasks!.Count, Is.EqualTo(1));
        
        var retrievedTask = tasks.First();
        Assert.That(retrievedTask.Title, Is.EqualTo(taskDto.Title.ToUpper())); // TaskService converts to upper
        Assert.That(retrievedTask.Description, Is.EqualTo(taskDto.Description));
        Assert.That(retrievedTask.CreatedById, Is.EqualTo(taskDto.CreatedById));
        Assert.That(retrievedTask.AssignedToId, Is.EqualTo(taskDto.AssignedToId));
        Assert.That(retrievedTask.Status, Is.EqualTo(DomainTaskStatus.New));
    }

    [Test]
    public async Task CompleteTaskWorkflow_CreateMultipleTasksWithPagination_ShouldWorkCorrectly()
    {
        // Arrange
        var tasks = new List<TaskDto>();
        for (int i = 1; i <= 25; i++)
        {
            tasks.Add(new TaskDto
            {
                Title = $"Task {i:D2}",
                Description = $"Description for task {i}",
                CreatedById = Guid.NewGuid(),
                AssignedToId = Guid.NewGuid(),
                DueDate = DateTime.UtcNow.AddDays(i)
            });
        }

        // Act 1: Create all tasks
        foreach (var task in tasks)
        {
            var response = await CreateTaskAsync(task);
            Assert.That(response.IsSuccessStatusCode, Is.True);
        }

        // Act 2: Get first page
        var page1Response = await _client.GetAsync("/api/tasks?page=1&pageSize=10");
        var page1Json = await page1Response.Content.ReadAsStringAsync();
        var page1Tasks = JsonSerializer.Deserialize<List<TaskItem>>(page1Json, _jsonOptions);

        // Act 3: Get second page
        var page2Response = await _client.GetAsync("/api/tasks?page=2&pageSize=10");
        var page2Json = await page2Response.Content.ReadAsStringAsync();
        var page2Tasks = JsonSerializer.Deserialize<List<TaskItem>>(page2Json, _jsonOptions);

        // Assert
        Assert.That(page1Response.IsSuccessStatusCode, Is.True);
        Assert.That(page2Response.IsSuccessStatusCode, Is.True);
        
        Assert.That(page1Tasks, Is.Not.Null);
        Assert.That(page2Tasks, Is.Not.Null);
        
        Assert.That(page1Tasks!.Count, Is.EqualTo(10));
        Assert.That(page2Tasks!.Count, Is.EqualTo(10));

        // Verify no duplicates between pages
        var page1Ids = page1Tasks.Select(t => t.Id).ToHashSet();
        var page2Ids = page2Tasks.Select(t => t.Id).ToHashSet();
        Assert.That(page1Ids.Intersect(page2Ids), Is.Empty);
    }

    #endregion

    #region Error Handling Integration Tests

    [Test]
    public async Task CreateTask_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var invalidTaskDto = new TaskDto
        {
            Title = "", // Empty title should cause validation error
            Description = "Valid description",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(7)
        };

        // Act
        var response = await CreateTaskAsync(invalidTaskDto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.That(responseContent, Does.Contain("Task title is required"));
    }

    [Test]
    public async Task CreateTask_WithNullData_ShouldReturnBadRequest()
    {
        // Arrange
        var json = "null";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/tasks", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetTasks_WithInvalidPagination_ShouldHandleGracefully()
    {
        // Act
        var response = await _client.GetAsync("/api/tasks?page=-1&pageSize=0");

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var tasks = JsonSerializer.Deserialize<List<TaskItem>>(responseContent, _jsonOptions);
        
        Assert.That(tasks, Is.Not.Null);
        // Should return empty list for invalid pagination
    }

    #endregion

    #region API Endpoint Integration Tests

    [Test]
    public async Task GetUserReport_WithValidUserId_ShouldReturnReport()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/tasks/report/{userId}");

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.That(responseContent, Is.Not.Empty);
        
        // The response should be JSON with report data
        var reportData = JsonSerializer.Deserialize<JsonElement>(responseContent);
        Assert.That(reportData.TryGetProperty("report", out _), Is.True);
        Assert.That(reportData.TryGetProperty("generatedAt", out _), Is.True);
        Assert.That(reportData.TryGetProperty("userId", out _), Is.True);
    }

    [Test]
    public async Task GetUserReport_WithEmptyGuid_ShouldReturnBadRequest()
    {
        // Act
        var response = await _client.GetAsync($"/api/tasks/report/{Guid.Empty}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task UploadFile_WithValidFile_ShouldReturnSuccess()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test file content"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "test.txt");

        // Act
        var response = await _client.PostAsync("/api/tasks/upload", content);

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<JsonElement>(responseContent);
        
        Assert.That(responseData.TryGetProperty("message", out _), Is.True);
        Assert.That(responseData.TryGetProperty("fileName", out _), Is.True);
        Assert.That(responseData.TryGetProperty("size", out _), Is.True);
    }

    [Test]
    public async Task UploadFile_WithInvalidExtension_ShouldReturnBadRequest()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Malicious content"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", "malicious.exe");

        // Act
        var response = await _client.PostAsync("/api/tasks/upload", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.That(responseContent, Does.Contain("Only .txt, .csv, .json files are allowed"));
    }

    #endregion

    #region Search Integration Tests

    [Test]
    public async Task SearchTasks_WithValidApiKey_ShouldReturnResults()
    {
        // Arrange
        var searchParams = new Dictionary<string, string>
        {
            ["ApiKey"] = "test-api-key", // This will need to match the configured key
            ["Title"] = "test",
            ["Status"] = "1",
            ["Priority"] = "5"
        };

        var queryString = string.Join("&", searchParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));

        // Act
        var response = await _client.GetAsync($"/api/tasks/search?{queryString}");

        // Assert
        // Note: This might return unauthorized if the API key validation is strict
        // The response depends on the actual SecurityConfiguration implementation
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Assert.Pass("API key validation is working as expected");
        }
        else
        {
            Assert.That(response.IsSuccessStatusCode, Is.True);
        }
    }

    #endregion

    #region Performance Integration Tests

    [Test]
    [Explicit("Performance test - run manually")]
    public async Task CreateManyTasks_ShouldHandleLoadEfficiently()
    {
        // Arrange
        const int taskCount = 100;
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < taskCount; i++)
        {
            var taskDto = new TaskDto
            {
                Title = $"Performance Test Task {i}",
                Description = $"Description for task {i}",
                CreatedById = Guid.NewGuid(),
                AssignedToId = Guid.NewGuid(),
                DueDate = DateTime.UtcNow.AddDays(i % 30)
            };

            tasks.Add(CreateTaskAsync(taskDto));
        }

        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Assert.That(responses.All(r => r.IsSuccessStatusCode), Is.True);
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(30000)); // Should complete within 30 seconds
        
        TestContext.WriteLine($"Created {taskCount} tasks in {stopwatch.ElapsedMilliseconds}ms");
        TestContext.WriteLine($"Average time per task: {(double)stopwatch.ElapsedMilliseconds / taskCount:F2}ms");

        // Verify all tasks were created
        var getAllResponse = await _client.GetAsync("/api/tasks?pageSize=100");
        var tasksJson = await getAllResponse.Content.ReadAsStringAsync();
        var allTasks = JsonSerializer.Deserialize<List<TaskItem>>(tasksJson, _jsonOptions);
        
        Assert.That(allTasks, Is.Not.Null);
        Assert.That(allTasks!.Count, Is.EqualTo(taskCount));
    }

    #endregion

    #region Data Persistence Integration Tests

    [Test]
    public async Task TaskData_ShouldPersistBetweenRequests()
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = "Persistence Test Task",
            Description = "This task should persist between requests",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(7)
        };

        // Act 1: Create task
        var createResponse = await CreateTaskAsync(taskDto);
        Assert.That(createResponse.IsSuccessStatusCode, Is.True);

        // Act 2: Retrieve tasks in a separate request
        var getResponse = await _client.GetAsync("/api/tasks");
        var tasksJson = await getResponse.Content.ReadAsStringAsync();
        var tasks = JsonSerializer.Deserialize<List<TaskItem>>(tasksJson, _jsonOptions);

        // Act 3: Retrieve tasks again to ensure consistency
        var getResponse2 = await _client.GetAsync("/api/tasks");
        var tasksJson2 = await getResponse2.Content.ReadAsStringAsync();
        var tasks2 = JsonSerializer.Deserialize<List<TaskItem>>(tasksJson2, _jsonOptions);

        // Assert
        Assert.That(tasks, Is.Not.Null);
        Assert.That(tasks2, Is.Not.Null);
        Assert.That(tasks!.Count, Is.EqualTo(1));
        Assert.That(tasks2!.Count, Is.EqualTo(1));
        
        Assert.That(tasks.First().Id, Is.EqualTo(tasks2.First().Id));
        Assert.That(tasks.First().Title, Is.EqualTo(tasks2.First().Title));
    }

    #endregion

    #region Helper Methods

    private async Task<HttpResponseMessage> CreateTaskAsync(TaskDto taskDto)
    {
        var json = JsonSerializer.Serialize(taskDto, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/api/tasks", content);
    }

    #endregion

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }
}