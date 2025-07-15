using AgenticTaskManager.Domain.Entities;
using DomainTaskStatus = AgenticTaskManager.Domain.Entities.TaskStatus;

namespace AgenticTaskManager.Domain.Tests.Entities;

/// <summary>
/// Unit tests for TaskItem domain entity
/// Focus: Pure unit tests for business entities and domain logic
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
public class TaskItemTests
{
    [Test]
    public void TaskItem_DefaultConstructor_ShouldCreateInstanceWithDefaults()
    {
        // Arrange & Act
        var task = new TaskItem();

        // Assert
        Assert.That(task.Id, Is.EqualTo(Guid.Empty));
        Assert.That(task.Title, Is.Null);
        Assert.That(task.Description, Is.Null);
        Assert.That(task.Status, Is.EqualTo(DomainTaskStatus.New));
        Assert.That(task.CreatedAt, Is.EqualTo(DateTime.MinValue));
        Assert.That(task.DueDate, Is.EqualTo(DateTime.MinValue));
        Assert.That(task.CreatedById, Is.EqualTo(Guid.Empty));
        Assert.That(task.AssignedToId, Is.EqualTo(Guid.Empty));
    }

    [Test]
    public void TaskItem_WithValidData_ShouldCreateSuccessfully()
    {
        // Arrange
        var id = Guid.NewGuid();
        var createdById = Guid.NewGuid();
        var assignedToId = Guid.NewGuid();
        var title = "Test Task";
        var description = "Test Description";
        var dueDate = DateTime.UtcNow.AddDays(7);
        var createdAt = DateTime.UtcNow;

        // Act
        var task = new TaskItem
        {
            Id = id,
            Title = title,
            Description = description,
            Status = DomainTaskStatus.InProgress,
            CreatedById = createdById,
            AssignedToId = assignedToId,
            DueDate = dueDate,
            CreatedAt = createdAt
        };

        // Assert
        Assert.That(task.Id, Is.EqualTo(id));
        Assert.That(task.Title, Is.EqualTo(title));
        Assert.That(task.Description, Is.EqualTo(description));
        Assert.That(task.Status, Is.EqualTo(DomainTaskStatus.InProgress));
        Assert.That(task.CreatedById, Is.EqualTo(createdById));
        Assert.That(task.AssignedToId, Is.EqualTo(assignedToId));
        Assert.That(task.DueDate, Is.EqualTo(dueDate));
        Assert.That(task.CreatedAt, Is.EqualTo(createdAt));
    }

    [Test]
    [TestCase(DomainTaskStatus.New)]
    [TestCase(DomainTaskStatus.InProgress)]
    [TestCase(DomainTaskStatus.Completed)]
    [TestCase(DomainTaskStatus.Failed)]
    public void TaskItem_StatusProperty_ShouldAcceptAllValidStatuses(DomainTaskStatus status)
    {
        // Arrange
        var task = new TaskItem();

        // Act
        task.Status = status;

        // Assert
        Assert.That(task.Status, Is.EqualTo(status));
    }

    [Test]
    public void TaskItem_TitleProperty_ShouldAcceptValidStrings()
    {
        // Arrange
        var task = new TaskItem();
        var title = "Valid Task Title";

        // Act
        task.Title = title;

        // Assert
        Assert.That(task.Title, Is.EqualTo(title));
    }

    [Test]
    public void TaskItem_TitleProperty_ShouldAcceptNullValue()
    {
        // Arrange
        var task = new TaskItem();

        // Act
        task.Title = null;

        // Assert
        Assert.That(task.Title, Is.Null);
    }

    [Test]
    public void TaskItem_DescriptionProperty_ShouldAcceptValidStrings()
    {
        // Arrange
        var task = new TaskItem();
        var description = "Valid Task Description";

        // Act
        task.Description = description;

        // Assert
        Assert.That(task.Description, Is.EqualTo(description));
    }

    [Test]
    public void TaskItem_DescriptionProperty_ShouldAcceptNullValue()
    {
        // Arrange
        var task = new TaskItem();

        // Act
        task.Description = null;

        // Assert
        Assert.That(task.Description, Is.Null);
    }

    [Test]
    public void TaskItem_DateProperties_ShouldAcceptValidDateTimes()
    {
        // Arrange
        var task = new TaskItem();
        var dueDate = DateTime.UtcNow.AddDays(7);
        var createdAt = DateTime.UtcNow;

        // Act
        task.DueDate = dueDate;
        task.CreatedAt = createdAt;

        // Assert
        Assert.That(task.DueDate, Is.EqualTo(dueDate));
        Assert.That(task.CreatedAt, Is.EqualTo(createdAt));
    }

