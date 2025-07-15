using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Application.Services;
using AgenticTaskManager.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using Xunit;
using DomainTaskStatus = AgenticTaskManager.Domain.Entities.TaskStatus;

namespace AgenticTaskManager.Application.Tests;

public class TaskServiceTests : IDisposable
{
    private readonly Mock<ITaskRepository> _mockRepository;
    private readonly Mock<ILogger<TaskService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly TaskService _taskService;

    public TaskServiceTests()
    {
        _mockRepository = new Mock<ITaskRepository>();
        _mockLogger = new Mock<ILogger<TaskService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        // Create configuration setup using in-memory configuration
        var configData = new Dictionary<string, string>
        {
            ["ApiSettings:AdminPassword"] = "SecurePassword123",
            ["FileSettings:DataFilePath"] = "test-file.txt"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        _taskService = new TaskService(_mockRepository.Object, _mockLogger.Object, configuration);
    }

    #region Security Testing

    [Theory]
    [InlineData("'; DROP TABLE Tasks; --")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("../../etc/passwd")]
    [InlineData("SELECT * FROM Users WHERE id = 1")]
    public async Task CreateTaskAsync_WithMaliciousInput_ShouldSanitizeAndProcess(string maliciousInput)
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

        _mockRepository.Setup(x => x.AddAsync(It.IsAny<TaskItem>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _taskService.CreateTaskAsync(taskDto);

        // Assert
        result.Should().NotBeEmpty();
        _mockRepository.Verify(x => x.AddAsync(It.Is<TaskItem>(t => 
            t.Title == maliciousInput && 
            t.Description == maliciousInput.Trim())), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidateAdminAccess_WithInvalidPassword_ShouldReturnFalse(string invalidPassword)
    {
        // Act
        var result = _taskService.ValidateAdminAccess(invalidPassword);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateAdminAccess_WithCorrectPassword_ShouldReturnTrue()
    {
        // Act
        var result = _taskService.ValidateAdminAccess("SecurePassword123");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateAdminAccess_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Act
        var result = _taskService.ValidateAdminAccess("WrongPassword");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("'; UNION SELECT * FROM Users; --")]
    [InlineData("admin' OR '1'='1")]
    [InlineData("1; DELETE FROM Tasks; --")]
    public async Task GetUserReportAsync_WithSqlInjectionAttempt_ShouldHandleSafely(string maliciousUserId)
    {
        // Arrange
        _mockRepository.Setup(x => x.GetTasksByUserIdAsync(maliciousUserId))
            .ReturnsAsync(new List<TaskItem>());

        // Act
        var result = await _taskService.GetUserReportAsync(maliciousUserId);

        // Assert
        result.Should().NotBeNull();
        _mockRepository.Verify(x => x.GetTasksByUserIdAsync(maliciousUserId), Times.Once);
    }

    #endregion

    #region Performance Testing

    [Fact]
    public async Task CreateTaskAsync_PerformanceBenchmark_ShouldCompleteWithinReasonableTime()
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = "Performance Test Task",
            Description = "Testing performance",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        _mockRepository.Setup(x => x.AddAsync(It.IsAny<TaskItem>()))
            .Returns(Task.CompletedTask);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _taskService.CreateTaskAsync(taskDto);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Should complete within 1 second
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTasksAsync_WithLargeDataset_ShouldHandleEfficiently()
    {
        // Arrange
        var largeTasks = Enumerable.Range(1, 1000)
            .Select(i => new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = $"Task {i}",
                Status = i % 2 == 0 ? DomainTaskStatus.New : DomainTaskStatus.Completed,
                CreatedById = Guid.NewGuid(),
                DueDate = DateTime.UtcNow.AddDays(i)
            }).ToList();

        _mockRepository.Setup(x => x.GetAllAsync())
            .ReturnsAsync(largeTasks);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _taskService.GetTasksAsync();

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);
        result.Should().HaveCount(500); // Only non-completed tasks
    }

    [Fact]
    public async Task ProcessStringDataAsync_WithLargeInput_ShouldHandleEfficiently()
    {
        // Arrange
        var largeInput = new string('x', 1000);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _taskService.ProcessStringDataAsync(largeInput, 100);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
        result.Should().BeTrue();
    }

    [Fact]
    public void Dispose_ShouldReleaseResourcesProperly()
    {
        // Arrange
        var service = new TaskService(_mockRepository.Object, _mockLogger.Object, _mockConfiguration.Object);

        // Act
        service.Dispose();

        // Assert - Should not throw exception when called multiple times
        service.Dispose();
        service.Dispose();
    }

    #endregion

    #region Quality Assurance - Edge Cases and Error Scenarios

    [Fact]
    public async Task CreateTaskAsync_WithNullDto_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _taskService.CreateTaskAsync(null!));
    }

    [Fact]
    public async Task CreateTaskAsync_WhenRepositoryThrows_ShouldPropagateException()
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = "Test Task",
            CreatedById = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        _mockRepository.Setup(x => x.AddAsync(It.IsAny<TaskItem>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _taskService.CreateTaskAsync(taskDto));
    }

    [Fact]
    public async Task GetTasksAsync_WhenRepositoryThrows_ShouldPropagateException()
    {
        // Arrange
        _mockRepository.Setup(x => x.GetAllAsync())
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _taskService.GetTasksAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GetUserReportAsync_WithInvalidUserId_ShouldThrowArgumentException(string invalidUserId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _taskService.GetUserReportAsync(invalidUserId));
    }

    [Fact]
    public void GenerateTaskReport_WithNullTasksList_ShouldReturnAppropriateMessage()
    {
        // Act
        var result = _taskService.GenerateTaskReport(null!);

        // Assert
        result.Should().Be("Tasks list is null");
    }

    [Fact]
    public void GenerateTaskReport_WithEmptyTasksList_ShouldReturnNoTasksMessage()
    {
        // Act
        var result = _taskService.GenerateTaskReport(new List<TaskItem>());

        // Assert
        result.Should().Be("No tasks found");
    }

    [Fact]
    public void GenerateTaskReport_WithMixedStatusTasks_ShouldGenerateCorrectReport()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new() { Title = "New Task", Status = DomainTaskStatus.New, DueDate = DateTime.UtcNow.AddDays(1), AssignedToId = Guid.NewGuid() },
            new() { Title = "Overdue Task", Status = DomainTaskStatus.New, DueDate = DateTime.UtcNow.AddDays(-1), AssignedToId = Guid.Empty },
            new() { Title = "In Progress Task", Status = DomainTaskStatus.InProgress, DueDate = DateTime.UtcNow.AddDays(1), AssignedToId = Guid.NewGuid() }
        };

