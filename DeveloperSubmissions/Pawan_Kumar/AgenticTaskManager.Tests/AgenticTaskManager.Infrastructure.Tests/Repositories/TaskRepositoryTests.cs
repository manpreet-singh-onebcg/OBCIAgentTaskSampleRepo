using AgenticTaskManager.Domain.Entities;
using AgenticTaskManager.Infrastructure.Repositories;
using AgenticTaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgenticTaskManager.Infrastructure.Tests.Repositories
{
    public class TaskRepositoryTests : IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly AppDbContext _context;
        private readonly Mock<ILogger<TaskRepository>> _mockLogger;
        private readonly TaskRepository _repository;

        public TaskRepositoryTests()
        {
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(_options);
            _mockLogger = new Mock<ILogger<TaskRepository>>();
            _repository = new TaskRepository(_context, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnAllTasks()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "Task 1", Description = "Description 1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TaskItem { Id = 2, Title = "Task 2", Description = "Description 2", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            };

            await _context.Tasks.AddRangeAsync(tasks);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            Assert.Equal(2, result.Count());
            Assert.Contains(result, t => t.Title == "Task 1");
            Assert.Contains(result, t => t.Title == "Task 2");
        }

        [Fact]
        public async Task GetByIdAsync_WithValidId_ShouldReturnTask()
        {
            // Arrange
            var task = new TaskItem 
            { 
                Id = 1, 
                Title = "Test Task", 
                Description = "Test Description",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Tasks.AddAsync(task);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Test Task", result.Title);
        }

        [Fact]
        public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
        {
            // Act
            var result = await _repository.GetByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task AddAsync_ShouldAddTaskToDatabase()
        {
            // Arrange
            var task = new TaskItem
            {
                Title = "New Task",
                Description = "New Description",
                Priority = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            var result = await _repository.AddAsync(task);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Id > 0);
            Assert.Equal("New Task", result.Title);

            var taskInDb = await _context.Tasks.FindAsync(result.Id);
            Assert.NotNull(taskInDb);
            Assert.Equal("New Task", taskInDb.Title);
        }

        [Fact]
        public async Task UpdateAsync_WithExistingTask_ShouldUpdateTask()
        {
            // Arrange
            var task = new TaskItem
            {
                Title = "Original Task",
                Description = "Original Description",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Tasks.AddAsync(task);
            await _context.SaveChangesAsync();

            // Modify the task
            task.Title = "Updated Task";
            task.Description = "Updated Description";
            task.UpdatedAt = DateTime.UtcNow;

            // Act
            var result = await _repository.UpdateAsync(task);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Updated Task", result.Title);
            Assert.Equal("Updated Description", result.Description);

            var taskInDb = await _context.Tasks.FindAsync(task.Id);
            Assert.Equal("Updated Task", taskInDb.Title);
        }

        [Fact]
        public async Task DeleteAsync_WithExistingTask_ShouldReturnTrue()
        {
            // Arrange
            var task = new TaskItem
            {
                Title = "Task to Delete",
                Description = "Description",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Tasks.AddAsync(task);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.DeleteAsync(task.Id);

            // Assert
            Assert.True(result);

            var taskInDb = await _context.Tasks.FindAsync(task.Id);
            Assert.Null(taskInDb);
        }

        [Fact]
        public async Task DeleteAsync_WithNonExistentTask_ShouldReturnFalse()
        {
            // Act
            var result = await _repository.DeleteAsync(999);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetTasksByActorIdAsync_ShouldReturnTasksForSpecificActor()
        {
            // Arrange
            var actorId = 1;
            var tasks = new List<TaskItem>
            {
                new TaskItem { Title = "Task 1", AssignedActorId = actorId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TaskItem { Title = "Task 2", AssignedActorId = actorId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TaskItem { Title = "Task 3", AssignedActorId = 2, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            };

            await _context.Tasks.AddRangeAsync(tasks);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetTasksByActorIdAsync(actorId);

            // Assert
            Assert.Equal(2, result.Count());
            Assert.All(result, t => Assert.Equal(actorId, t.AssignedActorId));
        }

        [Fact]
        public async Task GetCompletedTasksAsync_ShouldReturnOnlyCompletedTasks()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem { Title = "Completed 1", IsCompleted = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TaskItem { Title = "Incomplete 1", IsCompleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TaskItem { Title = "Completed 2", IsCompleted = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            };

            await _context.Tasks.AddRangeAsync(tasks);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetCompletedTasksAsync();

            // Assert
            Assert.Equal(2, result.Count());
            Assert.All(result, t => Assert.True(t.IsCompleted));
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
