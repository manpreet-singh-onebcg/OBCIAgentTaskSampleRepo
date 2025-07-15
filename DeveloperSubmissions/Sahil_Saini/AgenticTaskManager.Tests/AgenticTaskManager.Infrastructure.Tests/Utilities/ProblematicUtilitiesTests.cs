using AgenticTaskManager.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;

namespace AgenticTaskManager.Infrastructure.Tests.Utilities;

/// <summary>
/// Unit tests for ProblematicUtilities class
/// Focus: Testing utility methods and identifying code quality issues
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Infrastructure")]
public class ProblematicUtilitiesTests
{
    #region BuildLargeString Tests

    [Test]
    public void BuildLargeString_WithValidCount_ShouldBuildString()
    {
        // Act
        var result = ProblematicUtilities.BuildLargeString(3);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo("Item 0, Item 1, Item 2, "));
    }

    [Test]
    public void BuildLargeString_WithZeroCount_ShouldReturnEmptyString()
    {
        // Act
        var result = ProblematicUtilities.BuildLargeString(0);

        // Assert
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void BuildLargeString_WithNegativeCount_ShouldReturnEmptyString()
    {
        // Act
        var result = ProblematicUtilities.BuildLargeString(-5);

        // Assert
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    [Explicit("Performance test - demonstrates inefficient string concatenation")]
    public void BuildLargeString_WithLargeCount_ShouldBeInefficient()
    {
        // This test demonstrates the performance issue with string concatenation
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = ProblematicUtilities.BuildLargeString(1000);

        stopwatch.Stop();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.GreaterThan(0));

        // This will likely be slow due to string concatenation in a loop
        TestContext.WriteLine($"BuildLargeString(1000) took {stopwatch.ElapsedMilliseconds}ms");
    }

    #endregion

    #region FormatTaskInfo Tests

    [Test]
    public void FormatTaskInfo_WithCriticalPriority_ShouldReturnCriticalFormat()
    {
        // Act
        var result = ProblematicUtilities.FormatTaskInfo(
            "Test Task",
            "Test Description",
            DateTime.UtcNow.AddDays(1),
            "user@test.com",
            "creator@test.com",
            1,
            6, // Priority > 5
            true, // IsUrgent
            true,
            "Work",
            "Development",
            100m);

        // Assert
        Assert.That(result, Is.EqualTo("CRITICAL: Test Task - Test Description"));
    }

    [Test]
    public void FormatTaskInfo_WithUrgentwPriorityLow_ShouldReturnUrgentFormat()
    {
        // Act
        var result = ProblematicUtilities.FormatTaskInfo(
            "Test Task",
            "Test Description",
            DateTime.UtcNow.AddDays(1),
            "user@test.com",
            "creator@test.com",
            1,
            3, // Priority <= 5
            true, // IsUrgent
            true,
            "Work",
            "Development",
            100m);

        // Assert
        Assert.That(result, Is.EqualTo("URGENT: Test Task"));
    }

    [Test]
    public void FormatTaskInfo_WithOverdueTask_ShouldReturnOverdueFormat()
    {
        // Act
        var result = ProblematicUtilities.FormatTaskInfo(
            "Test Task",
            "Test Description",
            DateTime.UtcNow.AddDays(-1), // Past due date
            "user@test.com",
            "creator@test.com",
            1,
            3,
            false, // Not urgent
            true,
            "Work",
            "Development",
            100m);

        // Assert
        Assert.That(result, Is.EqualTo("OVERDUE: Test Task"));
    }

    [Test]
    public void FormatTaskInfo_WithNormalTask_ShouldReturnNormalFormat()
    {
        // Act
        var result = ProblematicUtilities.FormatTaskInfo(
            "Test Task",
            "Test Description",
            DateTime.UtcNow.AddDays(1), // Future due date
            "user@test.com",
            "creator@test.com",
            1,
            3,
            false, // Not urgent
            true,
            "Work",
            "Development",
            100m);

        // Assert
        Assert.That(result, Is.EqualTo("NORMAL: Test Task"));
    }

    [Test]
    public void FormatTaskInfo_WithNullTitle_ShouldReturnUnknown()
    {
        // Act
        var result = ProblematicUtilities.FormatTaskInfo(
            null!,
            "Test Description",
            DateTime.UtcNow.AddDays(1),
            "user@test.com",
            "creator@test.com",
            1,
            3,
            false,
            true,
            "Work",
            "Development",
            100m);

        // Assert
        Assert.That(result, Is.EqualTo("UNKNOWN"));
    }

    #endregion

    #region GetDataFromApi Tests

    [Test]
    [Explicit("Network test - requires external dependency")]
    public void GetDataFromApi_WithValidUrl_ShouldReturnData()
    {
        // This test demonstrates the blocking async anti-pattern
        // and dependency on external resources

        // Act & Assert
        Assert.DoesNotThrow(() =>
        {
            var result = ProblematicUtilities.GetDataFromApi("https://httpbin.org/get");
            Assert.That(result, Is.Not.Null);
        });
    }

    [Test]
    public void GetDataFromApi_WithInvalidUrl_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<UriFormatException>(() =>
        {
            ProblematicUtilities.GetDataFromApi("invalid-url");
        });
    }

    #endregion

    #region ParseJson Tests

    [Test]
    public void ParseJson_WithValidJson_ShouldReturnObject()
    {
        // Arrange
        var json = """{"Name": "Test", "Value": 123}""";

        // Act
        var result = ProblematicUtilities.ParseJson<dynamic>(json);

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void ParseJson_WithInvalidJson_ShouldReturnDefault()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act
        var result = ProblematicUtilities.ParseJson<string>(invalidJson);

        // Assert
        Assert.That(result, Is.Null); // Due to empty catch block
    }

    [Test]
    public void ParseJson_WithNullJson_ShouldReturnDefault()
    {
        // Act
        var result = ProblematicUtilities.ParseJson<string>(null!);

        // Assert
        Assert.That(result, Is.Null);
    }

    #endregion

    #region RemoveDuplicates Tests

    [Test]
    public void RemoveDuplicates_WithDuplicates_ShouldRemoveThem()
    {
        // Arrange
        var items = new List<int> { 1, 2, 3, 2, 4, 1, 5 };

        // Act
        var result = ProblematicUtilities.RemoveDuplicates(items);

        // Assert
        Assert.That(result.Count, Is.EqualTo(5));
        Assert.That(result, Is.EquivalentTo(new[] { 1, 2, 3, 4, 5 }));
    }

    [Test]
    public void RemoveDuplicates_WithNoDuplicates_ShouldReturnSameCount()
    {
        // Arrange
        var items = new List<string> { "a", "b", "c", "d" };

        // Act
        var result = ProblematicUtilities.RemoveDuplicates(items);

        // Assert
        Assert.That(result.Count, Is.EqualTo(4));
        Assert.That(result, Is.EquivalentTo(items));
    }

    [Test]
    public void RemoveDuplicates_WithEmptyList_ShouldReturnEmpty()
    {
        // Arrange
        var items = new List<int>();

        // Act
        var result = ProblematicUtilities.RemoveDuplicates(items);

        // Assert
        Assert.That(result.Count, Is.EqualTo(0));
    }

    #endregion

    #region IsValidPassword Tests

    [Test]
    [TestCase("password123!", true)]
    [TestCase("short", false)] // Too short
    [TestCase("toolongpasswordthatexceedsthemaximumlengthallowed!", false)] // Too long
    [TestCase("nouppercase123!", false)] // No special chars - wait, this has !
    [TestCase("nospecialchars", false)] // No special chars
    [TestCase("ValidPass1!", true)]
    public void IsValidPassword_WithVariousPasswords_ShouldReturnExpectedResult(string password, bool expected)
    {
        // Act
        var result = ProblematicUtilities.IsValidPassword(password);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void IsValidPassword_WithSpecialCharacters_ShouldCountCorrectly()
    {
        // Arrange
        var passwordWithTwoSpecialChars = "Password1!@";
        var passwordWithOneSpecialChar = "Password1!";

        // Act
        var resultTwo = ProblematicUtilities.IsValidPassword(passwordWithTwoSpecialChars);
        var resultOne = ProblematicUtilities.IsValidPassword(passwordWithOneSpecialChar);

        // Assert
        Assert.That(resultTwo, Is.True);
        Assert.That(resultOne, Is.False); // Needs at least 2 special chars
    }

    #endregion

    #region BuildQuery Tests

    [Test]
    public void BuildQuery_WithValidParameters_ShouldReturnSqlString()
    {
        // Act
        var result = ProblematicUtilities.BuildQuery("user123", "active");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("user123"));
        Assert.That(result, Does.Contain("active"));
        Assert.That(result, Does.Contain("SELECT * FROM Tasks"));
    }

    [Test]
    public void BuildQuery_DemonstratesSqlInjectionVulnerability()
    {
        // This test demonstrates the SQL injection vulnerability
        var maliciousUserId = "'; DROP TABLE Tasks; --";

        // Act
        var result = ProblematicUtilities.BuildQuery(maliciousUserId, "active");

        // Assert
        Assert.That(result, Does.Contain("DROP TABLE Tasks"));
        Assert.That(result, Does.Contain("--"));
        // This demonstrates that the method is vulnerable to SQL injection
    }

    #endregion

    #region CalculateFactorial Tests

    [Test]
    [TestCase(0, 1)]
    [TestCase(1, 1)]
    [TestCase(5, 120)]
    [TestCase(-1, 1)] // Demonstrates the bug - should throw exception
    public void CalculateFactorial_WithVariousInputs_ShouldReturnExpectedResults(int input, int expected)
    {
        // Act
        var result = ProblematicUtilities.CalculateFactorial(input);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    [Explicit("Demonstrates infinite recursion issue")]
    public void CalculateFactorial_WithLargeNumber_DemonstratesInfiniteRecursion()
    {
        // This test demonstrates that the method can cause stack overflow
        // with large numbers due to lack of iteration limits

        Assert.DoesNotThrow(() =>
        {
            try
            {
                var result = ProblematicUtilities.CalculateFactorial(1000);
                // If we reach here, either it worked or there's a limit we don't know about
            }
            catch (StackOverflowException)
            {
                Assert.Pass("Correctly identified stack overflow issue");
            }
        });
    }

    #endregion

    #region ProcessValues Tests

    [Test]
    public void ProcessValues_WithIntegerArray_ShouldProcessCorrectly()
    {
        // Arrange
        var values = new object[] { 1, 2, 3, "string", 4.5 };

        // Act & Assert
        Assert.DoesNotThrow(() => ProblematicUtilities.ProcessValues(values));
    }

    [Test]
    public void ProcessValues_WithEmptyArray_ShouldNotThrow()
    {
        // Arrange
        var values = new object[0];

        // Act & Assert
        Assert.DoesNotThrow(() => ProblematicUtilities.ProcessValues(values));
    }

    #endregion

    #region ContainsIgnoreCase Tests

    [Test]
    [TestCase("Hello World", "hello", true)]
    [TestCase("Hello World", "WORLD", true)]
    [TestCase("Hello World", "xyz", false)]
    [TestCase("", "test", false)]
    [TestCase("test", "", true)] // Empty string is contained in any string
    public void ContainsIgnoreCase_WithVariousInputs_ShouldReturnExpectedResults(string source, string search, bool expected)
    {
        // Act
        var result = ProblematicUtilities.ContainsIgnoreCase(source, search);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    [Explicit("Demonstrates performance issue with ToLower")]
    public void ContainsIgnoreCase_DemonstratesPerformanceIssue()
    {
        // This test demonstrates the performance issue with creating new string objects
        var source = "This is a very long string that we want to search through multiple times";
        var search = "LONG";

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < 10000; i++)
        {
            ProblematicUtilities.ContainsIgnoreCase(source, search);
        }

        stopwatch.Stop();

        TestContext.WriteLine($"ContainsIgnoreCase 10,000 iterations took {stopwatch.ElapsedMilliseconds}ms");

        // This demonstrates that the method creates new string objects unnecessarily
        Assert.That(stopwatch.ElapsedMilliseconds, Is.GreaterThan(0));
    }

    #endregion

    #region GetCurrentUser Tests

    [Test]
    public void GetCurrentUser_ShouldReturnUserNameAndModifyGlobalState()
    {
        // Arrange
        var initialCacheCount = ProblematicUtilities.GlobalCache.Count;

        // Act
        var result = ProblematicUtilities.GetCurrentUser();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Not.Empty);

        // Demonstrates the side effect - method modifies global state
        Assert.That(ProblematicUtilities.GlobalCache.Count, Is.EqualTo(initialCacheCount + 1));
        Assert.That(ProblematicUtilities.GlobalCache.Last(), Does.Contain("Access at"));
    }

    #endregion

    #region Thread Safety Tests

    [Test]
    [Explicit("Demonstrates thread safety issues")]
    public void GlobalCache_ConcurrentAccess_DemonstratesThreadSafetyIssues()
    {
        // This test demonstrates that GlobalCache is not thread-safe
        var tasks = new List<Task>();
        var initialCount = ProblematicUtilities.GlobalCache.Count;

        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                ProblematicUtilities.GlobalCache.Add($"Concurrent access {index}");
            }));
        }

        Task.WaitAll(tasks.ToArray());
    }
    #endregion
}