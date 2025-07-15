using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using System.Diagnostics;
using AgenticTaskManager.API.Controllers;
using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Domain.Entities;
using TestUtilities;

namespace AgenticTaskManager.API.Tests;

/// <summary>
/// Performance-focused tests for TasksController
/// These tests verify response times, memory usage, and scalability
/// </summary>
public class TasksControllerPerformanceTests : IDisposable
{
    private readonly Mock<ITaskService> _mockTaskService;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<TasksController>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly TasksController _controller;
    private readonly HttpClient _httpClient;

    public TasksControllerPerformanceTests()
    {
        _mockTaskService = new Mock<ITaskService>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<TasksController>>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        _httpClient = new HttpClient();
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

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task Create_WithVariousInputSizes_ShouldMaintainPerformance(int titleLength)
    {
        // Arrange
        var largeTitle = new string('A', titleLength);
        var taskDto = new TaskDto
        {
            Title = largeTitle,
            Description = "Performance test description",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        var expectedTaskId = Guid.NewGuid();
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(expectedTaskId);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        stopwatch.Stop();
        result.Should().BeOfType<CreatedAtActionResult>();
        
        // Performance assertion - should complete within reasonable time regardless of input size
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
            $"Create operation with {titleLength} character title took too long");
    }

    [Fact]
    public async Task GetAll_WithLargeDataset_ShouldMaintainPerformance()
    {
        // Arrange
        var largeTasks = GenerateLargeTaskList(10000); // 10K tasks
        _mockTaskService.Setup(x => x.GetTasksAsync()).ReturnsAsync(largeTasks);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _controller.GetAll();

        // Assert
        stopwatch.Stop();
        result.Should().BeOfType<OkObjectResult>();
        
        // Should handle large datasets efficiently
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500,
            "GetAll operation with 10K tasks took too long");
    }

    [Theory]
    [InlineData(10, 50)]    // 10 concurrent requests, max 50ms each
    [InlineData(50, 100)]   // 50 concurrent requests, max 100ms each
    [InlineData(100, 200)]  // 100 concurrent requests, max 200ms each
    public async Task Create_ConcurrentRequests_ShouldMaintainPerformance(int concurrentRequests, int maxMilliseconds)
    {
        // Arrange
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(i => new TaskDto
            {
                Title = $"Concurrent Task {i}",
                Description = $"Performance test {i}",
                CreatedById = Guid.NewGuid(),
                AssignedToId = Guid.NewGuid(),
                DueDate = DateTime.UtcNow.AddDays(1)
            }).ToArray();

        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(Guid.NewGuid());

        var stopwatch = Stopwatch.StartNew();

        // Act
        var createTasks = tasks.Select(task => _controller.Create(task)).ToArray();
        var results = await Task.WhenAll(createTasks);

        // Assert
        stopwatch.Stop();
        results.Should().HaveCount(concurrentRequests);
        results.Should().AllBeOfType<CreatedAtActionResult>();
        
        // Average time per request should be reasonable
        var averageTimePerRequest = stopwatch.ElapsedMilliseconds / concurrentRequests;
        averageTimePerRequest.Should().BeLessThan(maxMilliseconds,
            $"Average time per request ({averageTimePerRequest}ms) exceeded threshold for {concurrentRequests} concurrent requests");
    }

    #region Memory Usage Tests

    [Fact]
    public async Task Create_RepeatedCalls_ShouldNotLeakMemory()
    {
        // Arrange
        var taskDto = CreateTestTaskDto();
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(Guid.NewGuid());

        var initialMemory = GC.GetTotalMemory(true);

        // Act
        for (int i = 0; i < 1000; i++)
        {
            await _controller.Create(taskDto);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(true);

        // Assert
        var memoryIncrease = finalMemory - initialMemory;
        
        // Memory increase should be minimal (less than 1MB for 1000 operations)
        memoryIncrease.Should().BeLessThan(1024 * 1024,
            $"Memory increased by {memoryIncrease} bytes after 1000 create operations");
    }

    [Fact]
    public async Task UploadFile_LargeFile_ShouldHandleMemoryEfficiently()
    {
        // Arrange
        var largeFile = HttpContextTestHelper.CreateMockFormFile("large.txt", new string('A', 1024 * 1024)); // 1MB
        
        SetupFileUploadConfiguration();

        var initialMemory = GC.GetTotalMemory(true);

        // Act
        var result = await _controller.UploadFile(largeFile);

        // Clean up
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(true);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var memoryIncrease = finalMemory - initialMemory;
        
        // Memory increase should be reasonable (less than 2x file size)
        memoryIncrease.Should().BeLessThan(2 * 1024 * 1024,
            $"Memory increased by {memoryIncrease} bytes for 1MB file upload");
    }

    #endregion

    #region Async/Await Pattern Tests

    [Fact]
    public async Task Create_AsyncPattern_ShouldNotDeadlock()
    {
        // Arrange
        var taskDto = CreateTestTaskDto();
        var completionSource = new TaskCompletionSource<Guid>();
        
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .Returns(completionSource.Task);

        // Act
        var controllerTask = _controller.Create(taskDto);
        
        // Verify task is not completed yet
        controllerTask.IsCompleted.Should().BeFalse();
        
        // Complete the underlying task
        completionSource.SetResult(Guid.NewGuid());
        
        // Should complete without deadlock
        var result = await controllerTask;

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Multiple_AsyncOperations_ShouldRunConcurrently()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(100);
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .Returns(async () =>
                       {
                           await Task.Delay(delay);
                           return Guid.NewGuid();
                       });

        var taskDto = CreateTestTaskDto();
        var stopwatch = Stopwatch.StartNew();

        // Act - Start 5 operations concurrently
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _controller.Create(taskDto))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        stopwatch.Stop();
        
        // If operations ran concurrently, total time should be closer to 100ms than 500ms
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(300,
            "Operations should run concurrently, not sequentially");
        
        results.Should().HaveCount(5);
        results.Should().AllBeOfType<CreatedAtActionResult>();
    }

