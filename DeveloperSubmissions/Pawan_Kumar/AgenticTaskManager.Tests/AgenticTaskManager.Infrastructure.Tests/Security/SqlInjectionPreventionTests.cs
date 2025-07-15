using AgenticTaskManager.Infrastructure.Security;
using AgenticTaskManager.Infrastructure.Data;
using AgenticTaskManager.Domain.Entities;
using AgenticTaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Data;
using System.Data.Common;
using Xunit;

namespace AgenticTaskManager.Infrastructure.Tests.Security
{
    public class SqlInjectionPreventionTests : IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly AppDbContext _context;
        private readonly Mock<ILogger<LegacyDataAccess>> _mockLogger;

        public SqlInjectionPreventionTests()
        {
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(_options);
            _mockLogger = new Mock<ILogger<LegacyDataAccess>>();
        }

        [Theory]
        [InlineData("'; DROP TABLE Tasks; --")]
        [InlineData("1' OR '1'='1")]
        [InlineData("1'; SELECT * FROM Tasks; --")]
        [InlineData("1' UNION SELECT * FROM Tasks --")]
        [InlineData("'; INSERT INTO Tasks VALUES ('malicious'); --")]
        public async Task SearchTasks_WithSqlInjectionAttempts_ShouldUseparameterizedQueries(string maliciousInput)
        {
            // Arrange
            var legacyDataAccess = new LegacyDataAccess(_context, _mockLogger.Object);

            // Act & Assert
            // The method should use parameterized queries and not be vulnerable to SQL injection
            var exception = await Record.ExceptionAsync(() => legacyDataAccess.SearchTasksSecureAsync(maliciousInput));
            
            // Should not throw SQL syntax errors or return unexpected results
            Assert.Null(exception);
        }

