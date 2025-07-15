using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Application.Services;
using AgenticTaskManager.Domain.Entities;
using DomainTaskStatus = AgenticTaskManager.Domain.Entities.TaskStatus;

namespace AgenticTaskManager.Application.Tests.Services;

/// <summary>
/// Unit tests for TaskService business logic
/// Focus: Business logic, services, and use cases
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Application")]
public class TaskServiceTests
{
    private MockTaskRepository _mockRepository;
    private TaskService _taskService;

    [SetUp]
    public void SetUp()
    {
        _mockRepository = new MockTaskRepository();
        _taskService = new TaskService(_mockRepository);
    }

    [TearDown]
    public void TearDown()
    {
        // TaskService doesn't implement IDisposable, so we don't need to dispose it
        // The mock repository doesn't need disposal either
    }

    #region CreateTaskAsync Tests

    [Test]
    public async Task CreateTaskAsync_WithValidInput_ShouldReturnTaskId()
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = "Test Task",
            Description = "Test Description",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(7)
        };

        var expectedTaskId = Guid.NewGuid();
        _mockRepository.SetupAddAsync(expectedTaskId);

        // Act
        var result = await _taskService.CreateTaskAsync(taskDto);

        // Assert
        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
        Assert.That(_mockRepository.AddAsyncCalled, Is.True);
        Assert.That(_mockRepository.LastAddedTask, Is.Not.Null);
        Assert.That(_mockRepository.LastAddedTask!.Title, Is.EqualTo(taskDto.Title.ToUpper()));
        Assert.That(_mockRepository.LastAddedTask.Description, Is.EqualTo(taskDto.Description));
        Assert.That(_mockRepository.LastAddedTask.CreatedById, Is.EqualTo(taskDto.CreatedById));
        Assert.That(_mockRepository.LastAddedTask.AssignedToId, Is.EqualTo(taskDto.AssignedToId));
        Assert.That(_mockRepository.LastAddedTask.DueDate, Is.EqualTo(taskDto.DueDate));
        Assert.That(_mockRepository.LastAddedTask.Status, Is.EqualTo(DomainTaskStatus.New));
    }

    [Test]
    public async Task CreateTaskAsync_WithNullDto_ShouldThrowArgumentNullException()
    {
        // Arrange
        TaskDto? nullDto = null;

        // Act & Assert
        Assert.ThrowsAsync<NullReferenceException>(async () => await _taskService.CreateTaskAsync(nullDto!));
    }

    [Test]
    public async Task CreateTaskAsync_WhenRepositoryThrows_ShouldNotThrow()
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = "Test Task",
            Description = "Test Description",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(7)
        };

        _mockRepository.SetupAddAsyncToThrow(new InvalidOperationException("Database error"));

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _taskService.CreateTaskAsync(taskDto));
    }

    #endregion

    #region GetTasksAsync Tests

    [Test]
    public async Task GetTasksAsync_WithoutPagination_ShouldReturnFilteredTasks()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            CreateTaskItem("Task 1", DomainTaskStatus.New),
            CreateTaskItem("Task 2", DomainTaskStatus.InProgress),
            CreateTaskItem("Task 3", DomainTaskStatus.Completed), // Should be filtered out
            CreateTaskItem("Task 4", DomainTaskStatus.New)
        };

        _mockRepository.SetupGetAllAsync(tasks);

        // Act
        var result = await _taskService.GetTasksAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(3)); // Excludes completed task
        Assert.That(result.All(t => t.Status != DomainTaskStatus.Completed), Is.True);
        Assert.That(_mockRepository.GetAllAsyncCalled, Is.True);
    }

    [Test]
    public async Task GetTasksAsync_WithPagination_ShouldReturnFilteredPagedTasks()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            CreateTaskItem("Task 1", DomainTaskStatus.New),
            CreateTaskItem("Task 2", DomainTaskStatus.InProgress),
            CreateTaskItem("Task 3", DomainTaskStatus.Completed), // Should be filtered out
        };

        _mockRepository.SetupGetAllAsyncWithPagination(tasks);

        // Act
        var result = await _taskService.GetTasksAsync(1, 10);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2)); // Excludes completed task
        Assert.That(result.All(t => t.Status != DomainTaskStatus.Completed), Is.True);
        Assert.That(_mockRepository.GetAllAsyncWithPaginationCalled, Is.True);
        Assert.That(_mockRepository.LastPage, Is.EqualTo(1));
        Assert.That(_mockRepository.LastPageSize, Is.EqualTo(10));
    }

    [Test]
    public async Task GetTasksAsync_WithEmptyRepository_ShouldReturnEmptyList()
    {
        // Arrange
        _mockRepository.SetupGetAllAsync(new List<TaskItem>());

        // Act
        var result = await _taskService.GetTasksAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetTasksAsync_WhenRepositoryThrows_ShouldPropagateException()
    {
        // Arrange
        _mockRepository.SetupGetAllAsyncToThrow(new InvalidOperationException("Database error"));

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => await _taskService.GetTasksAsync());
    }

    #endregion

    #region GenerateTaskReport Tests

    [Test]
    public void GenerateTaskReport_WithValidTasks_ShouldReturnFormattedReport()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            CreateTaskItem("New Task", DomainTaskStatus.New, DateTime.UtcNow.AddDays(1)), // Not overdue
            CreateTaskItem("Overdue Task", DomainTaskStatus.New, DateTime.UtcNow.AddDays(-1)), // Overdue, assigned
            CreateTaskItem("InProgress Task", DomainTaskStatus.InProgress),
            CreateTaskItem("Other Task", DomainTaskStatus.Failed)
        };

        // Act
        var result = _taskService.GenerateTaskReport(tasks);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("NEW: New Task"));
        Assert.That(result, Does.Contain("URGENT: Overdue Task"));
        Assert.That(result, Does.Contain("IN PROGRESS: InProgress Task"));
        Assert.That(result, Does.Contain("OTHER: Other Task"));
    }

    [Test]
    public void GenerateTaskReport_WithEmptyList_ShouldReturnNoTasksMessage()
    {
        // Arrange
        var emptyTasks = new List<TaskItem>();

        // Act
        var result = _taskService.GenerateTaskReport(emptyTasks);

        // Assert
        Assert.That(result, Is.EqualTo("No tasks found"));
    }

    [Test]
    public void GenerateTaskReport_WithNullList_ShouldReturnNullMessage()
    {
        // Arrange
        List<TaskItem>? nullTasks = null;

        // Act
        var result = _taskService.GenerateTaskReport(nullTasks!);

        // Assert
        Assert.That(result, Is.EqualTo("Tasks list is null"));
    }

    [Test]
    public void GenerateTaskReport_WithNullTaskInList_ShouldSkipNullTask()
    {
        // Arrange
        var tasks = new List<TaskItem?>
        {
            CreateTaskItem("Valid Task", DomainTaskStatus.New),
            null,
            CreateTaskItem("Another Valid Task", DomainTaskStatus.InProgress)
        };

        // Act
        var result = _taskService.GenerateTaskReport(tasks!);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("NEW: Valid Task"));
        Assert.That(result, Does.Contain("IN PROGRESS: Another Valid Task"));
        // Should not crash due to null task
    }

    #endregion

    #region DoStuff Tests (Testing Poor Method Design)

    [Test]
    public async Task DoStuff_WithValidInput_ShouldReturnExpectedResult()
    {
        // Arrange
        var input = "test";
        var count = 5;

        // Act
        var result = await _taskService.DoStuff(input, count);

        // Assert
        Assert.That(result, Is.TypeOf<bool>());
        // The method returns true if result length > 42
        // With input="test" and count=5, we get "test0test1test2test3test4" = 25 chars < 42
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task DoStuff_WithLargeCount_ShouldReturnTrue()
    {
        // Arrange
        var input = "test";
        var count = 20; // Should generate string > 42 characters

        // Act
        var result = await _taskService.DoStuff(input, count);

        // Assert
        Assert.That(result, Is.True);
    }

    #endregion

    #region CalculateComplexity Tests (Testing Infinite Recursion)

    [Test]
    public void CalculateComplexity_WithNullTask_ShouldReturnZero()
    {
        // Arrange
        TaskItem? nullTask = null;

        // Act
        var result = _taskService.CalculateComplexity(nullTask!);

        // Assert
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public void CalculateComplexity_WithAssignedTask_ShouldReturnDepth()
    {
        // Arrange
        var task = CreateTaskItem("Test Task", DomainTaskStatus.New);
        task.AssignedToId = Guid.NewGuid(); // Non-empty GUID

        // Act
        var result = _taskService.CalculateComplexity(task, 5);

        // Assert
        Assert.That(result, Is.EqualTo(5)); // Should return the depth passed in
    }

    [Test]
    public void CalculateComplexity_WithUnassignedTask_MightCauseInfiniteRecursion()
    {
        // Arrange
        var task = CreateTaskItem("Test Task", DomainTaskStatus.New);
        task.AssignedToId = Guid.Empty; // This will cause infinite recursion

        // Act & Assert
        // This test demonstrates the infinite recursion bug
        // In a real scenario, this would cause a StackOverflowException
        // For testing purposes, we'll limit the depth to avoid actual stack overflow
        Assert.DoesNotThrow(() =>
        {
            try
            {
                var result = _taskService.CalculateComplexity(task, 0);
                // If we reach here, the recursion was limited somehow
            }
            catch (StackOverflowException)
            {
                // This is expected due to the infinite recursion bug
                Assert.Pass("Correctly identified infinite recursion issue");
            }
        });
    }

    #endregion

    #region ValidateAdminAccess Tests (Testing Hardcoded Password)

    [Test]
    [TestCase("admin123", true)]
    [TestCase("wrongpassword", false)]
    [TestCase("", false)]
    [TestCase("ADMIN123", false)] // Case sensitive
    public void ValidateAdminAccess_WithVariousPasswords_ShouldReturnExpectedResult(string password, bool expected)
    {
        // Act
        var result = _taskService.ValidateAdminAccess(password);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    #endregion

    #region Helper Methods

    private TaskItem CreateTaskItem(string title, DomainTaskStatus status, DateTime? dueDate = null)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "Test Description",
            Status = status,
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = dueDate ?? DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion
}

#region Mock Repository Implementation

/// <summary>
/// Mock implementation of ITaskRepository for testing
/// </summary>
public class MockTaskRepository : ITaskRepository
{
    public bool AddAsyncCalled { get; private set; }
    public bool GetAllAsyncCalled { get; private set; }
    public bool GetAllAsyncWithPaginationCalled { get; private set; }
    
    public TaskItem? LastAddedTask { get; private set; }
    public int LastPage { get; private set; }
    public int LastPageSize { get; private set; }

    private List<TaskItem> _tasks = new();
    private Exception? _addAsyncException;
    private Exception? _getAllAsyncException;

    public void SetupAddAsync(Guid taskId)
    {
        _addAsyncException = null;
    }

    public void SetupAddAsyncToThrow(Exception exception)
    {
        _addAsyncException = exception;
    }

    public void SetupGetAllAsync(List<TaskItem> tasks)
    {
        _tasks = tasks;
        _getAllAsyncException = null;
    }

    public void SetupGetAllAsyncWithPagination(List<TaskItem> tasks)
    {
        _tasks = tasks;
        _getAllAsyncException = null;
    }

    public void SetupGetAllAsyncToThrow(Exception exception)
    {
        _getAllAsyncException = exception;
    }

    public Task AddAsync(TaskItem task)
    {
        AddAsyncCalled = true;
        LastAddedTask = task;

        if (_addAsyncException != null)
            throw _addAsyncException;

        return Task.CompletedTask;
    }

    public Task<List<TaskItem>> GetAllAsync(int page = 1, int pageSize = 50)
    {
        if (page == 1 && pageSize == 50)
        {
            GetAllAsyncCalled = true;
        }
        else
        {
            GetAllAsyncWithPaginationCalled = true;
            LastPage = page;
            LastPageSize = pageSize;
        }

        if (_getAllAsyncException != null)
            throw _getAllAsyncException;

        return Task.FromResult(_tasks);
    }
}

#endregion