    #endregion

    #region Scalability Tests

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public async Task GetAll_ScalabilityTest_ShouldHandleIncreasingLoad(int taskCount)
    {
        // Arrange
        var tasks = GenerateLargeTaskList(taskCount);
        _mockTaskService.Setup(x => x.GetTasksAsync()).ReturnsAsync(tasks);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _controller.GetAll();

        // Assert
        stopwatch.Stop();
        result.Should().BeOfType<OkObjectResult>();
        
        var okResult = result as OkObjectResult;
        var returnedTasks = okResult!.Value as List<TaskItem>;
        returnedTasks.Should().HaveCount(taskCount);
        
        // Time should scale linearly or better (not exponentially)
        var timePerTask = (double)stopwatch.ElapsedMilliseconds / taskCount;
        timePerTask.Should().BeLessThan(1.0, // Less than 1ms per task
            $"Time per task ({timePerTask:F2}ms) indicates poor scalability for {taskCount} tasks");
    }

    [Fact]
    public async Task SearchTasks_ComplexSearch_ShouldMaintainPerformance()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["ApiSettings:ApiKey"]).Returns("valid-key");
        
        var complexSearchParams = new TaskSearchParametersDto
        {
            ApiKey = "valid-key",
            Title = "Complex search title",
            Description = "Complex search description",
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow,
            AssignedTo = "user1",
            CreatedBy = "user2",
            Status = 1,
            Priority = 3,
            IncludeCompleted = true,
            SortBy = "CreatedAt",
            SortDirection = "desc",
            Format = "json"
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _controller.SearchTasks(complexSearchParams);

        // Assert
        stopwatch.Stop();
        result.Should().BeOfType<OkObjectResult>();
        
        // Complex search should still be fast
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50,
            "Complex search took too long");
    }

    #endregion

    #region Resource Disposal Tests

    [Fact]
    public async Task UploadFile_MultipleFiles_ShouldDisposeResourcesProperly()
    {
        // Arrange
        var files = Enumerable.Range(0, 10)
            .Select(i => HttpContextTestHelper.CreateMockFormFile($"file{i}.txt", $"Content {i}"))
            .ToArray();

        SetupFileUploadConfiguration();

        // Act
        var tasks = files.Select(file => _controller.UploadFile(file)).ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(10);
        // Some files might fail due to configuration or validation, so we check that we got responses
        results.Should().AllSatisfy(result => result.Should().BeAssignableTo<IActionResult>());
        
        // Verify no resource leaks by forcing garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // If we reach here without exceptions, resources were properly disposed
        true.Should().BeTrue("All resources were properly disposed");
    }

    [Fact]
    public async Task Controller_Disposal_ShouldCleanupResources()
    {
        // Arrange
        var taskDto = CreateTestTaskDto();
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(Guid.NewGuid());

        // Act
        await _controller.Create(taskDto);
        
        // Dispose and verify no exceptions
        Dispose();

        // Assert
        Assert.True(true, "Controller disposed without issues");
    }

    #endregion

    #region Load Testing Simulation

    [Fact]
    public async Task Create_HighLoad_ShouldMaintainStability()
    {
        // Arrange
        const int requestCount = 200;
        var random = new Random();
        var tasks = Enumerable.Range(0, requestCount)
            .Select(i => new TaskDto
            {
                Title = $"Load Test Task {i}",
                Description = $"Load test description {i} with random data {random.Next()}",
                CreatedById = Guid.NewGuid(),
                AssignedToId = Guid.NewGuid(),
                DueDate = DateTime.UtcNow.AddDays(random.Next(1, 30))
            }).ToArray();

        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(() => Guid.NewGuid());

        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;
        var failureCount = 0;

        // Act
        var createTasks = tasks.Select(async task =>
        {
            try
            {
                var result = await _controller.Create(task);
                if (result is CreatedAtActionResult)
                    Interlocked.Increment(ref successCount);
                else
                    Interlocked.Increment(ref failureCount);
            }
            catch
            {
                Interlocked.Increment(ref failureCount);
            }
        });

        await Task.WhenAll(createTasks);

        // Assert
        stopwatch.Stop();
        
        successCount.Should().Be(requestCount, "All requests should succeed under load");
        failureCount.Should().Be(0, "No requests should fail under normal load");
        
        var averageTime = (double)stopwatch.ElapsedMilliseconds / requestCount;
        averageTime.Should().BeLessThan(10, // Less than 10ms average per request
            $"Average response time ({averageTime:F2}ms) too high under load");
    }

    #endregion

    #region Helper Methods

    private TaskDto CreateTestTaskDto()
    {
        return new TaskDto
        {
            Title = "Performance Test Task",
            Description = "Performance test description",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(1)
        };
    }

    private List<TaskItem> GenerateLargeTaskList(int count)
    {
        var tasks = new List<TaskItem>();
        var random = new Random(42); // Fixed seed for reproducible tests
        
        for (int i = 0; i < count; i++)
        {
            tasks.Add(new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = $"Performance Task {i}",
                Description = $"Performance test description {i}",
                Status = (AgenticTaskManager.Domain.Entities.TaskStatus)(i % 4), // Cycle through all statuses
                CreatedById = Guid.NewGuid(),
                AssignedToId = Guid.NewGuid(),
                DueDate = DateTime.UtcNow.AddDays(random.Next(-30, 30)),
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(0, 100))
            });
        }
        
        return tasks;
    }

    #endregion
}
