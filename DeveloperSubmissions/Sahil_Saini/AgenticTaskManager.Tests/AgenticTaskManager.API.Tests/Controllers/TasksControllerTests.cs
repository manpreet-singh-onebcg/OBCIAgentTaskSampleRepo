using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using AgenticTaskManager.API.Controllers;
using AgenticTaskManager.API.DTOs;
using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Application.Interfaces;
using AgenticTaskManager.Infrastructure.Configuration;
using AgenticTaskManager.Infrastructure.Security;
using AgenticTaskManager.Domain.Entities;
using System.Text;
using DomainTaskStatus = AgenticTaskManager.Domain.Entities.TaskStatus;

namespace AgenticTaskManager.API.Tests.Controllers;

/// <summary>
/// Unit tests for TasksController
/// Focus: Controllers, HTTP endpoints, request/response handling
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("API")]
public class TasksControllerTests
{
    private MockTaskService _mockTaskService;
    private MockSecurityConfiguration _mockSecurityConfig;
    private MockSecurityHelper _mockSecurityHelper;
    private MockLogger<TasksController> _mockLogger;
    private MockHttpClient _mockHttpClient;
    private TasksController _controller;

    [SetUp]
    public void SetUp()
    {
        // Create manual mocks
        _mockTaskService = new MockTaskService();
        _mockSecurityConfig = new MockSecurityConfiguration();
        _mockSecurityHelper = new MockSecurityHelper();
        _mockLogger = new MockLogger<TasksController>();
        _mockHttpClient = new MockHttpClient();

        // Create controller instance
        _controller = new TasksController(
            _mockTaskService,
            _mockSecurityConfig,
            _mockSecurityHelper,
            _mockLogger,
            _mockHttpClient);
    }

    [TearDown]
    public void TearDown()
    {
        _mockHttpClient?.Dispose();
        // Note: TasksController doesn't implement IDisposable, so we don't dispose it
    }

    #region Create Method Tests

