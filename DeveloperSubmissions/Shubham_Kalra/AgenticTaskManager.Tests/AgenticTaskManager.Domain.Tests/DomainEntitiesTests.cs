using AgenticTaskManager.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace AgenticTaskManager.Domain.Tests;

public class DomainEntitiesTests
{
    #region TaskItem Tests

    [Fact]
    public void TaskItem_DefaultConstructor_SetsDefaultValues()
    {
        // Act
        var taskItem = new TaskItem();

        // Assert
        taskItem.Id.Should().Be(Guid.Empty); // TaskItem doesn't auto-generate ID
        taskItem.Title.Should().Be(string.Empty);
        taskItem.Description.Should().Be(string.Empty);
        taskItem.Status.Should().Be(Domain.Entities.TaskStatus.New);
        taskItem.AgentComment.Should().BeNull();
        taskItem.CreatedById.Should().Be(Guid.Empty);
        taskItem.AssignedToId.Should().Be(Guid.Empty);
        taskItem.DueDate.Should().Be(default(DateTime));
        taskItem.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void TaskItem_PropertySetters_WorkCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var title = "Test Task";
        var description = "Test Description";
        var status = Domain.Entities.TaskStatus.InProgress;
        var agentComment = "Agent processed this task";
        var createdById = Guid.NewGuid();
        var assignedToId = Guid.NewGuid();
        var dueDate = DateTime.UtcNow.AddDays(7);
        var createdAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var taskItem = new TaskItem
        {
            Id = id,
            Title = title,
            Description = description,
            Status = status,
            AgentComment = agentComment,
            CreatedById = createdById,
            AssignedToId = assignedToId,
            DueDate = dueDate,
            CreatedAt = createdAt
        };

        // Assert
        taskItem.Id.Should().Be(id);
        taskItem.Title.Should().Be(title);
        taskItem.Description.Should().Be(description);
        taskItem.Status.Should().Be(status);
        taskItem.AgentComment.Should().Be(agentComment);
        taskItem.CreatedById.Should().Be(createdById);
        taskItem.AssignedToId.Should().Be(assignedToId);
        taskItem.DueDate.Should().Be(dueDate);
        taskItem.CreatedAt.Should().Be(createdAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TaskItem_Title_HandlesEmptyAndWhitespaceValues(string title)
    {
        // Act
        var taskItem = new TaskItem { Title = title };

        // Assert
        taskItem.Title.Should().Be(title); // Properties can be null
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TaskItem_Description_HandlesEmptyAndWhitespaceValues(string description)
    {
        // Act
        var taskItem = new TaskItem { Description = description };

        // Assert
        taskItem.Description.Should().Be(description); // Properties can be null
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Valid comment")]
    [InlineData("Very long comment that exceeds normal length expectations for testing purposes")]
    public void TaskItem_AgentComment_HandlesNullableStringValues(string agentComment)
    {
        // Act
        var taskItem = new TaskItem { AgentComment = agentComment };

        // Assert
        taskItem.AgentComment.Should().Be(agentComment);
    }

    [Fact]
    public void TaskItem_CreatedAt_CanBeOverridden()
    {
        // Arrange
        var specificDate = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var taskItem = new TaskItem { CreatedAt = specificDate };

        // Assert
        taskItem.CreatedAt.Should().Be(specificDate);
    }

    [Fact]
    public void TaskItem_GuidsCanBeEmpty()
    {
        // Act
        var taskItem = new TaskItem
        {
            Id = Guid.Empty,
            CreatedById = Guid.Empty,
            AssignedToId = Guid.Empty
        };

        // Assert
        taskItem.Id.Should().Be(Guid.Empty);
        taskItem.CreatedById.Should().Be(Guid.Empty);
        taskItem.AssignedToId.Should().Be(Guid.Empty);
    }

    #endregion

    #region TaskStatus Enum Tests

    [Theory]
    [InlineData(Domain.Entities.TaskStatus.New, 0)]
    [InlineData(Domain.Entities.TaskStatus.InProgress, 1)]
    [InlineData(Domain.Entities.TaskStatus.Completed, 2)]
    [InlineData(Domain.Entities.TaskStatus.Failed, 3)]
    public void TaskStatus_EnumValues_HaveCorrectUnderlyingValues(Domain.Entities.TaskStatus status, int expectedValue)
    {
        // Assert
        ((int)status).Should().Be(expectedValue);
    }

    [Fact]
    public void TaskStatus_AllEnumValues_CanBeAssigned()
    {
        // Arrange & Act
        var taskNew = new TaskItem { Status = Domain.Entities.TaskStatus.New };
        var taskInProgress = new TaskItem { Status = Domain.Entities.TaskStatus.InProgress };
        var taskCompleted = new TaskItem { Status = Domain.Entities.TaskStatus.Completed };
        var taskFailed = new TaskItem { Status = Domain.Entities.TaskStatus.Failed };

        // Assert
        taskNew.Status.Should().Be(Domain.Entities.TaskStatus.New);
        taskInProgress.Status.Should().Be(Domain.Entities.TaskStatus.InProgress);
        taskCompleted.Status.Should().Be(Domain.Entities.TaskStatus.Completed);
        taskFailed.Status.Should().Be(Domain.Entities.TaskStatus.Failed);
    }

    [Fact]
    public void TaskStatus_ToString_ReturnsCorrectNames()
    {
        // Assert
        Domain.Entities.TaskStatus.New.ToString().Should().Be("New");
        Domain.Entities.TaskStatus.InProgress.ToString().Should().Be("InProgress");
        Domain.Entities.TaskStatus.Completed.ToString().Should().Be("Completed");
        Domain.Entities.TaskStatus.Failed.ToString().Should().Be("Failed");
    }

    #endregion

    #region Actor Tests

    [Fact]
    public void Actor_DefaultConstructor_SetsDefaultValues()
    {
        // Act
        var actor = new Actor();

        // Assert
        actor.Id.Should().Be(Guid.Empty);
        actor.Name.Should().Be(string.Empty);
        actor.Type.Should().Be(ActorType.HumanUser);
    }

    [Fact]
    public void Actor_PropertySetters_WorkCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Test Actor";
        var type = ActorType.AIAgent;

        // Act
        var actor = new Actor
        {
            Id = id,
            Name = name,
            Type = type
        };

        // Assert
        actor.Id.Should().Be(id);
        actor.Name.Should().Be(name);
        actor.Type.Should().Be(type);
    }

    [Fact]
    public void Actor_Id_CanBeSetToEmptyGuid()
    {
        // Act
        var actor = new Actor { Id = Guid.Empty };

        // Assert
        actor.Id.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Actor_Id_CanBeSetToValidGuid()
    {
        // Arrange
        var validId = Guid.NewGuid();

        // Act
        var actor = new Actor { Id = validId };

        // Assert
        actor.Id.Should().Be(validId);
        actor.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("John Doe")]
    [InlineData("AI-Agent-001")]
    public void Actor_Name_HandlesVariousStringValues(string name)
    {
        // Act
        var actor = new Actor { Name = name };

        // Assert
        actor.Name.Should().Be(name); // Properties can be null
    }

    #endregion

    #region ActorType Enum Tests

    [Theory]
    [InlineData(ActorType.HumanUser, 0)]
    [InlineData(ActorType.AIAgent, 1)]
    public void ActorType_EnumValues_HaveCorrectUnderlyingValues(ActorType actorType, int expectedValue)
    {
        // Assert
        ((int)actorType).Should().Be(expectedValue);
    }

    [Fact]
    public void ActorType_AllEnumValues_CanBeAssigned()
    {
        // Arrange & Act
        var humanActor = new Actor { Type = ActorType.HumanUser };
        var aiActor = new Actor { Type = ActorType.AIAgent };

        // Assert
        humanActor.Type.Should().Be(ActorType.HumanUser);
        aiActor.Type.Should().Be(ActorType.AIAgent);
    }

    [Fact]
    public void ActorType_ToString_ReturnsCorrectNames()
    {
        // Assert
        ActorType.HumanUser.ToString().Should().Be("HumanUser");
        ActorType.AIAgent.ToString().Should().Be("AIAgent");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void TaskItem_WithActorRelationship_WorksCorrectly()
    {
        // Arrange
        var creatorId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();

        // Act
        var task = new TaskItem
        {
            Title = "Integration Test Task",
            CreatedById = creatorId,
            AssignedToId = assigneeId,
            Status = Domain.Entities.TaskStatus.InProgress
        };

        // Assert
        task.CreatedById.Should().Be(creatorId);
        task.AssignedToId.Should().Be(assigneeId);
        task.Status.Should().Be(Domain.Entities.TaskStatus.InProgress);
    }

    [Fact]
    public void TaskItem_CompleteWorkflow_StatusProgression()
    {
        // Arrange
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Workflow Test Task",
            Status = Domain.Entities.TaskStatus.New
        };

        // Act & Assert - Status progression
        task.Status.Should().Be(Domain.Entities.TaskStatus.New);

        task.Status = Domain.Entities.TaskStatus.InProgress;
        task.Status.Should().Be(Domain.Entities.TaskStatus.InProgress);

        task.Status = Domain.Entities.TaskStatus.Completed;
        task.Status.Should().Be(Domain.Entities.TaskStatus.Completed);
    }

    [Fact]
    public void TaskItem_FailedWorkflow_StatusProgression()
    {
        // Arrange
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Failed Workflow Test Task",
            Status = Domain.Entities.TaskStatus.InProgress
        };

        // Act
        task.Status = Domain.Entities.TaskStatus.Failed;

        // Assert
        task.Status.Should().Be(Domain.Entities.TaskStatus.Failed);
    }

    [Fact]
    public void Actor_TypeSpecificBehavior_HumanUser()
    {
        // Arrange & Act
        var humanActor = new Actor
        {
            Id = Guid.NewGuid(),
            Name = "Human User",
            Type = ActorType.HumanUser
        };

        // Assert
        humanActor.Type.Should().Be(ActorType.HumanUser);
        humanActor.Name.Should().NotBeEmpty();
    }

    [Fact]
    public void Actor_TypeSpecificBehavior_AIAgent()
    {
        // Arrange & Act
        var aiActor = new Actor
        {
            Id = Guid.NewGuid(),
            Name = "AI Assistant",
            Type = ActorType.AIAgent
        };

        // Assert
        aiActor.Type.Should().Be(ActorType.AIAgent);
        aiActor.Name.Should().NotBeEmpty();
    }

    #endregion

    #region Edge Cases and Security Tests

    [Fact]
    public void TaskItem_LargeStringValues_HandledCorrectly()
    {
        // Arrange
        var largeTitle = new string('A', 10000);
        var largeDescription = new string('B', 50000);
        var largeComment = new string('C', 25000);

        // Act
        var task = new TaskItem
        {
            Title = largeTitle,
            Description = largeDescription,
            AgentComment = largeComment
        };

        // Assert
        task.Title.Should().Be(largeTitle);
        task.Description.Should().Be(largeDescription);
        task.AgentComment.Should().Be(largeComment);
    }

    [Fact]
    public void Actor_LargeName_HandledCorrectly()
    {
        // Arrange
        var largeName = new string('X', 5000);

        // Act
        var actor = new Actor { Name = largeName };

        // Assert
        actor.Name.Should().Be(largeName);
    }

    [Theory]
    [InlineData("Normal Title")]
    [InlineData("Title with 'single quotes'")]
    [InlineData("Title with \"double quotes\"")]
    [InlineData("Title with <xml>tags</xml>")]
    [InlineData("Title with SQL'; DROP TABLE Tasks; --")]
    [InlineData("Title\nwith\nnewlines")]
    [InlineData("Title\twith\ttabs")]
    public void TaskItem_SpecialCharacters_HandledSafely(string input)
    {
        // Act
        var task = new TaskItem
        {
            Title = input,
            Description = input,
            AgentComment = input
        };

        // Assert
        task.Title.Should().Be(input);
        task.Description.Should().Be(input);
        task.AgentComment.Should().Be(input);
    }

    [Theory]
    [InlineData("Normal Name")]
    [InlineData("Name with 'quotes'")]
    [InlineData("Name'; DROP TABLE Actors; --")]
    [InlineData("Name\nwith\nspecial\nchars")]
    public void Actor_SpecialCharacters_HandledSafely(string input)
    {
        // Act
        var actor = new Actor { Name = input };

        // Assert
        actor.Name.Should().Be(input);
    }

    [Fact]
    public void TaskItem_DateTimeBoundaries_HandledCorrectly()
    {
        // Arrange
        var minDate = DateTime.MinValue;
        var maxDate = DateTime.MaxValue;

        // Act
        var task = new TaskItem
        {
            DueDate = minDate,
            CreatedAt = maxDate
        };

        // Assert
        task.DueDate.Should().Be(minDate);
        task.CreatedAt.Should().Be(maxDate);
    }

    [Fact]
    public void TaskItem_GuidBoundaries_HandledCorrectly()
    {
        // Arrange
        var emptyGuid = Guid.Empty;
        var maxGuid = new Guid("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");

        // Act
        var task = new TaskItem
        {
            Id = emptyGuid,
            CreatedById = maxGuid,
            AssignedToId = emptyGuid
        };

        // Assert
        task.Id.Should().Be(emptyGuid);
        task.CreatedById.Should().Be(maxGuid);
        task.AssignedToId.Should().Be(emptyGuid);
    }

    #endregion
}
