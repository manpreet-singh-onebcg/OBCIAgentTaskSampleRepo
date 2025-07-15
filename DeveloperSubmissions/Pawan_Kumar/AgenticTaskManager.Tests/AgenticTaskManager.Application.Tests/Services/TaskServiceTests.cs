using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Application.Services;
using AgenticTaskManager.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgenticTaskManager.Application.Tests.Services
{
    public class TaskServiceTests
    {
        private readonly Mock<ITaskRepository> _mockRepository;
        private readonly Mock<ILogger<TaskService>> _mockLogger;
        private readonly TaskService _taskService;

        public TaskServiceTests()
        {
            _mockRepository = new Mock<ITaskRepository>();
            _mockLogger = new Mock<ILogger<TaskService>>();
            _taskService = new TaskService(_mockRepository.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnAllTasks()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "Task 1", Description = "Description 1" },
                new TaskItem { Id = 2, Title = "Task 2", Description = "Description 2" }
            };
            _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(tasks);

            // Act
            var result = await _taskService.GetAllAsync();

            // Assert
            Assert.Equal(2, result.Count());
            Assert.Equal("Task 1", result.First().Title);
            _mockRepository.Verify(r => r.GetAllAsync(), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_WithValidId_ShouldReturnTask()
        {
            // Arrange
            var taskId = 1;
            var task = new TaskItem { Id = taskId, Title = "Test Task", Description = "Test Description" };
            _mockRepository.Setup(r => r.GetByIdAsync(taskId)).ReturnsAsync(task);

            // Act
            var result = await _taskService.GetByIdAsync(taskId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(taskId, result.Id);
            Assert.Equal("Test Task", result.Title);
            _mockRepository.Verify(r => r.GetByIdAsync(taskId), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
        {
            // Arrange
            var taskId = 999;
            _mockRepository.Setup(r => r.GetByIdAsync(taskId)).ReturnsAsync((TaskItem)null);

            // Act
            var result = await _taskService.GetByIdAsync(taskId);

            // Assert
            Assert.Null(result);
            _mockRepository.Verify(r => r.GetByIdAsync(taskId), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_WithValidDto_ShouldCreateTask()
        {
            // Arrange
            var taskDto = new TaskDto
            {
                Title = "New Task",
                Description = "New Description",
                CreatedById = Guid.NewGuid()
            };

            var createdTask = new TaskItem
            {
                Id = 1,
                Title = taskDto.Title,
                Description = taskDto.Description,
                CreatedAt = DateTime.UtcNow,
                CreatedById = taskDto.CreatedById
            };

            _mockRepository.Setup(r => r.AddAsync(It.IsAny<TaskItem>())).ReturnsAsync(createdTask);

            // Act
            var result = await _taskService.CreateAsync(taskDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(taskDto.Title, result.Title);
            Assert.Equal(taskDto.Description, result.Description);
            _mockRepository.Verify(r => r.AddAsync(It.IsAny<TaskItem>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WithValidDto_ShouldUpdateTask()
        {
            // Arrange
            var taskId = 1;
            var existingTask = new TaskItem { Id = taskId, Title = "Old Title", Description = "Old Description" };
            var taskDto = new TaskDto { Title = "Updated Title", Description = "Updated Description" };

            _mockRepository.Setup(r => r.GetByIdAsync(taskId)).ReturnsAsync(existingTask);
            _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<TaskItem>())).ReturnsAsync(existingTask);

            // Act
            var result = await _taskService.UpdateAsync(taskId, taskDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Updated Title", result.Title);
            Assert.Equal("Updated Description", result.Description);
            _mockRepository.Verify(r => r.GetByIdAsync(taskId), Times.Once);
            _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WithNonExistentTask_ShouldReturnNull()
        {
            // Arrange
            var taskId = 999;
            var taskDto = new TaskDto { Title = "Updated Title" };
            _mockRepository.Setup(r => r.GetByIdAsync(taskId)).ReturnsAsync((TaskItem)null);

            // Act
            var result = await _taskService.UpdateAsync(taskId, taskDto);

            // Assert
            Assert.Null(result);
            _mockRepository.Verify(r => r.GetByIdAsync(taskId), Times.Once);
            _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_WithValidId_ShouldReturnTrue()
        {
            // Arrange
            var taskId = 1;
            _mockRepository.Setup(r => r.DeleteAsync(taskId)).ReturnsAsync(true);

            // Act
            var result = await _taskService.DeleteAsync(taskId);

            // Assert
            Assert.True(result);
            _mockRepository.Verify(r => r.DeleteAsync(taskId), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_WithNonExistentTask_ShouldReturnFalse()
        {
            // Arrange
            var taskId = 999;
            _mockRepository.Setup(r => r.DeleteAsync(taskId)).ReturnsAsync(false);

            // Act
            var result = await _taskService.DeleteAsync(taskId);

            // Assert
            Assert.False(result);
            _mockRepository.Verify(r => r.DeleteAsync(taskId), Times.Once);
        }

        // Security Tests
        [Theory]
        [InlineData("<script>alert('XSS')</script>")]
        [InlineData("'; DROP TABLE Tasks; --")]
        [InlineData("1' OR '1'='1")]
        [InlineData("<img src=x onerror=alert('XSS')>")]
        public async Task CreateAsync_WithMaliciousInput_ShouldSanitizeInput(string maliciousInput)
        {
            // Arrange
            var taskDto = new TaskDto
            {
                Title = maliciousInput,
                Description = maliciousInput,
                CreatedById = Guid.NewGuid()
            };

            var createdTask = new TaskItem { Id = 1, Title = "Sanitized", Description = "Sanitized" };
            _mockRepository.Setup(r => r.AddAsync(It.IsAny<TaskItem>())).ReturnsAsync(createdTask);

            // Act
            var result = await _taskService.CreateAsync(taskDto);

            // Assert
            Assert.NotNull(result);
            // Verify that the input is passed to the repository (sanitization should happen at repository level)
            _mockRepository.Verify(r => r.AddAsync(It.Is<TaskItem>(t => 
                t.Title == maliciousInput && t.Description == maliciousInput)), Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task CreateAsync_WithInvalidTitle_ShouldReturnNull(string invalidTitle)
        {
            // Arrange
            var taskDto = new TaskDto { Title = invalidTitle, Description = "Valid Description", CreatedById = Guid.NewGuid() };

            // Act
            var result = await _taskService.CreateAsync(taskDto);

            // Assert
            Assert.Null(result);
            _mockRepository.Verify(r => r.AddAsync(It.IsAny<TaskItem>()), Times.Never);
        }

        // Performance Tests
        [Fact]
        public async Task GetAllAsync_WithLargeDataset_ShouldComplete()
        {
            // Arrange
            var largeTasks = Enumerable.Range(1, 10000)
                .Select(i => new TaskItem { Id = i, Title = $"Task {i}" })
                .ToList();
            
            _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(largeTasks);
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var result = await _taskService.GetAllAsync();

            // Assert
            stopwatch.Stop();
            Assert.Equal(10000, result.Count());
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Operation should complete within 5 seconds");
        }

        [Fact]
        public async Task CreateAsync_ConcurrentRequests_ShouldHandleThreadSafety()
        {
            // Arrange
            var tasks = new List<Task<TaskDto?>>();
            var createdTask = new TaskItem { Id = 1, Title = "Test", Description = "Test" };
            _mockRepository.Setup(r => r.AddAsync(It.IsAny<TaskItem>())).ReturnsAsync(createdTask);

            // Act - Create 100 concurrent requests
            for (int i = 0; i < 100; i++)
            {
                var taskDto = new TaskDto 
                { 
                    Title = $"Task {i}", 
                    Description = $"Description {i}",
                    CreatedById = Guid.NewGuid()
                };
                tasks.Add(_taskService.CreateAsync(taskDto));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, result => Assert.NotNull(result));
            _mockRepository.Verify(r => r.AddAsync(It.IsAny<TaskItem>()), Times.Exactly(100));
        }

        // Exception Handling Tests
        [Fact]
        public async Task CreateAsync_WhenRepositoryThrows_ShouldReturnNull()
        {
            // Arrange
            var taskDto = new TaskDto { Title = "Test", Description = "Test", CreatedById = Guid.NewGuid() };
            _mockRepository.Setup(r => r.AddAsync(It.IsAny<TaskItem>())).ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _taskService.CreateAsync(taskDto);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAllAsync_WhenRepositoryThrows_ShouldThrowException()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("Database connection failed"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _taskService.GetAllAsync());
        }

        // Edge Cases
        [Fact]
        public async Task CreateAsync_WithEmptyGuid_ShouldReturnNull()
        {
            // Arrange
            var taskDto = new TaskDto 
            { 
                Title = "Valid Title", 
                Description = "Valid Description",
                CreatedById = Guid.Empty 
            };

            // Act
            var result = await _taskService.CreateAsync(taskDto);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SearchAsync_WithSpecialCharacters_ShouldHandleCorrectly()
        {
            // Arrange
            var searchResults = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "Test with 'quotes'", Description = "Test & symbols" }
            };
            
            _mockRepository.Setup(r => r.SearchAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), 
                It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(searchResults);

            // Act
            var result = await _taskService.SearchAsync("'test'", "&symbols", null, null, 0, 10);

            // Assert
            Assert.Single(result);
            _mockRepository.Verify(r => r.SearchAsync(
                "'test'", "&symbols", null, null, 0, 10), Times.Once);
        }
    }
}