    [Test]
    public void TaskItem_GuidProperties_ShouldAcceptValidGuids()
    {
        // Arrange
        var task = new TaskItem();
        var id = Guid.NewGuid();
        var createdById = Guid.NewGuid();
        var assignedToId = Guid.NewGuid();

        // Act
        task.Id = id;
        task.CreatedById = createdById;
        task.AssignedToId = assignedToId;

        // Assert
        Assert.That(task.Id, Is.EqualTo(id));
        Assert.That(task.CreatedById, Is.EqualTo(createdById));
        Assert.That(task.AssignedToId, Is.EqualTo(assignedToId));
    }

    [Test]
    public void TaskItem_ToString_ShouldReturnExpectedFormat()
    {
        // Arrange
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Test Task",
            Status = DomainTaskStatus.InProgress
        };

        // Act
        var result = task.ToString();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Not.Empty);
        // Note: Since ToString() is not overridden, it will return the type name
        Assert.That(result, Does.Contain("TaskItem"));
    }
}

/// <summary>
/// Unit tests for TaskStatus enumeration
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
public class TaskStatusTests
{
    [Test]
    public void TaskStatus_AllValues_ShouldBeAvailable()
    {
        // Arrange
        var expectedValues = new[] { DomainTaskStatus.New, DomainTaskStatus.InProgress, DomainTaskStatus.Completed, DomainTaskStatus.Failed };

        // Act
        var actualValues = Enum.GetValues<DomainTaskStatus>();

        // Assert
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

    [Test]
    [TestCase(DomainTaskStatus.New, 0)]
    [TestCase(DomainTaskStatus.InProgress, 1)]
    [TestCase(DomainTaskStatus.Completed, 2)]
    [TestCase(DomainTaskStatus.Failed, 3)]
    public void TaskStatus_Values_ShouldHaveCorrectNumericValues(DomainTaskStatus status, int expectedValue)
    {
        // Act
        var actualValue = (int)status;

        // Assert
        Assert.That(actualValue, Is.EqualTo(expectedValue));
    }

    [Test]
    [TestCase("New", DomainTaskStatus.New)]
    [TestCase("InProgress", DomainTaskStatus.InProgress)]
    [TestCase("Completed", DomainTaskStatus.Completed)]
    [TestCase("Failed", DomainTaskStatus.Failed)]
    public void TaskStatus_Parse_ShouldConvertFromString(string statusString, DomainTaskStatus expectedStatus)
    {
        // Act
        var actualStatus = Enum.Parse<DomainTaskStatus>(statusString);

        // Assert
        Assert.That(actualStatus, Is.EqualTo(expectedStatus));
    }

    [Test]
    [TestCase(DomainTaskStatus.New, "New")]
    [TestCase(DomainTaskStatus.InProgress, "InProgress")]
    [TestCase(DomainTaskStatus.Completed, "Completed")]
    [TestCase(DomainTaskStatus.Failed, "Failed")]
    public void TaskStatus_ToString_ShouldReturnCorrectString(DomainTaskStatus status, string expectedString)
    {
        // Act
        var actualString = status.ToString();

        // Assert
        Assert.That(actualString, Is.EqualTo(expectedString));
    }

    [Test]
    public void TaskStatus_InvalidParse_ShouldThrowException()
    {
        // Arrange
        var invalidStatus = "InvalidStatus";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Enum.Parse<DomainTaskStatus>(invalidStatus));
    }

    [Test]
    public void TaskStatus_TryParse_ShouldReturnCorrectResults()
    {
        // Act & Assert - Valid values
        Assert.That(Enum.TryParse<DomainTaskStatus>("New", out var newStatus), Is.True);
        Assert.That(newStatus, Is.EqualTo(DomainTaskStatus.New));

        Assert.That(Enum.TryParse<DomainTaskStatus>("InProgress", out var inProgressStatus), Is.True);
        Assert.That(inProgressStatus, Is.EqualTo(DomainTaskStatus.InProgress));

        // Act & Assert - Invalid values
        Assert.That(Enum.TryParse<DomainTaskStatus>("Invalid", out var invalidStatus), Is.False);
        Assert.That(invalidStatus, Is.EqualTo(default(DomainTaskStatus)));
    }
}