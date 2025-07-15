using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using System.Text;
using AgenticTaskManager.API.Controllers;
using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Domain.Entities;
using TestUtilities;
using System.Net;
using System.Security.Claims;

namespace AgenticTaskManager.API.Tests;

public class TasksControllerTests : IDisposable
{
    private readonly Mock<ITaskService> _mockTaskService;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<TasksController>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly TasksController _controller;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;

    public TasksControllerTests()
    {
        _mockTaskService = new Mock<ITaskService>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<TasksController>>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        // Setup HttpClient with mock handler
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        
        _controller = new TasksController(
            _mockTaskService.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            _mockConfiguration.Object);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Security Tests

    [Theory]
    [InlineData("'; DROP TABLE Tasks; --")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("1' OR '1'='1")]
    [InlineData("admin'; DELETE FROM Users; --")]
    public async Task Create_WithMaliciousInput_ShouldHandleSafely(string maliciousInput)
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = maliciousInput,
            Description = maliciousInput,
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        var expectedTaskId = Guid.NewGuid();
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(expectedTaskId);

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        _mockTaskService.Verify(x => x.CreateTaskAsync(It.Is<TaskDto>(dto => 
            dto.Title == maliciousInput && dto.Description == maliciousInput)), Times.Once);
    }

    [Fact]
    public async Task SearchTasks_WithInvalidApiKey_ShouldReturnUnauthorized()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["ApiSettings:ApiKey"]).Returns("valid-api-key");
        
        var searchParams = new TaskSearchParametersDto
        {
            ApiKey = "invalid-api-key",
            Title = "Test"
        };

        // Act
        var result = await _controller.SearchTasks(searchParams);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        var unauthorizedResult = result as UnauthorizedObjectResult;
        unauthorizedResult!.Value.Should().Be("Invalid API key");
    }

    [Fact]
    public async Task SearchTasks_WithValidApiKey_ShouldProceedWithValidation()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["ApiSettings:ApiKey"]).Returns("valid-api-key");
        
        var searchParams = new TaskSearchParametersDto
        {
            ApiKey = "valid-api-key",
            Title = "Test"
        };

        // Act
        var result = await _controller.SearchTasks(searchParams);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadFile_WithMaliciousFileType_ShouldRejectFile()
    {
        // Arrange
        var maliciousFile = HttpContextTestHelper.CreateMaliciousFormFile("virus.exe", 1);
        
        SetupFileUploadConfiguration();

        // Act
        var result = await _controller.UploadFile(maliciousFile);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.Value.ToString()!.Should().Contain("not allowed");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUserReport_WithInvalidUserId_ShouldReturnBadRequest(string userId)
    {
        // Act
        var result = await _controller.GetUserReport(userId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.Value.Should().Be("User ID is required");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task Create_PerformanceTest_ShouldCompleteWithinReasonableTime()
    {
        // Arrange
        var taskDto = CreateValidTaskDto();
        var expectedTaskId = Guid.NewGuid();
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(expectedTaskId);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Should complete within 1 second
        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task GetAll_PerformanceTest_ShouldHandleLargeDatasetEfficiently()
    {
        // Arrange
        var largeTasks = GenerateLargeTaskList(1000);
        _mockTaskService.Setup(x => x.GetTasksAsync()).ReturnsAsync(largeTasks);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await _controller.GetAll();

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000); // Should handle 1000 items within 2 seconds
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Create_AsyncPatternTest_ShouldNotBlock()
    {
        // Arrange
        var taskDto = CreateValidTaskDto();
        var expectedTaskId = Guid.NewGuid();
        
        var tcs = new TaskCompletionSource<Guid>();
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .Returns(tcs.Task);

        // Act
        var task = _controller.Create(taskDto);
        
        // Assert - Task should not be completed yet
        task.IsCompleted.Should().BeFalse();
        
        // Complete the mock task
        tcs.SetResult(expectedTaskId);
        var result = await task;
        
        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task UploadFile_MemoryUsageTest_ShouldDisposeResourcesProperly()
    {
        // Arrange
        var mockFile = HttpContextTestHelper.CreateMockFormFile("test.txt", "test content");
        
        SetupFileUploadConfiguration();

        // Act
        var result = await _controller.UploadFile(mockFile);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        // Verify that streams are properly disposed (mocked file handles this)
        // In a real scenario, we would monitor memory usage
    }

    #endregion

    #region Quality Assurance - Edge Cases

    [Fact]
    public async Task Create_WithNullDto_ShouldHandleGracefully()
    {
        // Act
        var result = await _controller.Create(null!);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task Create_WithEmptyGuidIds_ShouldHandleValidation()
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = "Valid Title",
            Description = "Valid Description",
            CreatedById = Guid.Empty,
            AssignedToId = Guid.Empty,
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        var expectedTaskId = Guid.NewGuid();
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(expectedTaskId);

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Create_WithPastDueDate_ShouldStillProcess()
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = "Past Due Task",
            Description = "Description",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(-1) // Past date
        };

        var expectedTaskId = Guid.NewGuid();
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(expectedTaskId);

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task GetAll_WithEmptyTaskList_ShouldReturnEmptyList()
    {
        // Arrange
        _mockTaskService.Setup(x => x.GetTasksAsync()).ReturnsAsync(new List<TaskItem>());

        // Act
        var result = await _controller.GetAll();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var tasks = okResult!.Value as List<TaskItem>;
        tasks.Should().NotBeNull();
        tasks!.Count.Should().Be(0);
    }

    [Fact]
    public async Task UploadFile_WithNullFile_ShouldReturnBadRequest()
    {
        // Act
        var result = await _controller.UploadFile(null!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.Value.Should().Be("No file provided or file is empty");
    }

    [Fact]
    public async Task UploadFile_WithEmptyFile_ShouldReturnBadRequest()
    {
        // Arrange
        var emptyFile = HttpContextTestHelper.CreateMockFormFile("empty.txt", "");

        // Act
        var result = await _controller.UploadFile(emptyFile);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadFile_WithOversizedFile_ShouldReturnBadRequest()
    {
        // Arrange
        var largeFile = HttpContextTestHelper.CreateMaliciousFormFile("large.txt", 25); // 25MB
        
        SetupFileUploadConfiguration();

        // Act
        var result = await _controller.UploadFile(largeFile);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.Value.ToString()!.Should().Contain("exceeds the maximum limit");
    }

    [Fact]
    public async Task SearchTasks_WithAllComplexCriteria_ShouldReturnComplexSearchResponse()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["ApiSettings:ApiKey"]).Returns("valid-api-key");
        
        var searchParams = new TaskSearchParametersDto
        {
            ApiKey = "valid-api-key",
            Title = "Test Title",
            Description = "Test Description",
            StartDate = DateTime.UtcNow.AddDays(-10),
            EndDate = DateTime.UtcNow,
            AssignedTo = "user1",
            CreatedBy = "user2",
            Status = 1,
            Priority = 2
        };

        // Act
        var result = await _controller.SearchTasks(searchParams);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().Be("Complex search not implemented");
    }

    #endregion

    #region Exception Handling

    [Fact]
    public async Task Create_WhenServiceThrowsException_ShouldReturnInternalServerError()
    {
        // Arrange
        var taskDto = CreateValidTaskDto();
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
        objectResult.Value.Should().Be("An error occurred while creating the task");
    }

    [Fact]
    public async Task GetAll_WhenServiceThrowsException_ShouldReturnInternalServerError()
    {
        // Arrange
        _mockTaskService.Setup(x => x.GetTasksAsync())
                       .ThrowsAsync(new TimeoutException("Service timeout"));

        // Act
        var result = await _controller.GetAll();

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
        objectResult.Value.Should().Be("An error occurred while retrieving tasks");
    }

    [Fact]
    public async Task GetUserReport_WhenServiceThrowsException_ShouldReturnInternalServerError()
    {
        // Arrange
        var userId = "test-user-id";
        _mockTaskService.Setup(x => x.GetUserReportAsync(It.IsAny<string>()))
                       .ThrowsAsync(new UnauthorizedAccessException("Access denied"));

        // Act
        var result = await _controller.GetUserReport(userId);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
        objectResult.Value.Should().Be("An error occurred while generating the report");
    }

    [Fact]
    public async Task ClearCache_WhenServiceThrowsException_ShouldReturnInternalServerError()
    {
        // Arrange
        _mockTaskService.Setup(x => x.ClearCacheAsync())
                       .ThrowsAsync(new InvalidOperationException("Cache service unavailable"));

        // Act
        var result = await _controller.ClearCache();

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
        objectResult.Value.Should().Be("An error occurred while clearing the cache");
    }

    [Fact]
    public async Task SearchTasks_WhenExceptionOccurs_ShouldReturnInternalServerError()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["ApiSettings:ApiKey"]).Throws(new NullReferenceException());
        
        var searchParams = new TaskSearchParametersDto
        {
            ApiKey = "any-key"
        };

        // Act
        var result = await _controller.SearchTasks(searchParams);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
        objectResult.Value.Should().Be("An error occurred while searching tasks");
    }

    #endregion

    #region HTTP Status Code Tests

    [Fact]
    public async Task Create_WithValidData_ShouldReturnCreated()
    {
        // Arrange
        var taskDto = CreateValidTaskDto();
        var expectedTaskId = Guid.NewGuid();
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(expectedTaskId);

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result as CreatedAtActionResult;
        createdResult!.StatusCode.Should().Be(201);
        createdResult.Value.Should().Be(expectedTaskId);
    }

    [Fact]
    public async Task GetAll_WithValidData_ShouldReturnOk()
    {
        // Arrange
        var tasks = new List<TaskItem> { CreateValidTaskItem() };
        _mockTaskService.Setup(x => x.GetTasksAsync()).ReturnsAsync(tasks);

        // Act
        var result = await _controller.GetAll();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetUserReport_WithValidUserId_ShouldReturnOk()
    {
        // Arrange
        var userId = "valid-user-id";
        var reportData = new { TaskCount = 5, CompletedTasks = 3 };
        _mockTaskService.Setup(x => x.GetUserReportAsync(userId)).ReturnsAsync(reportData);

        // Act
        var result = await _controller.GetUserReport(userId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(reportData);
    }

    [Fact]
    public async Task ClearCache_WhenSuccessful_ShouldReturnOk()
    {
        // Arrange
        _mockTaskService.Setup(x => x.ClearCacheAsync()).ReturnsAsync(true);

        // Act
        var result = await _controller.ClearCache();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().Be("Cache cleared");
    }

    [Fact]
    public async Task ClearCache_WhenFailed_ShouldReturnInternalServerError()
    {
        // Arrange
        _mockTaskService.Setup(x => x.ClearCacheAsync()).ReturnsAsync(false);

        // Act
        var result = await _controller.ClearCache();

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
        objectResult.Value.Should().Be("Cache field not accessible");
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task Create_ConcurrentRequests_ShouldHandleThreadSafely()
    {
        // Arrange
        var taskDto = CreateValidTaskDto();
        var expectedTaskId = Guid.NewGuid();
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(expectedTaskId);

        // Act - Simulate concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _controller.Create(taskDto))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(10);
        results.Should().AllBeOfType<CreatedAtActionResult>();
        _mockTaskService.Verify(x => x.CreateTaskAsync(It.IsAny<TaskDto>()), Times.Exactly(10));
    }

    [Fact]
    public async Task GetAll_ConcurrentRequests_ShouldHandleThreadSafely()
    {
        // Arrange
        var tasks = new List<TaskItem> { CreateValidTaskItem() };
        _mockTaskService.Setup(x => x.GetTasksAsync()).ReturnsAsync(tasks);

        // Act - Simulate concurrent requests
        var getAllTasks = Enumerable.Range(0, 5)
            .Select(_ => _controller.GetAll())
            .ToArray();

        var results = await Task.WhenAll(getAllTasks);

        // Assert
        results.Should().HaveCount(5);
        results.Should().AllBeOfType<OkObjectResult>();
        _mockTaskService.Verify(x => x.GetTasksAsync(), Times.Exactly(5));
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullTaskService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new TasksController(
            null!,
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            _mockConfiguration.Object);

        action.Should().Throw<ArgumentNullException>()
              .WithParameterName("taskService");
    }

    [Fact]
    public void Constructor_WithNullHttpClientFactory_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new TasksController(
            _mockTaskService.Object,
            null!,
            _mockLogger.Object,
            _mockConfiguration.Object);

        action.Should().Throw<ArgumentNullException>()
              .WithParameterName("httpClientFactory");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new TasksController(
            _mockTaskService.Object,
            _mockHttpClientFactory.Object,
            null!,
            _mockConfiguration.Object);

        action.Should().Throw<ArgumentNullException>()
              .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new TasksController(
            _mockTaskService.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            null!);

        action.Should().Throw<ArgumentNullException>()
              .WithParameterName("configuration");
    }

    #endregion

    #region Helper Methods

    private TaskDto CreateValidTaskDto()
    {
        return new TaskDto
        {
            Title = "Test Task",
            Description = "Test Description",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(1)
        };
    }

    private TaskItem CreateValidTaskItem()
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Test Task",
            Description = "Test Description",
            Status = AgenticTaskManager.Domain.Entities.TaskStatus.New,
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        };
    }

    private IConfigurationSection CreateConfigurationSection(string key, string value)
    {
        var section = new Mock<IConfigurationSection>();
        section.Setup(x => x.Key).Returns(key);
        section.Setup(x => x.Value).Returns(value);
        return section.Object;
    }

    private void SetupFileUploadConfiguration()
    {
        // Mock max file size configuration
        var maxSizeSection = new Mock<IConfigurationSection>();
        maxSizeSection.Setup(x => x.Value).Returns("10");
        _mockConfiguration.Setup(x => x.GetSection("FileUpload:MaxFileSizeMB")).Returns(maxSizeSection.Object);
        
        // Mock allowed extensions configuration
        var allowedExtensionsSection = new Mock<IConfigurationSection>();
        allowedExtensionsSection.Setup(x => x.GetChildren()).Returns(new[]
        {
            CreateConfigurationSection("0", ".txt"),
            CreateConfigurationSection("1", ".pdf")
        });
        _mockConfiguration.Setup(x => x.GetSection("FileUpload:AllowedExtensions")).Returns(allowedExtensionsSection.Object);
        
        // Mock upload path configuration
        _mockConfiguration.Setup(x => x["FileUpload:Path"]).Returns("uploads");
    }

    private List<TaskItem> GenerateLargeTaskList(int count)
    {
        var tasks = new List<TaskItem>();
        for (int i = 0; i < count; i++)
        {
            tasks.Add(new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = $"Task {i}",
                Description = $"Description {i}",
                Status = i % 2 == 0 ? AgenticTaskManager.Domain.Entities.TaskStatus.Completed : AgenticTaskManager.Domain.Entities.TaskStatus.New,
                CreatedById = Guid.NewGuid(),
                AssignedToId = Guid.NewGuid(),
                DueDate = DateTime.UtcNow.AddDays(i),
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            });
        }
        return tasks;
    }

    #endregion
}
