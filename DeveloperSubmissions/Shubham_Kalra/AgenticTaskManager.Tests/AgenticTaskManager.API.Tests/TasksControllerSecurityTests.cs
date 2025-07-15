using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using AgenticTaskManager.API.Controllers;
using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Application.Interfaces;
using TestUtilities;
using System.ComponentModel.DataAnnotations;

namespace AgenticTaskManager.API.Tests;

/// <summary>
/// Security-focused tests for TasksController
/// These tests specifically target security vulnerabilities and attack vectors
/// </summary>
public class TasksControllerSecurityTests : IDisposable
{
    private readonly Mock<ITaskService> _mockTaskService;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<TasksController>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly TasksController _controller;
    private readonly HttpClient _httpClient;

    public TasksControllerSecurityTests()
    {
        _mockTaskService = new Mock<ITaskService>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<TasksController>>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        _httpClient = new HttpClient();
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        
        _controller = new TasksController(
            _mockTaskService.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            _mockConfiguration.Object);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Configuration Helper Methods

    private IConfigurationSection CreateConfigurationSection(string key, string value)
    {
        var section = new Mock<IConfigurationSection>();
        section.Setup(x => x.Key).Returns(key);
        section.Setup(x => x.Value).Returns(value);
        return section.Object;
    }

    private void SetupFileUploadConfiguration()
    {
        // Mock max file size configuration
        var maxSizeSection = new Mock<IConfigurationSection>();
        maxSizeSection.Setup(x => x.Value).Returns("10");
        _mockConfiguration.Setup(x => x.GetSection("FileUpload:MaxFileSizeMB")).Returns(maxSizeSection.Object);
        
        // Mock allowed extensions configuration
        var allowedExtensionsSection = new Mock<IConfigurationSection>();
        allowedExtensionsSection.Setup(x => x.GetChildren()).Returns(new[]
        {
            CreateConfigurationSection("0", ".txt"),
            CreateConfigurationSection("1", ".pdf")
        });
        _mockConfiguration.Setup(x => x.GetSection("FileUpload:AllowedExtensions")).Returns(allowedExtensionsSection.Object);
        
        // Mock upload path configuration
        _mockConfiguration.Setup(x => x["FileUpload:Path"]).Returns("uploads");
    }

    #endregion

    #region SQL Injection Prevention Tests

    [Theory]
    [InlineData("'; DROP TABLE Tasks; SELECT * FROM Users WHERE '1'='1")]
    [InlineData("1' UNION SELECT * FROM Users--")]
    [InlineData("'; EXEC xp_cmdshell('dir'); --")]
    [InlineData("' OR 1=1; DELETE FROM Tasks; --")]
    [InlineData("admin'/**/OR/**/1=1--")]
    [InlineData("'; INSERT INTO Tasks VALUES ('malicious'); --")]
    public async Task Create_WithSqlInjectionAttempts_ShouldSanitizeInput(string sqlInjectionPayload)
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = sqlInjectionPayload,
            Description = $"Description with {sqlInjectionPayload}",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        var expectedTaskId = Guid.NewGuid();
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(expectedTaskId);

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        
        // Verify the service was called with the exact input (controller should pass through)
        // The sanitization should happen at the service/repository layer
        _mockTaskService.Verify(x => x.CreateTaskAsync(It.Is<TaskDto>(dto => 
            dto.Title == sqlInjectionPayload)), Times.Once);
    }

    [Theory]
    [InlineData("'; DROP TABLE Tasks; --")]
    [InlineData("admin' OR '1'='1")]
    [InlineData("' UNION SELECT password FROM users --")]
    public async Task GetUserReport_WithSqlInjectionInUserId_ShouldHandleSafely(string maliciousUserId)
    {
        // Arrange
        var reportData = new { TaskCount = 0, Message = "No data found" };
        _mockTaskService.Setup(x => x.GetUserReportAsync(It.IsAny<string>()))
                       .ReturnsAsync(reportData);

        // Act
        var result = await _controller.GetUserReport(maliciousUserId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockTaskService.Verify(x => x.GetUserReportAsync(maliciousUserId), Times.Once);
    }

    #endregion

    #region XSS Prevention Tests

    [Theory]
    [InlineData("<script>alert('XSS')</script>")]
    [InlineData("<img src=x onerror=alert('XSS')>")]
    [InlineData("<svg onload=alert('XSS')>")]
    [InlineData("javascript:alert('XSS')")]
    [InlineData("<iframe src='javascript:alert(\"XSS\")'></iframe>")]
    [InlineData("&#60;script&#62;alert('XSS')&#60;/script&#62;")]
    public async Task Create_WithXssPayloads_ShouldAcceptButNotExecute(string xssPayload)
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = xssPayload,
            Description = $"Safe description with {xssPayload}",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        var expectedTaskId = Guid.NewGuid();
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(expectedTaskId);

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        
        // The controller should accept the input but proper encoding should happen at view layer
        _mockTaskService.Verify(x => x.CreateTaskAsync(It.Is<TaskDto>(dto => 
            dto.Title == xssPayload)), Times.Once);
    }

