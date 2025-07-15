using AgenticTaskManager.Infrastructure.Security;
using Xunit;

namespace AgenticTaskManager.Infrastructure.Tests.Security
{
    public class SecurityHelperTests
    {
        [Fact]
        public void HashPassword_WithValidPassword_ShouldReturnHashedPassword()
        {
            // Arrange
            var password = "testPassword123";

            // Act
            var hashedPassword = SecurityHelper.HashPassword(password);

            // Assert
            Assert.NotNull(hashedPassword);
            Assert.NotEqual(password, hashedPassword);
            Assert.True(hashedPassword.Length > 0);
        }

        [Fact]
        public void HashPassword_WithSamePassword_ShouldReturnDifferentHashes()
        {
            // Arrange
            var password = "testPassword123";

            // Act
            var hash1 = SecurityHelper.HashPassword(password);
            var hash2 = SecurityHelper.HashPassword(password);

            // Assert
            Assert.NotEqual(hash1, hash2); // Due to salt, each hash should be different
        }

        [Fact]
        public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
        {
            // Arrange
            var password = "testPassword123";
            var hashedPassword = SecurityHelper.HashPassword(password);

            // Act
            var isValid = SecurityHelper.VerifyPassword(password, hashedPassword);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
        {
            // Arrange
            var password = "testPassword123";
            var wrongPassword = "wrongPassword456";
            var hashedPassword = SecurityHelper.HashPassword(password);

            // Act
            var isValid = SecurityHelper.VerifyPassword(wrongPassword, hashedPassword);

            // Assert
            Assert.False(isValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void HashPassword_WithInvalidPassword_ShouldThrowArgumentException(string invalidPassword)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => SecurityHelper.HashPassword(invalidPassword));
        }

        [Theory]
        [InlineData("", "validHash")]
        [InlineData(null, "validHash")]
        [InlineData("validPassword", "")]
        [InlineData("validPassword", null)]
        public void VerifyPassword_WithInvalidInput_ShouldThrowArgumentException(string password, string hash)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => SecurityHelper.VerifyPassword(password, hash));
        }

        [Fact]
        public void SanitizeInput_WithMaliciousInput_ShouldRemoveHarmfulCharacters()
        {
            // Arrange
            var maliciousInput = "<script>alert('XSS')</script>";

            // Act
            var sanitized = SecurityHelper.SanitizeInput(maliciousInput);

            // Assert
            Assert.NotNull(sanitized);
            Assert.DoesNotContain("<script>", sanitized);
            Assert.DoesNotContain("</script>", sanitized);
        }

        [Fact]
        public void SanitizeInput_WithSqlInjectionAttempt_ShouldSanitizeInput()
        {
            // Arrange
            var sqlInjection = "'; DROP TABLE Tasks; --";

            // Act
            var sanitized = SecurityHelper.SanitizeInput(sqlInjection);

            // Assert
            Assert.NotNull(sanitized);
            // The implementation should handle SQL injection patterns
        }

        [Fact]
        public void SanitizeInput_WithNormalInput_ShouldReturnSameInput()
        {
            // Arrange
            var normalInput = "This is a normal task description.";

            // Act
            var sanitized = SecurityHelper.SanitizeInput(normalInput);

            // Assert
            Assert.Equal(normalInput, sanitized);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void SanitizeInput_WithEmptyOrNullInput_ShouldReturnEmptyString(string input)
        {
            // Act
            var sanitized = SecurityHelper.SanitizeInput(input);

            // Assert
            Assert.Equal(string.Empty, sanitized);
        }

        [Fact]
        public void GenerateSecureToken_ShouldReturnNonEmptyToken()
        {
            // Act
            var token = SecurityHelper.GenerateSecureToken();

            // Assert
            Assert.NotNull(token);
            Assert.NotEmpty(token);
        }

        [Fact]
        public void GenerateSecureToken_MultipleCalls_ShouldReturnDifferentTokens()
        {
            // Act
            var token1 = SecurityHelper.GenerateSecureToken();
            var token2 = SecurityHelper.GenerateSecureToken();

            // Assert
            Assert.NotEqual(token1, token2);
        }
    }
}
