using AgenticTaskManager.Domain.Entities;
using AgenticTaskManager.Infrastructure.Persistence;
using AgenticTaskManager.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using DomainTaskStatus = AgenticTaskManager.Domain.Entities.TaskStatus;

namespace AgenticTaskManager.Infrastructure.Tests.Repositories;

/// <summary>
/// Integration tests for TaskRepository
/// Focus: Data access, repositories, and database integration
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("Infrastructure")]
public class TaskRepositoryTests
{
    private AppDbContext _context;
    private TaskRepository _repository;

    [SetUp]
    public void SetUp()
    {
        // Create in-memory database for testing
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _repository = new TaskRepository(_context);

        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region AddAsync Tests

    [Test]
    public async Task AddAsync_WithValidTask_ShouldPersistToDatabase()
    {
        // Arrange
        var task = CreateSampleTask("Integration Test Task");

        // Act
        await _repository.AddAsync(task);

        // Assert
        var savedTask = await _context.Tasks.FindAsync(task.Id);
        Assert.That(savedTask, Is.Not.Null);
        Assert.That(savedTask.Title, Is.EqualTo(task.Title));
        Assert.That(savedTask.Description, Is.EqualTo(task.Description));
        Assert.That(savedTask.Status, Is.EqualTo(task.Status));
        Assert.That(savedTask.CreatedById, Is.EqualTo(task.CreatedById));
        Assert.That(savedTask.AssignedToId, Is.EqualTo(task.AssignedToId));
        Assert.That(savedTask.DueDate, Is.EqualTo(task.DueDate));
        Assert.That(savedTask.CreatedAt, Is.EqualTo(task.CreatedAt));
    }

    [Test]
    public async Task AddAsync_WithNullTask_ShouldThrowException()
    {
        // Arrange
        TaskItem? nullTask = null;

        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () => await _repository.AddAsync(nullTask!));
    }

