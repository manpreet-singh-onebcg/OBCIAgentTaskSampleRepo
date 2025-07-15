using AgenticTaskManager.Infrastructure.Utilities;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace AgenticTaskManager.Infrastructure.Tests;

public class ProblematicUtilitiesTests
{
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<ProblematicUtilities>> _mockLogger;
    private readonly ProblematicUtilities _utilities;

    public ProblematicUtilitiesTests()
    {
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"PasswordValidation:MinLength", "8"},
            {"PasswordValidation:MaxLength", "50"},
            {"PasswordValidation:RequiredSpecialChars", "2"},
            {"PasswordValidation:SpecialCharacterSet", "!@#$%^&*()"},
            {"Security:ApiKey", "test-api-key-123"},
            {"ConnectionStrings:DefaultConnection", "Server=localhost;Database=TestDB;Trusted_Connection=true;"}
        });
        
        _configuration = configurationBuilder.Build();
        _mockLogger = new Mock<ILogger<ProblematicUtilities>>();
        _utilities = new ProblematicUtilities(_configuration, _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidConfiguration_SetsPasswordSettings()
    {
        // Act & Assert
        _utilities.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new ProblematicUtilities(null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("configuration");
    }

    [Fact]
    public void Constructor_WithMissingPasswordSettings_UsesDefaults()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder().Build();

        // Act
        var utilities = new ProblematicUtilities(emptyConfig);

        // Assert
        utilities.Should().NotBeNull();
        utilities.IsValidPassword("TestPass123!@").Should().BeTrue();
    }

    #endregion

    #region BuildLargeString Tests

    [Fact]
    public void BuildLargeString_WithPositiveCount_ReturnsFormattedString()
    {
        // Act
        var result = _utilities.BuildLargeString(3);

        // Assert
        result.Should().Be("Item 0, Item 1, Item 2");
    }

    [Fact]
    public void BuildLargeString_WithZeroCount_ReturnsEmptyString()
    {
        // Act
        var result = _utilities.BuildLargeString(0);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildLargeString_WithNegativeCount_ReturnsEmptyString()
    {
        // Act
        var result = _utilities.BuildLargeString(-5);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region FormatTaskInfo Tests

    [Fact]
    public void FormatTaskInfo_WithNullRequest_ReturnsUnknown()
    {
        // Act
        var result = _utilities.FormatTaskInfo(null);

        // Assert
        result.Should().Be("UNKNOWN");
    }

    [Fact]
    public void FormatTaskInfo_WithCriticalTask_ReturnsCriticalFormat()
    {
        // Arrange
        var request = new TaskInfoRequest
        {
            Title = "Critical Task",
            Description = "Urgent fix needed",
            IsUrgent = true,
            Priority = 10
        };

        // Act
        var result = _utilities.FormatTaskInfo(request);

        // Assert
        result.Should().Be("CRITICAL: Critical Task - Urgent fix needed");
    }

    [Fact]
    public void FormatTaskInfo_WithUrgentTask_ReturnsUrgentFormat()
    {
        // Arrange
        var request = new TaskInfoRequest
        {
            Title = "Urgent Task",
            IsUrgent = true,
            Priority = 3
        };

        // Act
        var result = _utilities.FormatTaskInfo(request);

        // Assert
        result.Should().Be("URGENT: Urgent Task");
    }

    [Fact]
    public void FormatTaskInfo_WithOverdueTask_ReturnsOverdueFormat()
    {
        // Arrange
        var request = new TaskInfoRequest
        {
            Title = "Overdue Task",
            DueDate = DateTime.Now.AddDays(-1),
            IsUrgent = false
        };

        // Act
        var result = _utilities.FormatTaskInfo(request);

        // Assert
        result.Should().Be("OVERDUE: Overdue Task");
    }

    [Fact]
    public void FormatTaskInfo_WithNormalTask_ReturnsNormalFormat()
    {
        // Arrange
        var request = new TaskInfoRequest
        {
            Title = "Normal Task",
            DueDate = DateTime.Now.AddDays(1),
            IsUrgent = false
        };

        // Act
        var result = _utilities.FormatTaskInfo(request);

        // Assert
        result.Should().Be("NORMAL: Normal Task");
    }

    #endregion

    #region GetDataFromApiAsync Tests

    [Fact]
    public async Task GetDataFromApiAsync_WithNullUrl_ThrowsArgumentException()
    {
        // Act & Assert
        await FluentActions.Invoking(() => _utilities.GetDataFromApiAsync(null!))
            .Should().ThrowAsync<ArgumentException>()
            .WithParameterName("url");
    }

    [Fact]
    public async Task GetDataFromApiAsync_WithEmptyUrl_ThrowsArgumentException()
    {
        // Act & Assert
        await FluentActions.Invoking(() => _utilities.GetDataFromApiAsync(""))
            .Should().ThrowAsync<ArgumentException>()
            .WithParameterName("url");
    }

    // Note: Testing actual HTTP calls would require mocking HttpClient, 
    // which is complex and beyond basic unit testing scope

    #endregion

    #region WriteToFile Tests

    [Fact]
    public void WriteToFile_WithNullContent_ThrowsArgumentException()
    {
        // Act & Assert
        Action act = () => _utilities.WriteToFile(null!, "test.txt");
        act.Should().Throw<ArgumentException>()
           .WithParameterName("content");
    }

    [Fact]
    public void WriteToFile_WithNullFileName_ThrowsArgumentException()
    {
        // Act & Assert
        Action act = () => _utilities.WriteToFile("content", null!);
        act.Should().Throw<ArgumentException>()
           .WithParameterName("fileName");
    }

    [Fact]
    public void WriteToFile_WithEmptyContent_ThrowsArgumentException()
    {
        // Act & Assert
        Action act = () => _utilities.WriteToFile("", "test.txt");
        act.Should().Throw<ArgumentException>()
           .WithParameterName("content");
    }

    [Fact]
    public void WriteToFile_WithEmptyFileName_ThrowsArgumentException()
    {
        // Act & Assert
        Action act = () => _utilities.WriteToFile("content", "");
        act.Should().Throw<ArgumentException>()
           .WithParameterName("fileName");
    }

    #endregion

    #region ParseJson Tests

    [Fact]
    public void ParseJson_WithValidJson_ReturnsDeserializedObject()
    {
        // Arrange
        var json = """{"Name":"John","Age":30}""";

        // Act
        var result = _utilities.ParseJson<TestModel>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void ParseJson_WithInvalidJson_ReturnsDefault()
    {
        // Arrange
        var invalidJson = "{invalid json}";

        // Act
        var result = _utilities.ParseJson<TestModel>(invalidJson);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseJson_WithNullJson_ReturnsDefault()
    {
        // Act
        var result = _utilities.ParseJson<TestModel>(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseJson_WithEmptyJson_ReturnsDefault()
    {
        // Act
        var result = _utilities.ParseJson<TestModel>("");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region RemoveDuplicates Tests

    [Fact]
    public void RemoveDuplicates_WithNullList_ReturnsEmptyList()
    {
        // Act
        var result = ProblematicUtilities.RemoveDuplicates<int>(null!);

        // Assert
        result.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void RemoveDuplicates_WithDuplicates_ReturnsUniqueItems()
    {
        // Arrange
        var items = new List<int> { 1, 2, 2, 3, 1, 4, 3 };

        // Act
        var result = ProblematicUtilities.RemoveDuplicates(items);

        // Assert
        result.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void RemoveDuplicates_WithNoDuplicates_ReturnsSameItems()
    {
        // Arrange
        var items = new List<string> { "apple", "banana", "cherry" };

        // Act
        var result = ProblematicUtilities.RemoveDuplicates(items);

        // Assert
        result.Should().Equal("apple", "banana", "cherry");
    }

    #endregion

    #region IsValidPassword Tests

    [Theory]
    [InlineData("ValidPass123!@", true)]
    [InlineData("Short1!", false)]  // Too short
    [InlineData("ValidPassword123", false)]  // No special chars
    [InlineData("Valid123!", false)]  // Only 1 special char
    [InlineData("", false)]  // Empty
    [InlineData(null, false)]  // Null
    public void IsValidPassword_WithVariousPasswords_ReturnsExpectedResult(string password, bool expected)
    {
        // Act
        var result = _utilities.IsValidPassword(password);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region BuildSecureQuery Tests

    [Fact]
    public void BuildSecureQuery_WithValidParameters_ReturnsParameterizedQuery()
    {
        // Act
        var result = _utilities.BuildSecureQuery("user123", "active");

        // Assert
        result.Should().Be("SELECT * FROM Tasks WHERE UserId = @userId AND Status = @status");
    }

    [Theory]
    [InlineData(null, "active")]
    [InlineData("", "active")]
    [InlineData("user123", null)]
    [InlineData("user123", "")]
    public void BuildSecureQuery_WithInvalidParameters_ThrowsArgumentException(string userId, string status)
    {
        // Act & Assert
        Action act = () => _utilities.BuildSecureQuery(userId, status);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region CalculateFactorial Tests

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(5, 120)]
    [InlineData(10, 3628800)]
    public void CalculateFactorial_WithValidNumbers_ReturnsCorrectResult(int input, long expected)
    {
        // Act
        var result = ProblematicUtilities.CalculateFactorial(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CalculateFactorial_WithNegativeNumber_ThrowsArgumentException()
    {
        // Act & Assert
        Action act = () => ProblematicUtilities.CalculateFactorial(-1);
        act.Should().Throw<ArgumentException>()
           .WithParameterName("n");
    }

    [Fact]
    public void CalculateFactorial_WithLargeNumber_ThrowsArgumentException()
    {
        // Act & Assert
        Action act = () => ProblematicUtilities.CalculateFactorial(25);
        act.Should().Throw<ArgumentException>()
           .WithParameterName("n");
    }

    #endregion

    #region ContainsIgnoreCase Tests

    [Theory]
    [InlineData("Hello World", "WORLD", true)]
    [InlineData("Hello World", "xyz", false)]
    [InlineData("Test", "test", true)]
    [InlineData("", "test", false)]
    [InlineData("test", "", false)]
    [InlineData(null, "test", false)]
    [InlineData("test", null, false)]
    public void ContainsIgnoreCase_WithVariousInputs_ReturnsExpectedResult(string source, string search, bool expected)
    {
        // Act
        var result = ProblematicUtilities.ContainsIgnoreCase(source, search);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region GetCurrentUserWithAudit Tests

    [Fact]
    public void GetCurrentUserWithAudit_ReturnsCurrentUser()
    {
        // Act
        var result = _utilities.GetCurrentUserWithAudit();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Be(Environment.UserName);
    }

    #endregion

    #region ProcessValues Tests

    [Fact]
    public void ProcessValues_WithIntArray_LogsValues()
    {
        // Arrange
        var values = new[] { 1, 2, 3 };

        // Act
        _utilities.ProcessValues(values);

        // Assert - Verify logging occurred (basic verification)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing value")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3));
    }

    [Fact]
    public void ProcessMixedValues_WithMixedArray_LogsValues()
    {
        // Arrange
        var values = new object[] { 1, "test", 3.14 };

        // Act
        _utilities.ProcessMixedValues(values);

        // Assert - Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Integer") || 
                                              v.ToString()!.Contains("String") || 
                                              v.ToString()!.Contains("Double")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3));
    }

    #endregion

    // Test model for JSON parsing tests
    private class TestModel
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }
}
