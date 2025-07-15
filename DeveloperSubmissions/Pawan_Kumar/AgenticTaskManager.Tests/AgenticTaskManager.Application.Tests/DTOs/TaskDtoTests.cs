using AgenticTaskManager.Application.DTOs;
using Xunit;

namespace AgenticTaskManager.Application.Tests.DTOs
{
    public class TaskDtoTests
    {
        [Fact]
        public void TaskDto_Creation_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var id = 1;
            var title = "Test Task";
            var description = "Test Description";
            var status = "Pending";
            var createdById = Guid.NewGuid();
            var assignedToId = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;
            var updatedAt = DateTime.UtcNow;

            // Act
            var taskDto = new TaskDto
            {
                Id = id,
                Title = title,
                Description = description,
                Status = status,
                CreatedById = createdById,
                AssignedToId = assignedToId,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };

            // Assert
            Assert.Equal(id, taskDto.Id);
            Assert.Equal(title, taskDto.Title);
            Assert.Equal(description, taskDto.Description);
            Assert.Equal(status, taskDto.Status);
            Assert.Equal(createdById, taskDto.CreatedById);
            Assert.Equal(assignedToId, taskDto.AssignedToId);
            Assert.Equal(createdAt, taskDto.CreatedAt);
            Assert.Equal(updatedAt, taskDto.UpdatedAt);
        }

        [Fact]
        public void TaskDto_DefaultValues_ShouldBeSetCorrectly()
        {
            // Act
            var taskDto = new TaskDto();

            // Assert
            Assert.Equal(0, taskDto.Id);
            Assert.Equal(string.Empty, taskDto.Title);
            Assert.Equal(string.Empty, taskDto.Description);
            Assert.Equal(string.Empty, taskDto.Status);
            Assert.Equal(Guid.Empty, taskDto.CreatedById);
            Assert.Null(taskDto.AssignedToId);
            Assert.Equal(default(DateTime), taskDto.CreatedAt);
            Assert.Null(taskDto.UpdatedAt);
        }

        [Theory]
        [InlineData("Pending", "Pending")]
        [InlineData("InProgress", "InProgress")]
        [InlineData("Completed", "Completed")]
        public void TaskDto_WithDifferentStatuses_ShouldSetCorrectly(string status, string expectedStatus)
        {
            // Arrange & Act
            var taskDto = new TaskDto { Status = status };

            // Assert
            Assert.Equal(status, taskDto.Status);
        }

        [Fact]
        public void TaskDto_MarkAsCompleted_ShouldUpdateStatus()
        {
            // Arrange
            var taskDto = new TaskDto { Status = "Pending" };

            // Act
            taskDto.Status = "Completed";
            taskDto.UpdatedAt = DateTime.UtcNow;

            // Assert
            Assert.Equal("Completed", taskDto.Status);
            Assert.True(taskDto.UpdatedAt > DateTime.MinValue);
        }

        [Fact]
        public void TaskDto_AssignToActor_ShouldSetActorId()
        {
            // Arrange
            var taskDto = new TaskDto();
            var actorId = Guid.NewGuid();

            // Act
            taskDto.AssignedToId = actorId;

            // Assert
            Assert.Equal(actorId, taskDto.AssignedToId);
        }

        [Fact]
        public void TaskDto_UnassignFromActor_ShouldSetActorIdToNull()
        {
            // Arrange
            var taskDto = new TaskDto { AssignedToId = Guid.NewGuid() };

            // Act
            taskDto.AssignedToId = null;

            // Assert
            Assert.Null(taskDto.AssignedToId);
        }
    }
}