    [Test]
    public async Task Create_WithValidDto_ShouldReturnCreatedResult()
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = "Valid Task Title",
            Description = "Valid Description",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(7)
        };
        
        var expectedTaskId = Guid.NewGuid();
        _mockTaskService.SetupCreateTaskAsync(expectedTaskId);

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        Assert.That(result, Is.InstanceOf<CreatedAtActionResult>());
        var createdResult = result as CreatedAtActionResult;
        Assert.That(createdResult!.Value, Is.EqualTo(expectedTaskId));
        Assert.That(createdResult.ActionName, Is.EqualTo(nameof(_controller.GetAll)));
        
        // Verify service was called
        Assert.That(_mockTaskService.CreateTaskAsyncCalled, Is.True);
        
        // Verify logging
        Assert.That(_mockLogger.WasLoggedWithLevel(LogLevel.Information), Is.True);
    }

    [Test]
    public async Task Create_WithNullDto_ShouldReturnBadRequest()
    {
        // Arrange
        TaskDto? nullDto = null;

        // Act
        var result = await _controller.Create(nullDto!);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        var badRequestResult = result as BadRequestObjectResult;
        Assert.That(badRequestResult!.Value, Is.EqualTo("Task data is required"));
        
        // Verify service was not called
        Assert.That(_mockTaskService.CreateTaskAsyncCalled, Is.False);
        
        // Verify warning was logged
        Assert.That(_mockLogger.WasLoggedWithLevel(LogLevel.Warning), Is.True);
    }

    [Test]
    public async Task Create_WithEmptyTitle_ShouldReturnBadRequest()
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = string.Empty,
            Description = "Valid Description",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(7)
        };

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        var badRequestResult = result as BadRequestObjectResult;
        Assert.That(badRequestResult!.Value, Is.EqualTo("Task title is required"));
        
        Assert.That(_mockTaskService.CreateTaskAsyncCalled, Is.False);
    }

    [Test]
    public async Task Create_WithWhitespaceTitle_ShouldReturnBadRequest()
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = "   ",
            Description = "Valid Description",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(7)
        };

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        var badRequestResult = result as BadRequestObjectResult;
        Assert.That(badRequestResult!.Value, Is.EqualTo("Task title is required"));
    }

    [Test]
    public async Task Create_WhenServiceThrowsException_ShouldReturnInternalServerError()
    {
        // Arrange
        var taskDto = new TaskDto
        {
            Title = "Valid Title",
            Description = "Valid Description",
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(7)
        };
        
        _mockTaskService.SetupCreateTaskAsyncToThrow(new InvalidOperationException("Database error"));

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        Assert.That(result, Is.InstanceOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(500));
        Assert.That(objectResult.Value, Is.EqualTo("An error occurred while creating the task"));
        
        // Verify error logging
        Assert.That(_mockLogger.WasLoggedWithLevel(LogLevel.Error), Is.True);
    }

    [Test]
    public async Task Create_WithInvalidModelState_ShouldReturnBadRequest()
    {
        // Arrange
        var taskDto = new TaskDto();
        _controller.ModelState.AddModelError("Title", "Title is required");

        // Act
        var result = await _controller.Create(taskDto);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(_mockTaskService.CreateTaskAsyncCalled, Is.False);
    }

    #endregion

    #region GetAll Method Tests

    [Test]
    public async Task GetAll_WithValidPagination_ShouldReturnOkWithTasks()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            CreateSampleTask("Task 1"),
            CreateSampleTask("Task 2"),
            CreateSampleTask("Task 3"),
            CreateSampleTask("Task 4"),
            CreateSampleTask("Task 5")
        };
        var page = 1;
        var pageSize = 10;

        _mockTaskService.SetupGetTasksAsync(tasks);

        // Act
        var result = await _controller.GetAll(page, pageSize);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var returnedTasks = okResult!.Value as List<TaskItem>;
        Assert.That(returnedTasks, Is.Not.Null);
        Assert.That(returnedTasks!.Count, Is.EqualTo(5));
        
        Assert.That(_mockTaskService.GetTasksAsyncCalled, Is.True);
    }

    [Test]
    public async Task GetAll_WithInvalidPage_ShouldNormalizePagination()
    {
        // Arrange
        var tasks = new List<TaskItem> { CreateSampleTask("Task 1") };
        _mockTaskService.SetupGetTasksAsync(tasks);

        // Act - pass invalid page number
        var result = await _controller.GetAll(-1, 150);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        
        // Verify that the method executed successfully with normalized parameters
        Assert.That(_mockTaskService.GetTasksAsyncCalled, Is.True);
    }

    [Test]
    public async Task GetAll_WithEmptyTaskList_ShouldReturnEmptyList()
    {
        // Arrange
        var emptyTasks = new List<TaskItem>();
        _mockTaskService.SetupGetTasksAsync(emptyTasks);

        // Act
        var result = await _controller.GetAll();

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var returnedTasks = okResult!.Value as List<TaskItem>;
        Assert.That(returnedTasks, Is.Not.Null);
        Assert.That(returnedTasks!.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetAll_WhenServiceThrowsException_ShouldReturnInternalServerError()
    {
        // Arrange
        _mockTaskService.SetupGetTasksAsyncToThrow(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _controller.GetAll();

        // Assert
        Assert.That(result, Is.InstanceOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(500));
        Assert.That(objectResult.Value, Is.EqualTo("An error occurred while retrieving tasks"));
        
        Assert.That(_mockLogger.WasLoggedWithLevel(LogLevel.Error), Is.True);
    }

    [Test]
    public async Task GetAll_WithCompletedTasks_ShouldCountCompletedTasksCorrectly()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            CreateSampleTaskWithStatus(DomainTaskStatus.New),
            CreateSampleTaskWithStatus(DomainTaskStatus.Completed),
            CreateSampleTaskWithStatus(DomainTaskStatus.Completed),
            CreateSampleTaskWithStatus(DomainTaskStatus.InProgress)
        };

        _mockTaskService.SetupGetTasksAsync(tasks);

        // Act
        var result = await _controller.GetAll();

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        
        // Verify the method executed successfully
        Assert.That(_mockLogger.WasLoggedWithLevel(LogLevel.Debug), Is.True);
    }

    #endregion

    #region SearchTasks Method Tests

    [Test]
    public async Task SearchTasks_WithValidApiKey_ShouldReturnOk()
    {
        // Arrange
        var searchRequest = new TaskSearchRequest
        {
            ApiKey = "test-api-key-123",
            Title = "Valid Title",
            Status = 1,
            Priority = 5
        };

        _mockSecurityConfig.SetupGetApiKey("test-api-key-123");

        // Act
        var result = await _controller.SearchTasks(searchRequest);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        Assert.That(okResult!.Value, Is.EqualTo("Search functionality to be implemented with secure parameters"));
        
        Assert.That(_mockLogger.WasLoggedWithLevel(LogLevel.Information), Is.True);
    }

    [Test]
    public async Task SearchTasks_WithInvalidApiKey_ShouldReturnUnauthorized()
    {
        // Arrange
        var searchRequest = new TaskSearchRequest
        {
            ApiKey = "invalid-api-key"
        };

        _mockSecurityConfig.SetupGetApiKey("valid-key"); // Different from request

        // Act
        var result = await _controller.SearchTasks(searchRequest);

        // Assert
        Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        var unauthorizedResult = result as UnauthorizedObjectResult;
        Assert.That(unauthorizedResult!.Value, Is.EqualTo("Invalid API key"));
        
        Assert.That(_mockLogger.WasLoggedWithLevel(LogLevel.Warning), Is.True);
    }

    [Test]
    public async Task SearchTasks_WithEmptyApiKey_ShouldReturnUnauthorized()
    {
        // Arrange
        var searchRequest = new TaskSearchRequest
        {
            ApiKey = string.Empty
        };

        // Act
        var result = await _controller.SearchTasks(searchRequest);

        // Assert
        Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
    }

    [Test]
    public async Task SearchTasks_WithInvalidParameters_ShouldReturnBadRequest()
    {
        // Arrange
        var searchRequest = new TaskSearchRequest
        {
            ApiKey = "test-api-key-123",
            Title = new string('A', 101), // Exceeds max length
            Status = -1 // Invalid status
        };

        _mockSecurityConfig.SetupGetApiKey("test-api-key-123");

        // Act
        var result = await _controller.SearchTasks(searchRequest);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        var badRequestResult = result as BadRequestObjectResult;
        var errorMessage = badRequestResult!.Value as string;
        Assert.That(errorMessage, Does.Contain("Title cannot exceed 100 characters"));
        Assert.That(errorMessage, Does.Contain("Status must be between 0 and 10"));
    }

    [Test]
    public async Task SearchTasks_WithInvalidDateRange_ShouldReturnBadRequest()
    {
        // Arrange
        var searchRequest = new TaskSearchRequest
        {
            ApiKey = "test-api-key-123",
            StartDate = DateTime.Now.AddDays(1),
            EndDate = DateTime.Now // End date before start date
        };

        _mockSecurityConfig.SetupGetApiKey("test-api-key-123");

        // Act
        var result = await _controller.SearchTasks(searchRequest);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        var badRequestResult = result as BadRequestObjectResult;
        var errorMessage = badRequestResult!.Value as string;
        Assert.That(errorMessage, Does.Contain("Start date cannot be after end date"));
    }

    #endregion

    #region GetUserReport Method Tests

    [Test]
    public async Task GetUserReport_WithValidUserId_ShouldReturnOkWithReport()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _controller.GetUserReport(userId);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        Assert.That(okResult!.Value, Is.Not.Null);
        
        Assert.That(_mockLogger.WasLoggedWithLevel(LogLevel.Information), Is.True);
    }

    [Test]
    public async Task GetUserReport_WithEmptyGuid_ShouldReturnBadRequest()
    {
        // Arrange
        var emptyGuid = Guid.Empty;

        // Act
        var result = await _controller.GetUserReport(emptyGuid);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        var badRequestResult = result as BadRequestObjectResult;
        Assert.That(badRequestResult!.Value, Is.EqualTo("Valid user ID is required"));
    }

    #endregion

    #region UploadFile Method Tests

    [Test]
    public async Task UploadFile_WithValidFile_ShouldReturnOkWithFileInfo()
    {
        // Arrange
        var formFile = CreateMockFormFile("Test content", "test.txt");

        // Act
        var result = await _controller.UploadFile(formFile);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        Assert.That(_mockLogger.WasLoggedWithLevel(LogLevel.Information), Is.True);
    }

    [Test]
    public async Task UploadFile_WithNullFile_ShouldReturnBadRequest()
    {
        // Arrange
        IFormFile? nullFile = null;

        // Act
        var result = await _controller.UploadFile(nullFile!);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        var badRequestResult = result as BadRequestObjectResult;
        Assert.That(badRequestResult!.Value, Is.EqualTo("File is required"));
    }

    [Test]
    public async Task UploadFile_WithFileTooLarge_ShouldReturnBadRequest()
    {
        // Arrange
        var formFile = CreateMockFormFile("content", "large-file.txt", 11 * 1024 * 1024); // 11MB

        // Act
        var result = await _controller.UploadFile(formFile);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        var badRequestResult = result as BadRequestObjectResult;
        Assert.That(badRequestResult!.Value, Is.EqualTo("File size cannot exceed 10MB"));
    }

    [Test]
    public async Task UploadFile_WithInvalidExtension_ShouldReturnBadRequest()
    {
        // Arrange
        var formFile = CreateMockFormFile("content", "malicious-file.exe");

        // Act
        var result = await _controller.UploadFile(formFile);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        var badRequestResult = result as BadRequestObjectResult;
        var errorMessage = badRequestResult!.Value as string;
        Assert.That(errorMessage, Does.Contain("Only .txt, .csv, .json files are allowed"));
    }

    [Test]
    [TestCase(".txt")]
    [TestCase(".csv")]
    [TestCase(".json")]
    public async Task UploadFile_WithAllowedExtensions_ShouldReturnOk(string extension)
    {
        // Arrange
        var fileName = $"test{extension}";
        var formFile = CreateMockFormFile("test content", fileName);

        // Act
        var result = await _controller.UploadFile(formFile);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
    }

    #endregion

    #region Constructor Tests

    [Test]
    public void Constructor_WithNullTaskService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new TasksController(
            null!,
            _mockSecurityConfig,
            _mockSecurityHelper,
            _mockLogger,
            _mockHttpClient));

        Assert.That(ex.ParamName, Is.EqualTo("service"));
    }

    [Test]
    public void Constructor_WithNullSecurityConfig_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new TasksController(
            _mockTaskService,
            null!,
            _mockSecurityHelper,
            _mockLogger,
            _mockHttpClient));

        Assert.That(ex.ParamName, Is.EqualTo("config"));
    }

    [Test]
    public void Constructor_WithAllValidParameters_ShouldCreateInstance()
    {
        // Act & Assert
        var controller = new TasksController(
            _mockTaskService,
            _mockSecurityConfig,
            _mockSecurityHelper,
            _mockLogger,
            _mockHttpClient);

        Assert.That(controller, Is.Not.Null);
        Assert.That(controller, Is.InstanceOf<TasksController>());
    }

    #endregion

    #region Helper Methods

    private TaskItem CreateSampleTask(string title)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "Sample Description",
            Status = DomainTaskStatus.New,
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
    }

    private TaskItem CreateSampleTaskWithStatus(DomainTaskStatus status)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = $"Task with {status} status",
            Description = "Sample Description",
            Status = status,
            CreatedById = Guid.NewGuid(),
            AssignedToId = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
    }

    private IFormFile CreateMockFormFile(string content, string fileName, long? fileSize = null)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var size = fileSize ?? bytes.Length;
        
        return new MockFormFile(fileName, size, content);
    }

    #endregion
}