    [Test]
    public async Task AddAsync_WithDuplicateId_ShouldThrowException()
    {
        // Arrange
        var task1 = CreateSampleTask("Task 1");
        var task2 = CreateSampleTask("Task 2");
        task2.Id = task1.Id; // Same ID

        await _repository.AddAsync(task1);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => await _repository.AddAsync(task2));
    }

    [Test]
    public async Task AddAsync_MultipleValidTasks_ShouldPersistAll()
    {
        // Arrange
        var tasks = new[]
        {
            CreateSampleTask("Task 1"),
            CreateSampleTask("Task 2"),
            CreateSampleTask("Task 3")
        };

        // Act
        foreach (var task in tasks)
        {
            await _repository.AddAsync(task);
        }

        // Assert
        var allTasks = await _context.Tasks.ToListAsync();
        Assert.That(allTasks.Count, Is.EqualTo(3));
        
        foreach (var originalTask in tasks)
        {
            var savedTask = allTasks.FirstOrDefault(t => t.Id == originalTask.Id);
            Assert.That(savedTask, Is.Not.Null);
            Assert.That(savedTask.Title, Is.EqualTo(originalTask.Title));
        }
    }

    #endregion

    #region GetAllAsync Tests

    [Test]
    public async Task GetAllAsync_WithDefaultPagination_ShouldReturnPagedResults()
    {
        // Arrange
        var tasks = CreateMultipleTasks(75); // More than default page size
        await AddTasksToDatabase(tasks);

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(50)); // Default page size
        Assert.That(result.All(t => tasks.Any(orig => orig.Id == t.Id)), Is.True);
    }

    [Test]
    public async Task GetAllAsync_WithCustomPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var tasks = CreateMultipleTasks(25);
        await AddTasksToDatabase(tasks);

        // Act
        var page1 = await _repository.GetAllAsync(1, 10);
        var page2 = await _repository.GetAllAsync(2, 10);
        var page3 = await _repository.GetAllAsync(3, 10);

        // Assert
        Assert.That(page1.Count, Is.EqualTo(10));
        Assert.That(page2.Count, Is.EqualTo(10));
        Assert.That(page3.Count, Is.EqualTo(5)); // Remaining tasks

        // Verify no duplicates across pages
        var allPagedIds = page1.Select(t => t.Id)
            .Concat(page2.Select(t => t.Id))
            .Concat(page3.Select(t => t.Id))
            .ToList();

        Assert.That(allPagedIds.Count, Is.EqualTo(allPagedIds.Distinct().Count()));
    }

    [Test]
    public async Task GetAllAsync_WithEmptyDatabase_ShouldReturnEmptyList()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetAllAsync_WithPageBeyondData_ShouldReturnEmptyList()
    {
        // Arrange
        var tasks = CreateMultipleTasks(5);
        await AddTasksToDatabase(tasks);

        // Act
        var result = await _repository.GetAllAsync(5, 10); // Page beyond available data

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetAllAsync_ShouldOrderByCreatedAt()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var tasks = new[]
        {
            CreateSampleTask("Task 3", now.AddMinutes(30)),
            CreateSampleTask("Task 1", now),
            CreateSampleTask("Task 2", now.AddMinutes(15))
        };

        await AddTasksToDatabase(tasks);

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.That(result.Count, Is.EqualTo(3));
        Assert.That(result[0].Title, Is.EqualTo("Task 1")); // Earliest
        Assert.That(result[1].Title, Is.EqualTo("Task 2")); // Middle
        Assert.That(result[2].Title, Is.EqualTo("Task 3")); // Latest
    }

    [Test]
    [TestCase(0, 10, 0)] // Invalid page
    [TestCase(-1, 10, 0)] // Negative page
    [TestCase(1, 0, 0)] // Zero page size
    [TestCase(1, -5, 0)] // Negative page size
    public async Task GetAllAsync_WithInvalidPagination_ShouldHandleGracefully(int page, int pageSize, int expectedCount)
    {
        // Arrange
        var tasks = CreateMultipleTasks(10);
        await AddTasksToDatabase(tasks);

        // Act
        var result = await _repository.GetAllAsync(page, pageSize);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(expectedCount));
    }

    #endregion

    #region GetTasksByComplexCriteria Tests

    [Test]
    public async Task GetTasksByComplexCriteria_WithTitle_ShouldFilterCorrectly()
    {
        // Arrange
        var tasks = new[]
        {
            CreateSampleTask("Important Task"),
            CreateSampleTask("Regular Task"),
            CreateSampleTask("Another Important Item")
        };
        await AddTasksToDatabase(tasks);

        // Act
        var result = await _repository.GetTasksByComplexCriteria("Important", null, null, null, null);

        // Assert
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result.All(t => t.Title.Contains("Important")), Is.True);
    }

    [Test]
    public async Task GetTasksByComplexCriteria_WithUrgentTitle_ShouldSearchDescription()
    {
        // Arrange
        var tasks = new[]
        {
            CreateSampleTask("Normal Task", description: "This is urgent work"),
            CreateSampleTask("urgent Task"),
            CreateSampleTask("Regular Task", description: "Normal work")
        };
        await AddTasksToDatabase(tasks);

        // Act
        var result = await _repository.GetTasksByComplexCriteria("urgent", null, null, null, null);

        // Assert
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result.Any(t => t.Title.Contains("urgent") || t.Description!.Contains("urgent")), Is.True);
    }

    [Test]
    public async Task GetTasksByComplexCriteria_WithDateRange_ShouldFilterByCreatedAt()
    {
        // Arrange
        var baseDate = DateTime.UtcNow;
        var tasks = new[]
        {
            CreateSampleTask("Task 1", createdAt: baseDate.AddDays(-5)),
            CreateSampleTask("Task 2", createdAt: baseDate),
            CreateSampleTask("Task 3", createdAt: baseDate.AddDays(5))
        };
        await AddTasksToDatabase(tasks);

        // Act
        var result = await _repository.GetTasksByComplexCriteria(
            null, 
            baseDate.AddDays(-1), 
            baseDate.AddDays(1), 
            null, 
            null);

        // Assert
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Title, Is.EqualTo("Task 2"));
    }

    [Test]
    public async Task GetTasksByComplexCriteria_WithInvalidDateRange_ShouldReturnAllTasks()
    {
        // Arrange
        var tasks = CreateMultipleTasks(5);
        await AddTasksToDatabase(tasks);

        // Act - End date before start date
        var result = await _repository.GetTasksByComplexCriteria(
            null, 
            DateTime.UtcNow, 
            DateTime.UtcNow.AddDays(-1), 
            null, 
            null);

        // Assert
        Assert.That(result.Count, Is.EqualTo(5)); // Should return all tasks
    }

    #endregion

    #region Helper Methods

    private TaskItem CreateSampleTask(string title, DateTime? createdAt = null, string? description = null)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description ?? "Sample Description",
            Status = DomainTaskStatus.New,
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(7),
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }

    private List<TaskItem> CreateMultipleTasks(int count)
    {
        var tasks = new List<TaskItem>();
        var baseDate = DateTime.UtcNow;

        for (int i = 0; i < count; i++)
        {
            tasks.Add(new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = $"Task {i + 1}",
                Description = $"Description for task {i + 1}",
                Status = (DomainTaskStatus)(i % 3), // Rotate through statuses
                CreatedById = Guid.NewGuid(),
                AssignedToId = i % 2 == 0 ? Guid.NewGuid() : Guid.Empty,
                DueDate = baseDate.AddDays(i),
                CreatedAt = baseDate.AddMinutes(i) // Ensure different creation times
            });
        }

        return tasks;
    }

    private async Task AddTasksToDatabase(IEnumerable<TaskItem> tasks)
    {
        await _context.Tasks.AddRangeAsync(tasks);
        await _context.SaveChangesAsync();
    }

    #endregion
}