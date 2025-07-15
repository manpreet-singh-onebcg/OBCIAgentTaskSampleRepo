using System.Text;
using System.Text.Json;

namespace AgenticTaskManager.Infrastructure.Utilities;

// SQ: Class with too many responsibilities
public static class ProblematicUtilities
{
    // SQ: Hardcoded credentials
    private static readonly string API_KEY = "sk-1234567890abcdef"; 
    private static readonly string DATABASE_PASSWORD = "MySecretPassword123!";
    
    // SQ: Static mutable collection - not thread safe
    public static List<string> GlobalCache = new List<string>();
    
    // Performance: Inefficient string operations
    public static string BuildLargeString(int count)
    {
        string result = "";
        for (int i = 0; i < count; i++)
        {
            result += $"Item {i}, "; // SQ: String concatenation in loop
        }
        return result;
    }
    
    // SQ: Method with too many parameters
    public static string FormatTaskInfo(string title, string description, DateTime dueDate, 
        string assignedTo, string createdBy, int status, int priority, bool isUrgent, 
        bool isVisible, string category, string subcategory, decimal cost)
    {
        // SQ: High cognitive complexity with nested conditions
        if (title != null)
        {
            if (description != null)
            {
                if (assignedTo != null)
                {
                    if (isUrgent)
                    {
                        if (priority > 5)
                        {
                            return $"CRITICAL: {title} - {description}";
                        }
                        else
                        {
                            return $"URGENT: {title}";
                        }
                    }
                    else
                    {
                        if (dueDate < DateTime.Now)
                        {
                            return $"OVERDUE: {title}";
                        }
                        else
                        {
                            return $"NORMAL: {title}";
                        }
                    }
                }
            }
        }
        return "UNKNOWN";
    }
    
    // Performance: Blocking async operations
    public static string GetDataFromApi(string url)
    {
        using var client = new HttpClient();
        // SQ: Blocking async call
        var response = client.GetAsync(url).Result; 
        return response.Content.ReadAsStringAsync().Result;
    }
    
    // SQ: Resource leak - missing disposal
    public static void WriteToFile(string content, string fileName)
    {
        var stream = new FileStream(fileName, FileMode.Create);
        var writer = new StreamWriter(stream);
        writer.Write(content);
        // Missing: Dispose calls or using statements
    }
    
    // SQ: Empty catch block
    public static T ParseJson<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            // SQ: Empty catch block - exception swallowed
        }
        catch (Exception ex)
        {
            // SQ: Generic exception catch
            Console.WriteLine(ex.Message); // SQ: Sensitive data in logs
        }
        return default(T);
    }
    
    // Performance: Inefficient collection operations
    public static List<T> RemoveDuplicates<T>(List<T> items)
    {
        var result = new List<T>();
        foreach (var item in items)
        {
            // Performance: Contains is O(n) operation in loop
            if (!result.Contains(item))
            {
                result.Add(item);
            }
        }
        return result;
    }
    
    // SQ: Magic numbers and hardcoded values
    public static bool IsValidPassword(string password)
    {
        if (password.Length < 8) return false; // Magic number
        if (password.Length > 50) return false; // Magic number
        
        int specialChars = 0;
        foreach (char c in password)
        {
            if ("!@#$%^&*()".Contains(c)) // Hardcoded string
            {
                specialChars++;
            }
        }
        return specialChars >= 2; // Magic number
    }
    
    // SQ: SQL Injection vulnerability (simulated)
    public static string BuildQuery(string userId, string status)
    {
        // SQ: String concatenation for SQL query
        return $"SELECT * FROM Tasks WHERE UserId = '{userId}' AND Status = '{status}'";
    }
    
    // SQ: Infinite recursion potential
    public static int CalculateFactorial(int n)
    {
        if (n <= 0) return 1; // SQ: Should check for negative numbers
        return n * CalculateFactorial(n - 1); // No base case for large numbers
    }
    
    // Performance: Unnecessary boxing/unboxing
    public static void ProcessValues(object[] values)
    {
        foreach (object value in values)
        {
            if (value is int)
            {
                int intValue = (int)value; // Unnecessary boxing/unboxing
                Console.WriteLine($"Integer: {intValue}");
            }
        }
    }
    
    // SQ: Dead code - unused method
    private static void UnusedPrivateMethod()
    {
        var deadCode = "This method is never called";
    }
    
    // SQ: Public field instead of property
    public static string PublicField = "Should be a property";
    
    // Performance: Inefficient string comparison
    public static bool ContainsIgnoreCase(string source, string search)
    {
        // Performance: Creates new string objects
        return source.ToLower().Contains(search.ToLower());
    }
    
    // SQ: Method with side effects not indicated by name
    public static string GetCurrentUser()
    {
        // SQ: Method name suggests it only gets data, but it also modifies global state
        GlobalCache.Add($"Access at {DateTime.Now}");
        return Environment.UserName;
    }
}