        [Theory]
        [InlineData("<script>alert('XSS')</script>")]
        [InlineData("<img src=x onerror=alert('XSS')>")]
        [InlineData("javascript:alert('XSS')")]
        [InlineData("onclick=\"alert('XSS')\"")]
        public void SanitizeInput_WithXssAttempts_ShouldRemoveHarmfulContent(string xssInput)
        {
            // Act
            var sanitized = SecurityHelper.SanitizeInput(xssInput);

            // Assert
            Assert.NotNull(sanitized);
            Assert.DoesNotContain("<script>", sanitized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("javascript:", sanitized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onclick", sanitized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onerror", sanitized, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetTasksByUser_WithParameterizedQuery_ShouldPreventSqlInjection()
        {
            // Arrange
            var legacyDataAccess = new LegacyDataAccess(_context, _mockLogger.Object);
            var maliciousUserId = "'; DROP TABLE Tasks; --";

            // Act
            var result = await legacyDataAccess.GetTasksByUserSecureAsync(maliciousUserId);

            // Assert
            // Should return empty result, not execute malicious SQL
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Theory]
        [InlineData("user@domain.com' OR '1'='1")]
        [InlineData("admin'; DROP DATABASE; --")]
        [InlineData("test' UNION SELECT password FROM users --")]
        public async Task ValidateUser_WithSqlInjectionInEmail_ShouldUseSafeQueries(string maliciousEmail)
        {
            // Arrange
            var legacyDataAccess = new LegacyDataAccess(_context, _mockLogger.Object);

            // Act
            var isValid = await legacyDataAccess.ValidateUserSecureAsync(maliciousEmail, "password");

            // Assert
            // Should return false without executing malicious SQL
            Assert.False(isValid);
        }

        [Fact]
        public void EncryptSensitiveData_ShouldNotStorePlaintext()
        {
            // Arrange
            var sensitiveData = "Secret Information";

            // Act
            var encrypted = SecurityHelper.EncryptSensitiveData(sensitiveData);

            // Assert
            Assert.NotEqual(sensitiveData, encrypted);
            Assert.NotEmpty(encrypted);
        }

        [Fact]
        public void DecryptSensitiveData_ShouldRestoreOriginalData()
        {
            // Arrange
            var originalData = "Secret Information";
            var encrypted = SecurityHelper.EncryptSensitiveData(originalData);

            // Act
            var decrypted = SecurityHelper.DecryptSensitiveData(encrypted);

            // Assert
            Assert.Equal(originalData, decrypted);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void EncryptSensitiveData_WithInvalidInput_ShouldThrowArgumentException(string invalidInput)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => SecurityHelper.EncryptSensitiveData(invalidInput));
        }

        [Fact]
        public void ValidateInput_WithPathTraversalAttempt_ShouldReject()
        {
            // Arrange
            var pathTraversalAttempts = new[]
            {
                "../../../etc/passwd",
                "..\\..\\..\\windows\\system32\\config\\sam",
                "%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd",
                "....//....//....//etc//passwd"
            };

            foreach (var attempt in pathTraversalAttempts)
            {
                // Act
                var isValid = SecurityHelper.IsValidFilePath(attempt);

                // Assert
                Assert.False(isValid, $"Path traversal attempt should be rejected: {attempt}");
            }
        }

        [Fact]
        public void ValidateInput_WithValidPaths_ShouldAccept()
        {
            // Arrange
            var validPaths = new[]
            {
                "document.txt",
                "folder/document.txt",
                "reports/2023/summary.pdf"
            };

            foreach (var path in validPaths)
            {
                // Act
                var isValid = SecurityHelper.IsValidFilePath(path);

                // Assert
                Assert.True(isValid, $"Valid path should be accepted: {path}");
            }
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }

    public class PerformanceSecurityTests
    {
        [Fact]
        public async Task PasswordHashing_ShouldBeResistantToTimingAttacks()
        {
            // Arrange
            var password = "testPassword123";
            var wrongPassword = "wrongPassword456";
            var hashedPassword = SecurityHelper.HashPassword(password);

            var timings = new List<long>();

            // Act - Measure timing for correct password
            for (int i = 0; i < 100; i++)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                SecurityHelper.VerifyPassword(password, hashedPassword);
                stopwatch.Stop();
                timings.Add(stopwatch.ElapsedTicks);
            }

            var correctPasswordAverage = timings.Average();
            timings.Clear();

            // Act - Measure timing for wrong password
            for (int i = 0; i < 100; i++)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                SecurityHelper.VerifyPassword(wrongPassword, hashedPassword);
                stopwatch.Stop();
                timings.Add(stopwatch.ElapsedTicks);
            }

            var wrongPasswordAverage = timings.Average();

            // Assert - Timing difference should be minimal (within 20% variance)
            var timingDifference = Math.Abs(correctPasswordAverage - wrongPasswordAverage);
            var variance = timingDifference / Math.Max(correctPasswordAverage, wrongPasswordAverage);
            
            Assert.True(variance < 0.2, $"Timing variance {variance:P} is too high, may be vulnerable to timing attacks");
        }

        [Fact]
        public void PasswordHashing_ShouldHaveAppropriateComplexity()
        {
            // Arrange
            var password = "testPassword123";
            var iterations = 1000;

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                SecurityHelper.HashPassword(password);
            }
            stopwatch.Stop();

            var averageTimePerHash = stopwatch.ElapsedMilliseconds / (double)iterations;

            // Assert - Should take reasonable time (not too fast, not too slow)
            Assert.True(averageTimePerHash > 0.1, "Password hashing is too fast, may be vulnerable to brute force");
            Assert.True(averageTimePerHash < 100, "Password hashing is too slow, may impact performance");
        }

        [Fact]
        public async Task ConcurrentAccess_ShouldMaintainDataIntegrity()
        {
            // Arrange
            var tasks = new List<Task>();
            var secureCounter = 0;
            var lockObject = new object();

            // Act - Simulate concurrent access
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var token = SecurityHelper.GenerateSecureToken();
                    lock (lockObject)
                    {
                        secureCounter++;
                    }
                    Assert.NotEmpty(token);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(100, secureCounter);
        }
    }
}
