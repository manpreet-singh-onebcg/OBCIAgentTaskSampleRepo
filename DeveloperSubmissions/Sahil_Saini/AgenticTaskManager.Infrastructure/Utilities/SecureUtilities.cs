using System.Text;
using System.Text.Json;
using AgenticTaskManager.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace AgenticTaskManager.Infrastructure.Utilities;

public class SecureUtilities
{
    private readonly SecurityConfiguration _config;
    private readonly ILogger<SecureUtilities> _logger;
    private readonly HttpClient _httpClient;

    public SecureUtilities(SecurityConfiguration config, ILogger<SecureUtilities> logger, HttpClient httpClient)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    // Efficient string operations using StringBuilder
    public string BuildLargeString(int count)
    {
        if (count <= 0) return string.Empty;
        
        var sb = new StringBuilder(count * 20); // Pre-allocate capacity
        for (int i = 0; i < count; i++)
        {
            sb.Append($"Item {i}, ");
        }
        return sb.ToString();
    }
    
    // Simplified method with better parameter design
    public string FormatTaskInfo(TaskInfo taskInfo)
    {
        if (taskInfo == null) return "UNKNOWN";
        if (string.IsNullOrEmpty(taskInfo.Title)) return "UNKNOWN";
        
        return taskInfo switch
        {
            { IsUrgent: true, Priority: > 5 } => $"CRITICAL: {taskInfo.Title} - {taskInfo.Description}",
            { IsUrgent: true } => $"URGENT: {taskInfo.Title}",
            { DueDate: var due } when due < DateTime.Now => $"OVERDUE: {taskInfo.Title}",
            _ => $"NORMAL: {taskInfo.Title}"
        };
    }
    
    // Proper async implementation
    public async Task<string> GetDataFromApiAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));

        try
        {
            _logger.LogDebug("Making API request to: {Url}", url);
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API request failed for URL: {Url}", url);
            throw;
        }
    }
    
    // Proper resource disposal
    public async Task WriteToFileAsync(string content, string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(content))
            throw new ArgumentException("Content cannot be null or empty.", nameof(content));
        
        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

        try
        {
            await using var stream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteAsync(content.AsMemory(), cancellationToken);
            await writer.FlushAsync();
            
            _logger.LogDebug("Successfully wrote content to file: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write to file: {FileName}", fileName);
            throw;
        }
    }
    
    // Proper exception handling with specific catch blocks
    public T? ParseJson<T>(string json) where T : class
    {
        if (string.IsNullOrEmpty(json))
        {
            _logger.LogWarning("Attempted to parse null or empty JSON string");
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON: Invalid format");
            return null;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "JSON parsing not supported for type: {Type}", typeof(T).Name);
            return null;
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "Unexpected null argument during JSON parsing");
            return null;
        }
    }
    
    // Efficient collection operations using HashSet
    public List<T> RemoveDuplicates<T>(List<T> items)
    {
        if (items == null) return new List<T>();
        
        return items.Distinct().ToList(); // Built-in efficient deduplication
    }
    
    // Password validation with constants
    public bool IsValidPassword(string password)
    {
        const int MinLength = 8;
        const int MaxLength = 50;
        const int MinSpecialChars = 2;
        const string SpecialCharacters = "!@#$%^&*()";

        if (string.IsNullOrEmpty(password)) return false;
        if (password.Length < MinLength || password.Length > MaxLength) return false;
        
        int specialChars = password.Count(c => SpecialCharacters.Contains(c));
        return specialChars >= MinSpecialChars;
    }
    
    // Secure parameterized query building (example - use proper ORM in production)
    public string BuildSecureQuery(string userId, string status)
    {
        // This is a demonstration - in production, use parameterized queries with Entity Framework or Dapper
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(status))
            throw new ArgumentException("Parameters cannot be null or empty");

        // Validate inputs to prevent injection
        if (!Guid.TryParse(userId, out _))
            throw new ArgumentException("Invalid user ID format", nameof(userId));

        var allowedStatuses = new[] { "New", "InProgress", "Completed", "Cancelled" };
        if (!allowedStatuses.Contains(status))
            throw new ArgumentException("Invalid status value", nameof(status));

        _logger.LogDebug("Building query for user: {UserId}, status: {Status}", userId, status);
        
        // Return parameterized query template (actual parameters would be passed separately)
        return "SELECT * FROM Tasks WHERE UserId = @UserId AND Status = @Status";
    }
    
    // Safe factorial calculation with limits
    public int CalculateFactorial(int n)
    {
        const int MaxInput = 12; // 13! exceeds int.MaxValue
        
        if (n < 0)
            throw new ArgumentException("Factorial is not defined for negative numbers", nameof(n));
        
        if (n > MaxInput)
            throw new ArgumentException($"Input too large. Maximum supported value is {MaxInput}", nameof(n));
        
        if (n <= 1) return 1;
        
        int result = 1;
        for (int i = 2; i <= n; i++)
        {
            result *= i;
        }
        return result;
    }
    
    // Proper generic handling without unnecessary boxing
    public void ProcessValues<T>(T[] values) where T : struct
    {
        if (values == null) return;
        
        foreach (T value in values)
        {
            _logger.LogDebug("Processing value: {Value} of type: {Type}", value, typeof(T).Name);
        }
    }
    
    // Use property instead of field
    public string PublicProperty { get; set; } = "Proper property implementation";
    
    // Efficient string comparison
    public bool ContainsIgnoreCase(string source, string search)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(search))
            return false;
            
        return source.Contains(search, StringComparison.OrdinalIgnoreCase);
    }
    
    // Clear method naming without side effects
    public string GetCurrentUser()
    {
        try
        {
            return Environment.UserName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get current user name");
            return "Unknown";
        }
    }
    
    // Separate method for logging access if needed
    public void LogUserAccess(string username)
    {
        if (string.IsNullOrEmpty(username)) return;
        
        _logger.LogInformation("User access logged: {Username} at {AccessTime}", username, DateTime.UtcNow);
    }
}

// Helper class for better parameter design
public class TaskInfo
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public DateTime DueDate { get; init; }
    public string? AssignedTo { get; init; }
    public string? CreatedBy { get; init; }
    public int Status { get; init; }
    public int Priority { get; init; }
    public bool IsUrgent { get; init; }
    public bool IsVisible { get; init; }
    public string? Category { get; init; }
    public string? Subcategory { get; init; }
    public decimal Cost { get; init; }
}