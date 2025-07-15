using AgenticTaskManager.Infrastructure.Services;
using AgenticTaskManager.Domain.Entities;
using AgenticTaskManager.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgenticTaskManager.Infrastructure.Tests.Services
{
    public class TaskHelperServiceTests
    {
        private readonly Mock<ITaskRepository> _mockRepository;
        private readonly Mock<ILogger<TaskHelperService>> _mockLogger;
        private readonly TaskHelperService _service;

        public TaskHelperServiceTests()
        {
            _mockRepository = new Mock<ITaskRepository>();
            _mockLogger = new Mock<ILogger<TaskHelperService>>();
            _service = new TaskHelperService(_mockRepository.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task CalculateTaskStatisticsAsync_ShouldReturnCorrectStatistics()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "Task 1", IsCompleted = true, Priority = 1 },
                new TaskItem { Id = 2, Title = "Task 2", IsCompleted = false, Priority = 2 },
                new TaskItem { Id = 3, Title = "Task 3", IsCompleted = true, Priority = 3 },
                new TaskItem { Id = 4, Title = "Task 4", IsCompleted = false, Priority = 1 }
            };

            _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(tasks);

            // Act
            var stats = await _service.CalculateTaskStatisticsAsync();

            // Assert
            Assert.Equal(4, stats.TotalTasks);
            Assert.Equal(2, stats.CompletedTasks);
            Assert.Equal(2, stats.PendingTasks);
            Assert.Equal(50.0, stats.CompletionPercentage);
        }

        [Fact]
        public async Task CalculateTaskStatisticsAsync_WithNoTasks_ShouldReturnZeroStatistics()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<TaskItem>());

            // Act
            var stats = await _service.CalculateTaskStatisticsAsync();

            // Assert
            Assert.Equal(0, stats.TotalTasks);
            Assert.Equal(0, stats.CompletedTasks);
            Assert.Equal(0, stats.PendingTasks);
            Assert.Equal(0.0, stats.CompletionPercentage);
        }

        [Fact]
        public async Task GetTasksByPriorityAsync_ShouldReturnTasksFilteredByPriority()
        {
            // Arrange
            var priority = 1;
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "High Priority 1", Priority = 1 },
                new TaskItem { Id = 2, Title = "Medium Priority", Priority = 2 },
                new TaskItem { Id = 3, Title = "High Priority 2", Priority = 1 }
            };

            _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(tasks);

            // Act
            var result = await _service.GetTasksByPriorityAsync(priority);

            // Assert
            Assert.Equal(2, result.Count());
            Assert.All(result, task => Assert.Equal(priority, task.Priority));
        }

        [Fact]
        public async Task GetOverdueTasksAsync_ShouldReturnTasksPastDueDate()
        {
            // Arrange
            var currentDate = DateTime.UtcNow;
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "Overdue Task 1", DueDate = currentDate.AddDays(-1), IsCompleted = false },
                new TaskItem { Id = 2, Title = "Future Task", DueDate = currentDate.AddDays(1), IsCompleted = false },
                new TaskItem { Id = 3, Title = "Overdue Task 2", DueDate = currentDate.AddDays(-2), IsCompleted = false },
                new TaskItem { Id = 4, Title = "Completed Overdue", DueDate = currentDate.AddDays(-1), IsCompleted = true }
            };

            _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(tasks);

            // Act
            var result = await _service.GetOverdueTasksAsync();

            // Assert
            Assert.Equal(2, result.Count());
            Assert.All(result, task => Assert.True(task.DueDate < currentDate && !task.IsCompleted));
        }

        [Fact]
        public async Task BulkUpdateTaskPriorityAsync_ShouldUpdateAllSpecifiedTasks()
        {
            // Arrange
            var taskIds = new List<int> { 1, 2, 3 };
            var newPriority = 5;
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "Task 1", Priority = 1 },
                new TaskItem { Id = 2, Title = "Task 2", Priority = 2 },
                new TaskItem { Id = 3, Title = "Task 3", Priority = 3 }
            };

            foreach (var task in tasks)
            {
                _mockRepository.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
                _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<TaskItem>())).ReturnsAsync(task);
            }

            // Act
            var result = await _service.BulkUpdateTaskPriorityAsync(taskIds, newPriority);

            // Assert
            Assert.True(result);
            _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Exactly(3));
        }

        [Fact]
        public async Task BulkUpdateTaskPriorityAsync_WithNonExistentTask_ShouldReturnFalse()
        {
            // Arrange
            var taskIds = new List<int> { 999 };
            var newPriority = 5;

            _mockRepository.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((TaskItem)null);

            // Act
            var result = await _service.BulkUpdateTaskPriorityAsync(taskIds, newPriority);

            // Assert
            Assert.False(result);
            _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
        }

        [Fact]
        public async Task GetTaskCompletionTrendAsync_ShouldReturnTrendData()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "Task 1", IsCompleted = true, UpdatedAt = startDate.AddDays(1) },
                new TaskItem { Id = 2, Title = "Task 2", IsCompleted = true, UpdatedAt = startDate.AddDays(3) },
                new TaskItem { Id = 3, Title = "Task 3", IsCompleted = false, UpdatedAt = startDate.AddDays(2) }
            };

            _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(tasks);

            // Act
            var trend = await _service.GetTaskCompletionTrendAsync(startDate, endDate);

            // Assert
            Assert.NotNull(trend);
            Assert.True(trend.Any());
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(11)]
        public async Task GetTasksByPriorityAsync_WithInvalidPriority_ShouldThrowArgumentException(int invalidPriority)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetTasksByPriorityAsync(invalidPriority));
        }

        [Fact]
        public async Task ArchiveCompletedTasksAsync_ShouldArchiveOnlyCompletedTasks()
        {
            // Arrange
            var completedTasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "Completed 1", IsCompleted = true },
                new TaskItem { Id = 2, Title = "Completed 2", IsCompleted = true }
            };

            _mockRepository.Setup(r => r.GetCompletedTasksAsync()).ReturnsAsync(completedTasks);
            _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<TaskItem>())).ReturnsAsync((TaskItem task) => task);

            // Act
            var result = await _service.ArchiveCompletedTasksAsync();

            // Assert
            Assert.Equal(2, result);
            _mockRepository.Verify(r => r.GetCompletedTasksAsync(), Times.Once);
            _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Exactly(2));
        }
    }
}