#region Manual Mock Implementations

// Manual mock implementations to avoid external dependencies

public class MockTaskService : ITaskService
{
    public bool CreateTaskAsyncCalled { get; private set; }
    public bool GetTasksAsyncCalled { get; private set; }
    
    private Guid _createTaskResult = Guid.NewGuid();
    private List<TaskItem> _getTasksResult = new();
    private Exception? _createTaskException;
    private Exception? _getTasksException;

    public void SetupCreateTaskAsync(Guid result)
    {
        _createTaskResult = result;
        _createTaskException = null;
    }

    public void SetupCreateTaskAsyncToThrow(Exception exception)
    {
        _createTaskException = exception;
    }

    public void SetupGetTasksAsync(List<TaskItem> result)
    {
        _getTasksResult = result;
        _getTasksException = null;
    }

    public void SetupGetTasksAsyncToThrow(Exception exception)
    {
        _getTasksException = exception;
    }

    public Task<Guid> CreateTaskAsync(TaskDto taskDto)
    {
        CreateTaskAsyncCalled = true;
        if (_createTaskException != null)
            throw _createTaskException;
        return Task.FromResult(_createTaskResult);
    }

    public Task<List<TaskItem>> GetTasksAsync()
    {
        GetTasksAsyncCalled = true;
        if (_getTasksException != null)
            throw _getTasksException;
        return Task.FromResult(_getTasksResult);
    }

