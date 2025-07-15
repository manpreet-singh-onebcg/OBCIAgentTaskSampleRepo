using AgenticTaskManager.Domain.Entities;
using Xunit;

namespace AgenticTaskManager.Domain.Tests.Entities
{
    public class TaskItemTests
    {
        [Fact]
        public void TaskItem_Creation_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var id = 1;
            var title = "Test Task";
            var description = "Test Description";
            var isCompleted = false;
            var priority = 1;
            var assignedActorId = 1;

            // Act
            var taskItem = new TaskItem
            {
                Id = id,
                Title = title,
                Description = description,
                IsCompleted = isCompleted,
                Priority = priority,
                AssignedActorId = assignedActorId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Assert
            Assert.Equal(id, taskItem.Id);
            Assert.Equal(title, taskItem.Title);
            Assert.Equal(description, taskItem.Description);
            Assert.Equal(isCompleted, taskItem.IsCompleted);
            Assert.Equal(priority, taskItem.Priority);
            Assert.Equal(assignedActorId, taskItem.AssignedActorId);
            Assert.True(taskItem.CreatedAt <= DateTime.UtcNow);
            Assert.True(taskItem.UpdatedAt <= DateTime.UtcNow);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void TaskItem_WithEmptyTitle_ShouldAllowEmptyOrNullTitle(string title)
        {
            // Arrange & Act
            var taskItem = new TaskItem { Title = title };

            // Assert
            Assert.Equal(title, taskItem.Title);
        }

        [Fact]
        public void TaskItem_MarkAsCompleted_ShouldSetIsCompletedToTrue()
        {
            // Arrange
            var taskItem = new TaskItem { IsCompleted = false };

            // Act
            taskItem.IsCompleted = true;
            taskItem.UpdatedAt = DateTime.UtcNow;

            // Assert
            Assert.True(taskItem.IsCompleted);
        }

        [Fact]
        public void TaskItem_SetPriority_ShouldUpdatePriorityValue()
        {
            // Arrange
            var taskItem = new TaskItem { Priority = 1 };

            // Act
            taskItem.Priority = 5;

            // Assert
            Assert.Equal(5, taskItem.Priority);
        }

        [Fact]
        public void TaskItem_AssignToActor_ShouldSetAssignedActorId()
        {
            // Arrange
            var taskItem = new TaskItem();
            var actorId = 123;

            // Act
            taskItem.AssignedActorId = actorId;

            // Assert
            Assert.Equal(actorId, taskItem.AssignedActorId);
        }
    }
}
