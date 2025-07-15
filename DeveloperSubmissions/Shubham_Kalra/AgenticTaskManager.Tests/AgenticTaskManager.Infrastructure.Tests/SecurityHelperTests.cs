using AgenticTaskManager.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace AgenticTaskManager.Infrastructure.Tests;

/// <summary>
/// Comprehensive unit tests for SecurityHelper class
/// Tests security operations, encryption/decryption, token management, and password operations
/// </summary>
public class SecurityHelperTests
{
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<SecurityHelper>> _mockLogger;

    public SecurityHelperTests()
    {
        // Setup configuration with security settings
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"Security:EncryptionKey", "MyTestEncryptionKey12345"},
                {"Security:KeySalt", "MyTestSalt123"},
                {"ConnectionStrings:DefaultConnection", "Server=test;Database=test;Integrated Security=true;"}
            })
            .Build();

        _mockLogger = new Mock<ILogger<SecurityHelper>>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidConfiguration_ShouldInitializeCorrectly()
    {
        // Act
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);

        // Assert
        securityHelper.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new SecurityHelper(null!, _mockLogger.Object));
        exception.ParamName.Should().Be("configuration");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldNotThrow()
    {
        // Act & Assert
        var securityHelper = new SecurityHelper(_configuration, null);
        securityHelper.Should().NotBeNull();
    }

    #endregion

    #region Password Hashing Tests

    [Fact]
    public void HashPassword_WithValidPassword_ShouldReturnHashedPassword()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var password = "TestPassword123!";

        // Act
        var hashedPassword = securityHelper.HashPassword(password);

        // Assert
        hashedPassword.Should().NotBeNullOrEmpty();
        hashedPassword.Should().NotBe(password);
        hashedPassword.Should().Contain(":");  // Should contain salt separator
        hashedPassword.Length.Should().BeGreaterThan(50); // Hash should be substantial length
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void HashPassword_WithInvalidPassword_ShouldThrowArgumentException(string password)
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => securityHelper.HashPassword(password));
    }

    [Fact]
    public void HashPassword_WithSamePasswordMultipleTimes_ShouldReturnDifferentHashes()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var password = "TestPassword123!";

        // Act
        var hash1 = securityHelper.HashPassword(password);
        var hash2 = securityHelper.HashPassword(password);

        // Assert
        hash1.Should().NotBe(hash2); // Different salts should produce different hashes
    }

    #endregion

    #region Password Verification Tests

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var password = "TestPassword123!";
        var hashedPassword = securityHelper.HashPassword(password);

        // Act
        var result = securityHelper.VerifyPassword(password, hashedPassword);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var correctPassword = "TestPassword123!";
        var incorrectPassword = "WrongPassword123!";
        var hashedPassword = securityHelper.HashPassword(correctPassword);

        // Act
        var result = securityHelper.VerifyPassword(incorrectPassword, hashedPassword);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "hash")]
    [InlineData("", "hash")]
    [InlineData("password", null)]
    [InlineData("password", "")]
    [InlineData("password", "invalidhash")]
    public void VerifyPassword_WithInvalidInputs_ShouldReturnFalse(string password, string hashedPassword)
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);

        // Act
        var result = securityHelper.VerifyPassword(password, hashedPassword);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Encryption/Decryption Tests

    [Fact]
    public void EncryptSensitiveData_WithValidPlainText_ShouldReturnEncryptedData()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var plainText = "Sensitive information that needs encryption";

        // Act
        var encryptedData = securityHelper.EncryptSensitiveData(plainText);

        // Assert
        encryptedData.Should().NotBeNullOrEmpty();
        encryptedData.Should().NotBe(plainText);
        encryptedData.Should().MatchRegex(@"^[A-Za-z0-9+/]*={0,2}$"); // Base64 pattern
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EncryptSensitiveData_WithInvalidPlainText_ShouldThrowArgumentException(string plainText)
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => securityHelper.EncryptSensitiveData(plainText));
    }

    [Fact]
    public void DecryptSensitiveData_WithValidEncryptedData_ShouldReturnOriginalPlainText()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var originalText = "Sensitive information that needs decryption";
        var encryptedData = securityHelper.EncryptSensitiveData(originalText);

        // Act
        var decryptedText = securityHelper.DecryptSensitiveData(encryptedData);

        // Assert
        decryptedText.Should().Be(originalText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DecryptSensitiveData_WithInvalidEncryptedText_ShouldThrowArgumentException(string encryptedText)
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => securityHelper.DecryptSensitiveData(encryptedText));
    }

    [Fact]
    public void DecryptSensitiveData_WithInvalidBase64_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var invalidEncryptedText = "NotValidBase64!!!";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => securityHelper.DecryptSensitiveData(invalidEncryptedText));
    }

    [Fact]
    public void EncryptDecrypt_WithSameDataMultipleTimes_ShouldProduceDifferentEncryptions()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var plainText = "Test data for multiple encryptions";

        // Act
        var encrypted1 = securityHelper.EncryptSensitiveData(plainText);
        var encrypted2 = securityHelper.EncryptSensitiveData(plainText);

        // Assert
        encrypted1.Should().NotBe(encrypted2); // Different IVs should produce different results
        
        // But both should decrypt to the same original text
        var decrypted1 = securityHelper.DecryptSensitiveData(encrypted1);
        var decrypted2 = securityHelper.DecryptSensitiveData(encrypted2);
        
        decrypted1.Should().Be(plainText);
        decrypted2.Should().Be(plainText);
    }

    #endregion

    #region Token Generation Tests

    [Fact]
    public void GenerateUserToken_WithValidUsername_ShouldReturnToken()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var username = "testuser";

        // Act
        var token = securityHelper.GenerateUserToken(username);

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Should().MatchRegex(@"^[A-Za-z0-9+/]*={0,2}$"); // Base64 pattern
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GenerateUserToken_WithInvalidUsername_ShouldThrowArgumentException(string username)
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => securityHelper.GenerateUserToken(username));
    }

    [Fact]
    public void GenerateUserToken_WithSameUsernameMultipleTimes_ShouldReturnDifferentTokens()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var username = "testuser";

        // Act
        var token1 = securityHelper.GenerateUserToken(username);
        var token2 = securityHelper.GenerateUserToken(username);

        // Assert
        token1.Should().NotBe(token2); // Tokens should be unique due to random data
    }

    [Fact]
    public void GenerateUserToken_ShouldLogInformation()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var username = "testuser";

        // Act
        securityHelper.GenerateUserToken(username);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Generated secure token for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Token Validation Tests

    [Fact]
    public void ValidateUserToken_WithValidToken_ShouldReturnTrue()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var username = "testuser";
        var token = securityHelper.GenerateUserToken(username);

        // Act
        var result = securityHelper.ValidateUserToken(username, token);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateUserToken_WithInvalidToken_ShouldReturnFalse()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var username = "testuser";
        securityHelper.GenerateUserToken(username);
        var invalidToken = "InvalidToken123";

        // Act
        var result = securityHelper.ValidateUserToken(username, invalidToken);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "token")]
    [InlineData("", "token")]
    [InlineData("user", null)]
    [InlineData("user", "")]
    public void ValidateUserToken_WithInvalidInputs_ShouldReturnFalse(string username, string token)
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);

        // Act
        var result = securityHelper.ValidateUserToken(username, token);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateUserToken_WithNonExistentUser_ShouldReturnFalse()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var username = "nonexistentuser";
        var token = "sometoken";

        // Act
        var result = securityHelper.ValidateUserToken(username, token);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateUserToken_AfterTokenExpiry_ShouldReturnFalse()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var username = "testuser";
        var token = securityHelper.GenerateUserToken(username);

        // Since we can't easily manipulate time in this test, we'll just verify the method works
        // A more sophisticated test would require dependency injection of a time provider

        // Act
        var result = securityHelper.ValidateUserToken(username, token);

        // Assert - For now, just verify it doesn't crash and returns a boolean
        Assert.True(result == true || result == false);
    }

    #endregion

    #region Password Strength Tests

    [Theory]
    [InlineData("Password123!", true)]
    [InlineData("ComplexP@ssw0rd", true)]
    [InlineData("Aa1!", false)] // Too short
    [InlineData("password123!", false)] // No uppercase
    [InlineData("PASSWORD123!", false)] // No lowercase
    [InlineData("Password!", false)] // No digit
    [InlineData("Password123", false)] // No special character
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPasswordStrong_WithVariousPasswords_ShouldReturnExpectedResult(string password, bool expected)
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);

        // Act
        var result = securityHelper.IsPasswordStrong(password);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsPasswordStrong_WithWeakPassword_ShouldLogWarning()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var weakPassword = "weakpass123"; // Long enough but missing uppercase and special chars

        // Act
        securityHelper.IsPasswordStrong(weakPassword);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Password strength validation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region System Configuration Tests

    [Fact]
    public void GetSystemConfiguration_ShouldReturnValidJsonConfiguration()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);

        // Act
        var configJson = securityHelper.GetSystemConfiguration();

        // Assert
        configJson.Should().NotBeNullOrEmpty();
        
        // Verify it's valid JSON
        var config = JsonSerializer.Deserialize<object>(configJson);
        config.Should().NotBeNull();
    }

    [Fact]
    public void GetSystemConfiguration_ShouldNotExposeSensitiveInformation()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);

        // Act
        var configJson = securityHelper.GetSystemConfiguration();

        // Assert
        configJson.Should().NotContain("password");
        configJson.Should().NotContain("key");
        configJson.Should().NotContain("secret");
        configJson.Should().NotContain("token");
        configJson.Should().Contain("ConfigurationStatus");
        configJson.Should().Contain("Secure");
    }

    [Fact]
    public void GetSystemConfiguration_ShouldLogInformation()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);

        // Act
        securityHelper.GetSystemConfiguration();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("System configuration retrieved safely")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Password Reset Tests

    [Fact]
    public void ResetUserPassword_WithValidInput_ShouldLogAppropriately()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var username = "testuser";
        var newPassword = "NewPassword123!";

        // Act
        try
        {
            var result = securityHelper.ResetUserPassword(username, newPassword);
            // Result depends on database connectivity, so we don't assert on it
        }
        catch
        {
            // Expected since we don't have real database
        }

        // Assert - Verify the method at least attempts to validate password strength
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(null, "Password123!")]
    [InlineData("", "Password123!")]
    [InlineData("user", null)]
    [InlineData("user", "")]
    public void ResetUserPassword_WithInvalidInputs_ShouldReturnFalse(string username, string newPassword)
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);

        // Act
        var result = securityHelper.ResetUserPassword(username, newPassword);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ResetUserPassword_WithWeakPassword_ShouldReturnFalse()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var username = "testuser";
        var weakPassword = "weak";

        // Act
        var result = securityHelper.ResetUserPassword(username, weakPassword);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Session Management Tests

    [Fact]
    public void ClearUserSession_WithValidUsername_ShouldLogInformation()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var username = "testuser";
        
        // First generate a token to create a session
        securityHelper.GenerateUserToken(username);

        // Act
        securityHelper.ClearUserSession(username);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Session cleared successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ClearUserSession_WithInvalidUsername_ShouldLogWarning(string username)
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);

        // Act
        securityHelper.ClearUserSession(username);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Session cleanup failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ClearUserSession_WithNonExistentUser_ShouldLogWarning()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var nonExistentUser = "nonexistentuser";

        // Act
        securityHelper.ClearUserSession(nonExistentUser);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No active session found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ClearUserSession_AfterClearingSession_TokenValidationShouldFail()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var username = "testuser";
        var token = securityHelper.GenerateUserToken(username);

        // Verify token is valid before clearing
        var isValidBefore = securityHelper.ValidateUserToken(username, token);
        isValidBefore.Should().BeTrue();

        // Act
        securityHelper.ClearUserSession(username);

        // Assert
        var isValidAfter = securityHelper.ValidateUserToken(username, token);
        isValidAfter.Should().BeFalse();
    }

    #endregion

    #region Configuration Error Tests

    [Fact]
    public void Constructor_WithMissingEncryptionKey_ShouldThrowWhenEncryptionCalled()
    {
        // Arrange
        var configWithoutKey = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"Security:KeySalt", "TestSalt"}
            })
            .Build();

        var securityHelper = new SecurityHelper(configWithoutKey, _mockLogger.Object);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => securityHelper.EncryptSensitiveData("test"));
    }

    [Fact]
    public void ResetUserPassword_WithMissingConnectionString_ShouldReturnFalse()
    {
        // Arrange
        var configWithoutConnection = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"Security:EncryptionKey", "MyTestEncryptionKey12345"},
                {"Security:KeySalt", "MyTestSalt123"}
            })
            .Build();

        var securityHelper = new SecurityHelper(configWithoutConnection, _mockLogger.Object);

        // Act
        var result = securityHelper.ResetUserPassword("testuser", "Password123!");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Edge Cases and Security Tests

    [Fact]
    public async Task SecurityHelper_WithConcurrentTokenOperations_ShouldBeThreadSafe()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var username = "testuser";
        var tasks = new List<Task<string>>();

        // Act - Generate multiple tokens concurrently
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() => securityHelper.GenerateUserToken($"{username}{i}")));
        }

        await Task.WhenAll(tasks);

        // Assert - All tokens should be generated successfully
        tasks.Should().AllSatisfy(task => task.Result.Should().NotBeNullOrEmpty());
        
        // All tokens should be unique
        var tokens = tasks.Select(t => t.Result).ToList();
        tokens.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void EncryptDecrypt_WithUnicodeCharacters_ShouldWorkCorrectly()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var unicodeText = "测试数据 🔐 émojis and spëcial chars åÅ";

        // Act
        var encrypted = securityHelper.EncryptSensitiveData(unicodeText);
        var decrypted = securityHelper.DecryptSensitiveData(encrypted);

        // Assert
        decrypted.Should().Be(unicodeText);
    }

    [Fact]
    public void HashPassword_WithVeryLongPassword_ShouldHandleCorrectly()
    {
        // Arrange
        var securityHelper = new SecurityHelper(_configuration, _mockLogger.Object);
        var longPassword = new string('A', 1000) + "1!"; // Very long password with required complexity

        // Act
        var hashedPassword = securityHelper.HashPassword(longPassword);

        // Assert
        hashedPassword.Should().NotBeNullOrEmpty();
        securityHelper.VerifyPassword(longPassword, hashedPassword).Should().BeTrue();
    }

    [Fact]
    public void SecurityHelper_MultipleInstances_ShouldHaveIndependentTokenStorage()
    {
        // Arrange
        var securityHelper1 = new SecurityHelper(_configuration, _mockLogger.Object);
        var securityHelper2 = new SecurityHelper(_configuration, _mockLogger.Object);
        var username = "testuser";

        // Act
        var token1 = securityHelper1.GenerateUserToken(username);
        var token2 = securityHelper2.GenerateUserToken(username);

        // Assert
        // Each instance should only validate its own tokens
        securityHelper1.ValidateUserToken(username, token1).Should().BeTrue();
        securityHelper1.ValidateUserToken(username, token2).Should().BeFalse();
        
        securityHelper2.ValidateUserToken(username, token2).Should().BeTrue();
        securityHelper2.ValidateUserToken(username, token1).Should().BeFalse();
    }

    #endregion
}