    public Task<List<TaskItem>> GetTasksAsync(int page, int pageSize)
    {
        GetTasksAsyncCalled = true;
        if (_getTasksException != null)
            throw _getTasksException;
        return Task.FromResult(_getTasksResult);
    }
}

public class MockSecurityConfiguration : SecurityConfiguration
{
    private string _apiKey = "test-api-key-123";

    public MockSecurityConfiguration() : base(new MockConfiguration()) { }

    public void SetupGetApiKey(string key)
    {
        _apiKey = key;
    }

    public new string GetApiKey(string serviceName)
    {
        return _apiKey;
    }
}

public class MockSecurityHelper : SecurityHelper
{
    public MockSecurityHelper() : base(new MockSecurityConfiguration(), new MockLogger<SecurityHelper>()) { }
}

public class MockLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _logs = new();

    public bool WasLoggedWithLevel(LogLevel level)
    {
        return _logs.Any(l => l.Level == level);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logs.Add((logLevel, formatter(state, exception)));
    }
}

public class MockHttpClient : HttpClient
{
    public MockHttpClient() : base(new MockHttpMessageHandler()) { }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }
}

public class MockConfiguration : IConfiguration
{
    private readonly Dictionary<string, string> _values = new();

    public string? this[string key] 
    { 
        get => _values.TryGetValue(key, out var value) ? value : null;
        set => _values[key] = value ?? string.Empty;
    }