        // Act
        var result = _taskService.GenerateTaskReport(tasks);

        // Assert
        result.Should().Contain("NEW: New Task");
        result.Should().Contain("URGENT: Overdue Task is overdue and unassigned");
        result.Should().Contain("IN PROGRESS: In Progress Task");
    }

    [Theory]
    [InlineData("", 5)]
    [InlineData(null, 10)]
    [InlineData("test", 0)]
    [InlineData("test", -1)]
    public async Task ProcessStringDataAsync_WithInvalidParameters_ShouldReturnFalse(string input, int count)
    {
        // Act
        var result = await _taskService.ProcessStringDataAsync(input, count);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CalculateComplexity_WithNullTask_ShouldReturnZero()
    {
        // Act
        var result = _taskService.CalculateComplexity(null!);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateComplexity_WithUnassignedTask_ShouldHandleRecursion()
    {
        // Arrange
        var task = new TaskItem { AssignedToId = Guid.Empty };

        // Act
        var result = _taskService.CalculateComplexity(task);

        // Assert
        result.Should().Be(10); // Should hit max recursion depth
    }

    [Fact]
    public void CalculateComplexity_WithAssignedTask_ShouldReturnZero()
    {
        // Arrange
        var task = new TaskItem { AssignedToId = Guid.NewGuid() };

        // Act
        var result = _taskService.CalculateComplexity(task);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region Thread Safety Testing

    [Fact]
    public async Task ConcurrentCreateTaskAsync_ShouldHandleMultipleRequests()
    {
        // Arrange
        _mockRepository.Setup(x => x.AddAsync(It.IsAny<TaskItem>()))
            .Returns(Task.CompletedTask);

        var tasks = Enumerable.Range(1, 10).Select(i => new TaskDto
        {
            Title = $"Concurrent Task {i}",
            CreatedById = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(1)
        });

        // Act
        var createTasks = tasks.Select(dto => _taskService.CreateTaskAsync(dto));
        var results = await Task.WhenAll(createTasks);

        // Assert
        results.Should().HaveCount(10);
        results.Should().OnlyContain(id => id != Guid.Empty);
        _mockRepository.Verify(x => x.AddAsync(It.IsAny<TaskItem>()), Times.Exactly(10));
    }

    [Fact]
    public async Task ClearCacheAsync_ShouldHandleConcurrentAccess()
    {
        // Arrange
        var clearTasks = Enumerable.Range(1, 5).Select(_ => _taskService.ClearCacheAsync());

        // Act
        var results = await Task.WhenAll(clearTasks);

        // Assert
        results.Should().OnlyContain(result => result == true);
    }

    #endregion

    #region Async/Await Pattern Testing

    [Fact]
    public async Task CreateTaskAsync_ShouldCompleteAsynchronously()
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = "Async Test Task",
            CreatedById = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        var tcs = new TaskCompletionSource<bool>();
        _mockRepository.Setup(x => x.AddAsync(It.IsAny<TaskItem>()))
            .Returns(tcs.Task.ContinueWith(_ => { }));

        // Act
        var createTask = _taskService.CreateTaskAsync(taskDto);
        
        // Verify task hasn't completed yet
        createTask.IsCompleted.Should().BeFalse();
        
        // Complete the repository operation
        tcs.SetResult(true);
        var result = await createTask;

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUserReportAsync_ShouldReturnCorrectStructure()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var userTasks = new List<TaskItem>
        {
            new() { Id = Guid.NewGuid(), Title = "User Task 1", CreatedById = Guid.Parse(userId) }
        };

        _mockRepository.Setup(x => x.GetTasksByUserIdAsync(userId))
            .ReturnsAsync(userTasks);

        // Act
        var result = await _taskService.GetUserReportAsync(userId);

        // Assert
        result.Should().NotBeNull();
        var resultString = result.ToString();
        resultString.Should().Contain(userId);
    }

    #endregion

    public void Dispose()
    {
        _taskService?.Dispose();
    }
}
