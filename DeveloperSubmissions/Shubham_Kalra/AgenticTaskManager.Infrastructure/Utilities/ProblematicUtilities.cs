using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;

namespace AgenticTaskManager.Infrastructure.Utilities;

// Fixed: Refactored to instance-based class with dependency injection and proper separation of concerns
public class ProblematicUtilities
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProblematicUtilities>? _logger;
    
    // Fixed: Thread-safe collection instead of static mutable list
    private readonly ConcurrentBag<string> _cache = new();
    
    // Fixed: Configuration-based password validation settings
    private readonly PasswordValidationSettings _passwordSettings;

    public ProblematicUtilities(IConfiguration configuration, ILogger<ProblematicUtilities>? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
        
        // Fixed: Load password settings from configuration
        _passwordSettings = new PasswordValidationSettings
        {
            MinLength = int.TryParse(_configuration["PasswordValidation:MinLength"], out var minLen) ? minLen : 8,
            MaxLength = int.TryParse(_configuration["PasswordValidation:MaxLength"], out var maxLen) ? maxLen : 50,
            RequiredSpecialChars = int.TryParse(_configuration["PasswordValidation:RequiredSpecialChars"], out var reqChars) ? reqChars : 2,
            SpecialCharacterSet = _configuration["PasswordValidation:SpecialCharacterSet"] ?? "!@#$%^&*()"
        };
    }

    // Fixed: Secure credential access from configuration
    private string GetApiKey()
    {
        var apiKey = _configuration["Security:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("API key not found in configuration");
        }
        return apiKey;
    }

    private string GetDatabasePassword()
    {
        var dbPassword = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(dbPassword))
        {
            throw new InvalidOperationException("Database connection string not found in configuration");
        }
        return dbPassword;
    }
    
    // Fixed: Efficient string building using StringBuilder
    public string BuildLargeString(int count)
    {
        if (count <= 0) return string.Empty;
        
        var result = new StringBuilder(count * 10); // Pre-allocate capacity
        for (int i = 0; i < count; i++)
        {
            result.Append($"Item {i}, ");
        }
        
        // Remove trailing comma and space
        if (result.Length > 2)
        {
            result.Length -= 2;
        }
        
        return result.ToString();
    }
    
    // Fixed: Reduced parameters by using data transfer object
    public string FormatTaskInfo(TaskInfoRequest request)
    {
        if (request == null) return "UNKNOWN";
        
        // Fixed: Simplified logic using strategy pattern approach
        return request switch
        {
            { IsUrgent: true, Priority: > 5 } => FormatCriticalTask(request),
            { IsUrgent: true } => FormatUrgentTask(request),
            _ when request.DueDate < DateTime.Now => FormatOverdueTask(request),
            _ => FormatNormalTask(request)
        };
    }

    // Fixed: Helper methods to reduce cognitive complexity
    private static string FormatCriticalTask(TaskInfoRequest request) =>
        $"CRITICAL: {request.Title} - {request.Description}";

    private static string FormatUrgentTask(TaskInfoRequest request) =>
        $"URGENT: {request.Title}";

    private static string FormatOverdueTask(TaskInfoRequest request) =>
        $"OVERDUE: {request.Title}";

    private static string FormatNormalTask(TaskInfoRequest request) =>
        $"NORMAL: {request.Title}";
    
    // Fixed: Proper async implementation instead of blocking
    public async Task<string> GetDataFromApiAsync(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentException("URL cannot be null or empty", nameof(url));
        }

        using var client = new HttpClient();
        try
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Error fetching data from API: {Url}", url);
            throw;
        }
    }
    
    // Fixed: Proper resource disposal using using statement
    public void WriteToFile(string content, string fileName)
    {
        if (string.IsNullOrEmpty(content))
        {
            throw new ArgumentException("Content cannot be null or empty", nameof(content));
        }
        
        if (string.IsNullOrEmpty(fileName))
        {
            throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
        }

        try
        {
            using var stream = new FileStream(fileName, FileMode.Create);
            using var writer = new StreamWriter(stream);
            writer.Write(content);
            // Fixed: Resources automatically disposed by using statements
        }
        catch (IOException ex)
        {
            _logger?.LogError(ex, "Error writing to file: {FileName}", fileName);
            throw;
        }
    }
    
    // Fixed: Proper exception handling with logging and meaningful defaults
    public T? ParseJson<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            _logger?.LogWarning("JSON parsing failed: Input is null or empty");
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            // Fixed: Proper exception handling with logging
            _logger?.LogError(ex, "JSON deserialization failed for type {Type}", typeof(T).Name);
            return default;
        }
        catch (ArgumentException ex)
        {
            _logger?.LogError(ex, "Invalid JSON format for type {Type}", typeof(T).Name);
            return default;
        }
        catch (Exception ex)
        {
            // Fixed: Generic exception with proper logging (no sensitive data)
            _logger?.LogError(ex, "Unexpected error during JSON parsing for type {Type}", typeof(T).Name);
            return default;
        }
    }
    
    // Fixed: Efficient duplicate removal using HashSet for O(n) performance
    public static List<T> RemoveDuplicates<T>(List<T> items)
    {
        if (items == null) return new List<T>();

        var seen = new HashSet<T>();
        var result = new List<T>(items.Count);
        
        foreach (var item in items)
        {
            if (seen.Add(item)) // Add returns false if item already exists
            {
                result.Add(item);
            }
        }
        
        return result;
    }
    
    // Fixed: Configuration-based password validation without magic numbers
    public bool IsValidPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        if (password.Length < _passwordSettings.MinLength || 
            password.Length > _passwordSettings.MaxLength)
            return false;

        int specialChars = password.Count(c => _passwordSettings.SpecialCharacterSet.Contains(c));
        return specialChars >= _passwordSettings.RequiredSpecialChars;
    }
    
    // Fixed: SQL injection prevention using parameterized queries
    public string BuildSecureQuery(string userId, string status)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(status))
        {
            throw new ArgumentException("UserId and Status cannot be null or empty");
        }

        // Fixed: Return parameterized query template for safe execution
        // This should be used with SqlCommand.Parameters.AddWithValue()
        return "SELECT * FROM Tasks WHERE UserId = @userId AND Status = @status";
    }

    // Fixed: Example of how to execute the secure query
    public async Task<List<dynamic>> ExecuteSecureQueryAsync(string userId, string status)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Database connection string not found");
        }

        var results = new List<dynamic>();
        var query = BuildSecureQuery(userId, status);

        try
        {
            using var connection = new SqlConnection(connectionString);
            using var command = new SqlCommand(query, connection);
            
            // Fixed: Use parameterized queries to prevent SQL injection
            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@status", status);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                // Process results here - implementation depends on actual table structure
                results.Add(new { Id = reader["Id"], Title = reader["Title"] });
            }
        }
        catch (SqlException ex)
        {
            _logger?.LogError(ex, "Database error executing query for user {UserId}", userId);
            throw;
        }

        return results;
    }
    
    // Fixed: Stack overflow prevention with iterative approach
    public static long CalculateFactorial(int n)
    {
        if (n < 0)
        {
            throw new ArgumentException("Factorial is not defined for negative numbers", nameof(n));
        }

        if (n > 20) // Prevent overflow for long type
        {
            throw new ArgumentException("Input too large - would cause overflow", nameof(n));
        }

        if (n <= 1) return 1;

        // Fixed: Iterative approach to prevent stack overflow
        long result = 1;
        for (int i = 2; i <= n; i++)
        {
            result *= i;
        }
        
        return result;
    }
    
    // Fixed: Generic approach with pattern matching to avoid boxing/unboxing
    public void ProcessValues<T>(T[] values) where T : struct
    {
        foreach (var value in values)
        {
            _logger?.LogInformation("Processing value: {Value} of type {Type}", value, typeof(T).Name);
        }
    }

    // Fixed: Overload for mixed types when needed
    public void ProcessMixedValues(object[] values)
    {
        foreach (var value in values)
        {
            var message = value switch
            {
                int intValue => $"Integer: {intValue}",
                double doubleValue => $"Double: {doubleValue}",
                string stringValue => $"String: {stringValue}",
                _ => $"Unknown type: {value?.GetType().Name ?? "null"}"
            };
            
            _logger?.LogInformation("{Message}", message);
        }
    }
    
    // Fixed: Removed dead code - method was unused

    // Fixed: Property instead of public field with proper encapsulation
    public string PublicField { get; set; } = "Should be a property";

    // Fixed: Efficient case-insensitive string comparison
    public static bool ContainsIgnoreCase(string source, string search)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(search))
            return false;

        // Fixed: Use culture-invariant comparison without creating new strings
        return source.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    // Fixed: Method name reflects side effects and proper thread-safe access tracking
    public string GetCurrentUserWithAudit()
    {
        var currentUser = Environment.UserName;
        var auditEntry = $"User access: {currentUser} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
        
        // Fixed: Thread-safe collection for audit trail
        _cache.Add(auditEntry);
        
        _logger?.LogInformation("User access recorded: {User}", currentUser);
        return currentUser;
    }
}

// Fixed: Data transfer object to reduce parameter count
public class TaskInfoRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public int Status { get; set; }
    public int Priority { get; set; }
    public bool IsUrgent { get; set; }
    public bool IsVisible { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Subcategory { get; set; } = string.Empty;
    public decimal Cost { get; set; }
}

// Fixed: Configuration class for password validation settings
public class PasswordValidationSettings
{
    public int MinLength { get; set; } = 8;
    public int MaxLength { get; set; } = 50;
    public int RequiredSpecialChars { get; set; } = 2;
    public string SpecialCharacterSet { get; set; } = "!@#$%^&*()";
}