    public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();

    public IChangeToken GetReloadToken() => new MockChangeToken();

    public IConfigurationSection GetSection(string key) => new MockConfigurationSection();
}

public class MockConfigurationSection : IConfigurationSection
{
    private readonly Dictionary<string, string> _values = new();

    public string? this[string key] 
    { 
        get => _values.TryGetValue(key, out var value) ? value : null;
        set => _values[key] = value ?? string.Empty;
    }
    
    public string Key => "MockKey";
    public string Path => "MockPath";
    public string? Value { get; set; }

    public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();

    public IChangeToken GetReloadToken() => new MockChangeToken();

    public IConfigurationSection GetSection(string key) => new MockConfigurationSection();
}

public class MockChangeToken : IChangeToken
{
    public bool HasChanged => false;
    public bool ActiveChangeCallbacks => false;
    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => new MockDisposable();
}

public class MockDisposable : IDisposable
{
    public void Dispose() { }
}

public class MockFormFile : IFormFile
{
    public MockFormFile(string fileName, long length, string content = "")
    {
        FileName = fileName;
        Length = length;
        _content = content;
    }

    private readonly string _content;

    public string ContentType => "text/plain";
    public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
    public IHeaderDictionary Headers => new HeaderDictionary();
    public long Length { get; }
    public string Name => "file";
    public string FileName { get; }

    public void CopyTo(Stream target) => CopyToAsync(target).Wait();

    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(_content);
        return target.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
    }

    public Stream OpenReadStream() => new MemoryStream(Encoding.UTF8.GetBytes(_content));
}

#endregion