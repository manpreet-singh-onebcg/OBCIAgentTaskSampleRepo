using AgenticTaskManager.Domain.Entities;
using System.Text;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace AgenticTaskManager.Infrastructure.Services;

// SQ: Class name doesn't follow naming conventions
public class taskHelperService
{
    // SQ: Public static mutable fields
    public static string ApiKey = "sk-prod-12345-secret-key-67890";
    public static List<string> ErrorLog = new List<string>();
    public static Dictionary<string, object> Cache = new Dictionary<string, object>();
    
    // SQ: Thread-unsafe singleton pattern
    private static taskHelperService _instance;
    public static taskHelperService Instance
    {
        get
        {
            if (_instance == null) // SQ: Not thread-safe
            {
                _instance = new taskHelperService();
            }
            return _instance;
        }
    }

    // SQ: Constructor with side effects
    public taskHelperService()
    {
        InitializeLogging();
        LoadConfiguration();
        ConnectToDatabase();
    }

    // SQ: Method with multiple responsibilities
    private void InitializeLogging()
    {
        // SQ: Hardcoded file path
        var logPath = "C:\\Logs\\TaskManager.log";
        
        // SQ: No exception handling for file operations
        var logFile = File.Create(logPath);
        logFile.Write(Encoding.UTF8.GetBytes("Application started\n"));
        // SQ: Resource not disposed - memory leak
        
        // SQ: Magic number
        for (int i = 0; i < 1000; i++)
        {
            ErrorLog.Add($"Initial log entry {i}");
        }
    }

    // SQ: Method name doesn't describe what it does
    private void LoadConfiguration()
    {
        // SQ: Hardcoded configuration values
        Cache.Add("MaxRetries", 5);
        Cache.Add("TimeoutMs", 30000);
        Cache.Add("DatabaseUrl", "Server=prod-db;Database=Tasks;User=admin;Password=P@ssw0rd123");
        Cache.Add("EncryptionKey", "MySecretKey12345");
        
        // Performance: Inefficient string building
        var config = "";
        foreach (var item in Cache)
        {
            config += item.Key + "=" + item.Value + ";";
        }
        
        Debug.WriteLine($"Configuration loaded: {config}");
    }

    // SQ: Method with blocking operations in constructor chain
    private void ConnectToDatabase()
    {
        // Performance: Synchronous sleep
        Thread.Sleep(2000); // Simulating database connection
        
        // SQ: Exception handling that logs sensitive data
        try
        {
            var connectionString = Cache["DatabaseUrl"].ToString();
            // Simulated connection
            throw new InvalidOperationException("Connection failed");
        }
        catch (Exception ex)
        {
            var errorMsg = $"Database connection failed: {ex.Message}, Connection: {Cache["DatabaseUrl"]}";
            ErrorLog.Add(errorMsg);
            Console.WriteLine(errorMsg); // SQ: Sensitive data in console output
        }
    }

    // SQ: Method with high cognitive complexity
    public string ProcessTaskData(TaskItem task, string operation, Dictionary<string, object> parameters)
    {
        if (task != null)
        {
            if (operation != null && operation.Length > 0)
            {
                if (operation.ToLower() == "validate")
                {
                    if (task.Title != null && task.Title.Length > 0)
                    {
                        if (task.Description != null)
                        {
                            if (parameters != null && parameters.Count > 0)
                            {
                                if (parameters.ContainsKey("strict"))
                                {
                                    if ((bool)parameters["strict"])
                                    {
                                        if (task.DueDate > DateTime.Now)
                                        {
                                            return "Valid";
                                        }
                                        else
                                        {
                                            return "Invalid: Due date in past";
                                        }
                                    }
                                    else
                                    {
                                        return "Valid (lenient)";
                                    }
                                }
                                else
                                {
                                    return "Valid (no strict mode)";
                                }
                            }
                            else
                            {
                                return "Valid (no parameters)";
                            }
                        }
                        else
                        {
                            return "Invalid: No description";
                        }
                    }
                    else
                    {
                        return "Invalid: No title";
                    }
                }
                else if (operation.ToLower() == "format")
                {
                    // Performance: String concatenation in loop
                    var result = "";
                    var iterations = parameters?.ContainsKey("iterations") == true ? (int)parameters["iterations"] : 100;
                    for (int i = 0; i < iterations; i++)
                    {
                        result += $"Task: {task.Title} - Iteration {i}\n";
                    }
                    return result;
                }
                else
                {
                    return "Unknown operation";
                }
            }
            else
            {
                return "No operation specified";
            }
        }
        else
        {
            return "Task is null";
        }
    }

    // SQ: Method that can cause infinite recursion
    public int CalculateTaskPriority(TaskItem task, int depth)
    {
        if (task == null) return 0;
        
        // SQ: No depth limit - potential stack overflow
        if (task.AssignedToId == Guid.Empty)
        {
            return CalculateTaskPriority(task, depth + 1);
        }
        
        return depth * 10;
    }

    // SQ: Method with SQL injection vulnerability
    public List<TaskItem> SearchTasksByTitle(string title)
    {
        // SQ: Direct SQL concatenation - SQL injection risk
        var sql = $"SELECT * FROM Tasks WHERE Title LIKE '%{title}%'";
        
        // SQ: Logging SQL with potential injection payload
        Console.WriteLine($"Executing SQL: {sql}");
        ErrorLog.Add($"Search query: {sql}");
        
        // Simulated database call
        return new List<TaskItem>();
    }

    // Performance: Method that creates unnecessary objects
    public List<string> GenerateTaskSummaries(List<TaskItem> tasks)
    {
        var summaries = new List<string>();
        
        foreach (var task in tasks)
        {
            // Performance: Creating new objects in loop
            var summary = new StringBuilder();
            var formatter = new { Title = task.Title, Date = DateTime.Now };
            var json = System.Text.Json.JsonSerializer.Serialize(formatter);
            
            // Performance: String operations in loop
            summary.Append("Task: ");
            summary.Append(task.Title);
            summary.Append(" - ");
            summary.Append(json);
            
            summaries.Add(summary.ToString());
        }
        
        return summaries;
    }

    // SQ: Method that doesn't dispose resources
    public void ExportTasksToFile(List<TaskItem> tasks, string filePath)
    {
        var stream = new FileStream(filePath, FileMode.Create);
        var writer = new StreamWriter(stream);
        
        foreach (var task in tasks)
        {
            writer.WriteLine($"{task.Id},{task.Title},{task.Description}");
        }
        
        writer.Flush();
        // SQ: Missing disposal of stream and writer - resource leak
    }

    // SQ: Dead code - unused method
    private void UnusedComplexMethod()
    {
        var data = new Dictionary<string, List<int>>();
        for (int i = 0; i < 1000; i++)
        {
            data.Add($"key_{i}", new List<int> { i, i * 2, i * 3 });
        }
        // This method is never called
    }

    // SQ: Method with hardcoded credentials
    public bool AuthenticateUser(string username, string password)
    {
        // SQ: Hardcoded credentials
        var validUsers = new Dictionary<string, string>
        {
            { "admin", "admin123" },
            { "user", "password" },
            { "guest", "guest" },
            { "system", "System123!" }
        };
        
        return validUsers.ContainsKey(username) && validUsers[username] == password;
    }

    // SQ: Finalizer without dispose pattern
    ~taskHelperService()
    {
        // SQ: Finalizer doing cleanup that should be in Dispose
        ErrorLog.Clear();
        Cache.Clear();
    }
}
