using AgenticTaskManager.Application.DTOs;

namespace AgenticTaskManager.Application.Tests.DTOs;

/// <summary>
/// Unit tests for TaskDto data transfer object
/// Focus: Data validation and mapping logic
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Application")]
public class TaskDtoTests
{
    [Test]
    public void TaskDto_DefaultConstructor_ShouldCreateInstanceWithDefaults()
    {
        // Arrange & Act
        var dto = new TaskDto();

        // Assert
        Assert.That(dto.Title, Is.Null);
        Assert.That(dto.Description, Is.Null);
        Assert.That(dto.CreatedById, Is.EqualTo(Guid.Empty));
        Assert.That(dto.AssignedToId, Is.EqualTo(Guid.Empty));
        Assert.That(dto.DueDate, Is.EqualTo(DateTime.MinValue));
    }

    [Test]
    public void TaskDto_WithValidData_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var title = "Test Task";
        var description = "Test Description";
        var createdById = Guid.NewGuid();
        var assignedToId = Guid.NewGuid();
        var dueDate = DateTime.UtcNow.AddDays(7);

        // Act
        var dto = new TaskDto
        {
            Title = title,
            Description = description,
            CreatedById = createdById,
            AssignedToId = assignedToId,
            DueDate = dueDate
        };

        // Assert
        Assert.That(dto.Title, Is.EqualTo(title));
        Assert.That(dto.Description, Is.EqualTo(description));
        Assert.That(dto.CreatedById, Is.EqualTo(createdById));
        Assert.That(dto.AssignedToId, Is.EqualTo(assignedToId));
        Assert.That(dto.DueDate, Is.EqualTo(dueDate));
    }

    [Test]
    public void TaskDto_TitleProperty_ShouldAcceptNullAndEmptyValues()
    {
        // Arrange
        var dto = new TaskDto();

        // Act & Assert - Null
        dto.Title = null;
        Assert.That(dto.Title, Is.Null);

        // Act & Assert - Empty
        dto.Title = string.Empty;
        Assert.That(dto.Title, Is.EqualTo(string.Empty));

        // Act & Assert - Whitespace
        dto.Title = "   ";
        Assert.That(dto.Title, Is.EqualTo("   "));
    }

    [Test]
    public void TaskDto_DescriptionProperty_ShouldAcceptNullAndEmptyValues()
    {
        // Arrange
        var dto = new TaskDto();

        // Act & Assert - Null
        dto.Description = null;
        Assert.That(dto.Description, Is.Null);

        // Act & Assert - Empty
        dto.Description = string.Empty;
        Assert.That(dto.Description, Is.EqualTo(string.Empty));
    }

    [Test]
    public void TaskDto_GuidProperties_ShouldAcceptValidGuids()
    {
        // Arrange
        var dto = new TaskDto();
        var createdById = Guid.NewGuid();
        var assignedToId = Guid.NewGuid();

        // Act
        dto.CreatedById = createdById;
        dto.AssignedToId = assignedToId;

        // Assert
        Assert.That(dto.CreatedById, Is.EqualTo(createdById));
        Assert.That(dto.AssignedToId, Is.EqualTo(assignedToId));
    }

    [Test]
    public void TaskDto_GuidProperties_ShouldAcceptEmptyGuids()
    {
        // Arrange
        var dto = new TaskDto();

        // Act
        dto.CreatedById = Guid.Empty;
        dto.AssignedToId = Guid.Empty;

        // Assert
        Assert.That(dto.CreatedById, Is.EqualTo(Guid.Empty));
        Assert.That(dto.AssignedToId, Is.EqualTo(Guid.Empty));
    }

    [Test]
    public void TaskDto_DueDateProperty_ShouldAcceptValidDateTimes()
    {
        // Arrange
        var dto = new TaskDto();
        var futureDate = DateTime.UtcNow.AddDays(7);
        var pastDate = DateTime.UtcNow.AddDays(-7);

        // Act & Assert - Future date
        dto.DueDate = futureDate;
        Assert.That(dto.DueDate, Is.EqualTo(futureDate));

        // Act & Assert - Past date
        dto.DueDate = pastDate;
        Assert.That(dto.DueDate, Is.EqualTo(pastDate));

        // Act & Assert - Current date
        var now = DateTime.UtcNow;
        dto.DueDate = now;
        Assert.That(dto.DueDate, Is.EqualTo(now));
    }

    [Test]
    public void TaskDto_Properties_ShouldBeIndependent()
    {
        // Arrange
        var dto1 = new TaskDto { Title = "Task 1", CreatedById = Guid.NewGuid() };
        var dto2 = new TaskDto { Title = "Task 2", CreatedById = Guid.NewGuid() };

        // Act & Assert
        Assert.That(dto1.Title, Is.Not.EqualTo(dto2.Title));
        Assert.That(dto1.CreatedById, Is.Not.EqualTo(dto2.CreatedById));
    }

    [Test]
    public void TaskDto_Equality_ShouldWorkByReference()
    {
        // Arrange
        var dto1 = new TaskDto { Title = "Same Title", CreatedById = Guid.NewGuid() };
        var dto2 = new TaskDto { Title = "Same Title", CreatedById = dto1.CreatedById };
        var dto3 = dto1;

        // Act & Assert
        Assert.That(dto1.Equals(dto2), Is.False, "Different instances should not be equal even with same values");
        Assert.That(dto1.Equals(dto3), Is.True, "Same reference should be equal");
        Assert.That(ReferenceEquals(dto1, dto3), Is.True, "Same reference should be equal");
    }

    [Test]
    [TestCase("A")]
    [TestCase("Short")]
    [TestCase("This is a medium length task title")]
    [TestCase("This is a very long task title that might be used in real-world scenarios with detailed descriptions")]
    public void TaskDto_Title_ShouldAcceptVariousLengths(string title)
    {
        // Arrange
        var dto = new TaskDto();

        // Act
        dto.Title = title;

        // Assert
        Assert.That(dto.Title, Is.EqualTo(title));
    }

    [Test]
    public void TaskDto_WithUnicodeCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var dto = new TaskDto();
        var unicodeTitle = "???? ?? ???? Ñuño";
        var unicodeDescription = "Description with émojis ?? and spëcial chars";

        // Act
        dto.Title = unicodeTitle;
        dto.Description = unicodeDescription;

        // Assert
        Assert.That(dto.Title, Is.EqualTo(unicodeTitle));
        Assert.That(dto.Description, Is.EqualTo(unicodeDescription));
    }

    [Test]
    public void TaskDto_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var dto = new TaskDto();
        var titleWithQuotes = "Task with \"quotes\" and 'apostrophes'";
        var descriptionWithHtml = "Description with <tags> & ampersand";

        // Act
        dto.Title = titleWithQuotes;
        dto.Description = descriptionWithHtml;

        // Assert
        Assert.That(dto.Title, Is.EqualTo(titleWithQuotes));
        Assert.That(dto.Description, Is.EqualTo(descriptionWithHtml));
    }
}