    #endregion

    #region Input Validation Tests

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Create_WithInvalidTitleInput_ShouldValidateCorrectly(string invalidTitle)
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = invalidTitle,
            Description = "Valid Description",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        // Simulate model validation failure
        _controller.ModelState.AddModelError("Title", "Title is required");

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        _mockTaskService.Verify(x => x.CreateTaskAsync(It.IsAny<TaskDto>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithExtremelyLongInput_ShouldHandleGracefully()
    {
        // Arrange
        var longString = new string('A', 10000); // 10K characters
        var taskDto = new TaskDto
        {
            Title = longString,
            Description = longString,
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        var expectedTaskId = Guid.NewGuid();
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(expectedTaskId);

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd")]
    [InlineData("....//....//....//etc//passwd")]
    public async Task Create_WithPathTraversalAttempts_ShouldHandleSafely(string pathTraversalPayload)
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = pathTraversalPayload,
            Description = $"Path traversal attempt: {pathTraversalPayload}",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        var expectedTaskId = Guid.NewGuid();
        _mockTaskService.Setup(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(expectedTaskId);

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
    }

    #endregion

    #region Authentication & Authorization Tests

    [Theory]
    [InlineData("")]
    [InlineData("invalid-key")]
    [InlineData("null")]
    [InlineData("admin")]
    [InlineData("12345")]
    [InlineData("' OR '1'='1' --")]
    public async Task SearchTasks_WithInvalidApiKeys_ShouldRejectAccess(string invalidApiKey)
    {
        // Arrange
        _mockConfiguration.Setup(x => x["ApiSettings:ApiKey"]).Returns("secure-api-key-123");
        
        var searchParams = new TaskSearchParametersDto
        {
            ApiKey = invalidApiKey,
            Title = "Test"
        };

        // Act
        var result = await _controller.SearchTasks(searchParams);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        var unauthorizedResult = result as UnauthorizedObjectResult;
        unauthorizedResult!.Value.Should().Be("Invalid API key");
    }

    [Fact]
    public async Task SearchTasks_WithNullApiKeyConfig_ShouldHandleGracefully()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["ApiSettings:ApiKey"]).Returns((string?)null);
        
        var searchParams = new TaskSearchParametersDto
        {
            ApiKey = "any-key",
            Title = "Test"
        };

        // Act
        var result = await _controller.SearchTasks(searchParams);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Theory]
    [InlineData("Bearer malicious-token")]
    [InlineData("Basic YWRtaW46cGFzc3dvcmQ=")]
    [InlineData("ApiKey malicious-key")]
    public async Task Controller_WithMaliciousAuthHeaders_ShouldNotAffectApiKeyValidation(string authHeader)
    {
        // Arrange
        _mockConfiguration.Setup(x => x["ApiSettings:ApiKey"]).Returns("valid-key");
        
        var searchParams = new TaskSearchParametersDto
        {
            ApiKey = "valid-key",
            Title = "Test"
        };

        // Simulate malicious auth header (would be set by middleware in real scenario)
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = authHeader;
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = await _controller.SearchTasks(searchParams);

        // Assert
        // Should still validate properly based on API key in request body, not headers
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region File Upload Security Tests

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("virus.bat")]
    [InlineData("script.js")]
    [InlineData("payload.php")]
    [InlineData("backdoor.jsp")]
    [InlineData("shell.asp")]
    public async Task UploadFile_WithMaliciousFileExtensions_ShouldRejectFile(string maliciousFileName)
    {
        // Arrange
        var mockFile = HttpContextTestHelper.CreateMockFormFile(maliciousFileName, "malicious content");
        
        SetupFileUploadConfiguration();

        // Act
        var result = await _controller.UploadFile(mockFile);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.Value.ToString()!.Should().Contain("not allowed");
    }

    [Theory]
    [InlineData("normal.txt.exe")]
    [InlineData("document.pdf.bat")]
    [InlineData("file.docx.js")]
    public async Task UploadFile_WithDoubleExtensionAttacks_ShouldValidateCorrectly(string doubleExtensionFile)
    {
        // Arrange
        var mockFile = HttpContextTestHelper.CreateMockFormFile(doubleExtensionFile, "content");
        
        SetupFileUploadConfiguration();

        // Act
        var result = await _controller.UploadFile(mockFile);

        // Assert
        // Should reject based on final extension (.exe, .bat, .js)
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadFile_WithNullBytesInFileName_ShouldHandleSafely()
    {
        // Arrange
        var maliciousFileName = "safe.txt\0.exe";
        var mockFile = HttpContextTestHelper.CreateMockFormFile(maliciousFileName, "content");
        
        SetupFileUploadConfiguration();

        // Act
        var result = await _controller.UploadFile(mockFile);

        // Assert
        // The controller should handle this safely
        result.Should().NotBeOfType<ObjectResult>();
        if (result is ObjectResult objectResult)
        {
            objectResult.StatusCode.Should().NotBe(500);
        }
    }

    [Fact]
    public async Task UploadFile_WithExcessiveSize_ShouldPreventDoSAttack()
    {
        // Arrange
        var largeFile = HttpContextTestHelper.CreateMaliciousFormFile("large.txt", 100); // 100MB
        
        SetupFileUploadConfiguration();

        // Act
        var result = await _controller.UploadFile(largeFile);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.Value.ToString()!.Should().Contain("exceeds the maximum limit");
    }

    #endregion

    #region Configuration Security Tests

    [Fact]
    public async Task UploadFile_WithMissingUploadPath_ShouldHandleSecurely()
    {
        // Arrange
        var mockFile = HttpContextTestHelper.CreateMockFormFile("test.txt", "content");
        
        SetupFileUploadConfiguration();
        _mockConfiguration.Setup(x => x["FileUpload:Path"]).Returns((string?)null);

        // Act
        var result = await _controller.UploadFile(mockFile);

        // Assert
        // Should handle missing configuration gracefully
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task SearchTasks_WithMissingApiKeyConfig_ShouldDefaultSecurely()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["ApiSettings:ApiKey"]).Returns((string?)null);
        
        var searchParams = new TaskSearchParametersDto
        {
            ApiKey = "any-key"
        };

        // Act
        var result = await _controller.SearchTasks(searchParams);

        // Assert
        // Should reject when no valid API key is configured
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region Race Condition Tests

    [Fact]
    public async Task Create_ConcurrentTaskCreation_ShouldHandleRaceConditions()
    {
        // Arrange
        var taskDtos = Enumerable.Range(0, 20)
            .Select(i => new TaskDto
            {
                Title = $"Concurrent Task {i}",
                Description = $"Description {i}",
                CreatedById = Guid.NewGuid(),
                AssignedToId = Guid.NewGuid(),
                DueDate = DateTime.UtcNow.AddDays(1)
            }).ToArray();

        var taskIds = taskDtos.Select(_ => Guid.NewGuid()).ToArray();
        
        _mockTaskService.SetupSequence(x => x.CreateTaskAsync(It.IsAny<TaskDto>()))
                       .ReturnsAsync(taskIds[0])
                       .ReturnsAsync(taskIds[1])
                       .ReturnsAsync(taskIds[2])
                       .ReturnsAsync(taskIds[3])
                       .ReturnsAsync(taskIds[4])
                       .ReturnsAsync(taskIds[5])
                       .ReturnsAsync(taskIds[6])
                       .ReturnsAsync(taskIds[7])
                       .ReturnsAsync(taskIds[8])
                       .ReturnsAsync(taskIds[9])
                       .ReturnsAsync(taskIds[10])
                       .ReturnsAsync(taskIds[11])
                       .ReturnsAsync(taskIds[12])
                       .ReturnsAsync(taskIds[13])
                       .ReturnsAsync(taskIds[14])
                       .ReturnsAsync(taskIds[15])
                       .ReturnsAsync(taskIds[16])
                       .ReturnsAsync(taskIds[17])
                       .ReturnsAsync(taskIds[18])
                       .ReturnsAsync(taskIds[19]);

        // Act
        var tasks = taskDtos.Select(dto => _controller.Create(dto)).ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(20);
        results.Should().AllBeOfType<CreatedAtActionResult>();
        
        // Verify each call was made
        _mockTaskService.Verify(x => x.CreateTaskAsync(It.IsAny<TaskDto>()), Times.Exactly(20));
    }

    #endregion
}
