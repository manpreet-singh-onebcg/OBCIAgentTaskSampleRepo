using AgenticTaskManager.Domain.Entities;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text;

namespace AgenticTaskManager.Infrastructure.Data;

// SQ: Class with multiple data access anti-patterns
public class LegacyDataAccess
{
    // SQ: Hardcoded connection string with credentials
    private static readonly string ConnectionString = "Server=localhost;Database=TaskManagerDB;User Id=sa;Password=P@ssw0rd123;TrustServerCertificate=true;";
    
    // SQ: Static connection - not thread-safe and causes connection leaks
    private static SqlConnection _sharedConnection;
    
    // SQ: Global transaction - poor design
    private static SqlTransaction _globalTransaction;

    // SQ: Static constructor with potential exceptions
    static LegacyDataAccess()
    {
        try
        {
            _sharedConnection = new SqlConnection(ConnectionString);
            _sharedConnection.Open(); // SQ: Opening connection in static constructor
        }
        catch (Exception ex)
        {
            // SQ: Exception in static constructor - application will fail to start
            Console.WriteLine($"Failed to initialize database connection: {ex.Message}");
            throw; // SQ: Re-throwing exception from static constructor
        }
    }

    // SQ: Method with SQL injection vulnerability
    public static List<TaskItem> GetTasksByUser(string userId)
    {
        var tasks = new List<TaskItem>();
        
        // SQ: Direct string concatenation - SQL injection risk
        var sql = "SELECT * FROM Tasks WHERE CreatedById = '" + userId + "' OR AssignedToId = '" + userId + "'";
        
        // SQ: Logging SQL query with potentially sensitive data
        Console.WriteLine($"Executing query: {sql}");
        
        // SQ: Using shared connection without proper synchronization
        var command = new SqlCommand(sql, _sharedConnection);
        
        SqlDataReader reader = null;
        try
        {
            reader = command.ExecuteReader();
            
            while (reader.Read())
            {
                // Performance: Boxing/unboxing and multiple type conversions
                var task = new TaskItem
                {
                    Id = (Guid)reader["Id"],
                    Title = reader["Title"].ToString(),
                    Description = reader["Description"]?.ToString(),
                    CreatedById = (Guid)reader["CreatedById"],
                    AssignedToId = reader["AssignedToId"] == DBNull.Value ? Guid.Empty : (Guid)reader["AssignedToId"],
                    DueDate = reader["DueDate"] == DBNull.Value ? DateTime.MinValue : (DateTime)reader["DueDate"],
                    Status = (Domain.Entities.TaskStatus)Enum.Parse(typeof(Domain.Entities.TaskStatus), reader["Status"].ToString())
                };
                
                tasks.Add(task);
            }
        }
        catch (Exception ex)
        {
            // SQ: Catching all exceptions and logging sensitive information
            Console.WriteLine($"Error executing query '{sql}': {ex.Message}");
            Console.WriteLine($"Connection state: {_sharedConnection.State}");
            Console.WriteLine($"Connection string: {ConnectionString}");
        }
        finally
        {
            // SQ: Only disposing reader, not command
            reader?.Dispose();
        }
        
        return tasks;
    }

    // SQ: Method that starts global transaction but doesn't properly manage it
    public static void BeginGlobalTransaction()
    {
        try
        {
            if (_globalTransaction != null)
            {
                // SQ: Not checking transaction state before disposing
                _globalTransaction.Dispose();
            }
            
            _globalTransaction = _sharedConnection.BeginTransaction();
            Console.WriteLine("Global transaction started");
        }
        catch (Exception ex)
        {
            // SQ: Swallowing exception and not informing caller
            Console.WriteLine($"Failed to start transaction: {ex.Message}");
        }
    }

    // SQ: Method with resource leaks and poor error handling
    public static bool InsertTaskUnsafe(TaskItem task)
    {
        // Performance: String concatenation for SQL building
        var sql = "INSERT INTO Tasks (Id, Title, Description, CreatedById, AssignedToId, DueDate, Status) VALUES (";
        sql += "'" + task.Id.ToString() + "', ";
        sql += "'" + task.Title.Replace("'", "''") + "', "; // SQ: Manual SQL escaping - error prone
        sql += "'" + (task.Description?.Replace("'", "''") ?? "") + "', ";
        sql += "'" + task.CreatedById.ToString() + "', ";
        sql += task.AssignedToId == Guid.Empty ? "NULL, " : "'" + task.AssignedToId.ToString() + "', ";
        sql += "'" + task.DueDate.ToString("yyyy-MM-dd HH:mm:ss") + "', ";
        sql += "'" + task.Status.ToString() + "')";
        
        // SQ: Logging complete SQL with data
        Console.WriteLine($"Executing INSERT: {sql}");
        
        var command = new SqlCommand(sql, _sharedConnection);
        if (_globalTransaction != null)
        {
            command.Transaction = _globalTransaction;
        }
        
        try
        {
            var result = command.ExecuteNonQuery();
            return result > 0;
        }
        catch (SqlException ex)
        {
            // SQ: Exposing database structure and sensitive information
            Console.WriteLine($"SQL Error: {ex.Message}");
            Console.WriteLine($"Error Number: {ex.Number}");
            Console.WriteLine($"Severity: {ex.Class}");
            Console.WriteLine($"State: {ex.State}");
            Console.WriteLine($"Procedure: {ex.Procedure}");
            Console.WriteLine($"Line Number: {ex.LineNumber}");
            Console.WriteLine($"Server: {ex.Server}");
            return false;
        }
        finally
        {
            // SQ: Not disposing command object - resource leak
        }
    }

    // Performance: Method that causes N+1 query problem
    public static List<TaskItem> GetTasksWithAssigneeNames()
    {
        var sql = "SELECT * FROM Tasks";
        var command = new SqlCommand(sql, _sharedConnection);
        var tasks = new List<TaskItem>();
        
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var task = new TaskItem
                {
                    Id = (Guid)reader["Id"],
                    Title = reader["Title"].ToString(),
                    Description = reader["Description"]?.ToString(),
                    CreatedById = (Guid)reader["CreatedById"],
                    AssignedToId = reader["AssignedToId"] == DBNull.Value ? Guid.Empty : (Guid)reader["AssignedToId"],
                    DueDate = (DateTime)reader["DueDate"],
                    Status = (Domain.Entities.TaskStatus)Enum.Parse(typeof(Domain.Entities.TaskStatus), reader["Status"].ToString())
                };
                
                tasks.Add(task);
            }
        }
        
        // Performance: N+1 queries - separate query for each task's assignee
        foreach (var task in tasks)
        {
            if (task.AssignedToId != Guid.Empty)
            {
                // SQ: Another SQL injection risk
                var userSql = "SELECT Name FROM Users WHERE Id = '" + task.AssignedToId + "'";
                var userCommand = new SqlCommand(userSql, _sharedConnection);
                
                try
                {
                    var assigneeName = userCommand.ExecuteScalar()?.ToString();
                    // SQ: Modifying object in place during enumeration (if this were a concurrent collection)
                    // For demo purposes, we'd store this in a custom property or dictionary
                }
                catch
                {
                    // SQ: Silently ignoring errors
                }
                finally
                {
                    // SQ: Not disposing userCommand
                }
            }
        }
        
        return tasks;
    }

    // SQ: Method that doesn't validate input and has buffer overflow potential
    public static void BulkInsertTasks(List<TaskItem> tasks)
    {
        if (tasks == null) return; // SQ: Silent handling of null input
        
        // Performance: Building huge SQL string in memory
        var sqlBuilder = new StringBuilder();
        sqlBuilder.Append("INSERT INTO Tasks (Id, Title, Description, CreatedById, AssignedToId, DueDate, Status) VALUES ");
        
        for (int i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            
            // SQ: No null check on individual tasks
            sqlBuilder.Append($"('{task.Id}', '{task.Title}', '{task.Description}', '{task.CreatedById}', ");
            sqlBuilder.Append(task.AssignedToId == Guid.Empty ? "NULL" : $"'{task.AssignedToId}'");
            sqlBuilder.Append($", '{task.DueDate:yyyy-MM-dd HH:mm:ss}', '{task.Status}')");
            
            if (i < tasks.Count - 1)
            {
                sqlBuilder.Append(", ");
            }
        }
        
        var finalSql = sqlBuilder.ToString();
        
        // SQ: Logging potentially huge SQL statement
        Console.WriteLine($"Executing bulk insert with {finalSql.Length} characters");
        
        var command = new SqlCommand(finalSql, _sharedConnection);
        // SQ: No timeout set - potential for hanging operations
        
        try
        {
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            // SQ: Re-throwing exception without cleanup
            throw new InvalidOperationException($"Bulk insert failed: {ex.Message}", ex);
        }
        // SQ: Not disposing command
    }

    // SQ: Method that exposes sensitive system information
    public static string GetDatabaseInfo()
    {
        var info = new StringBuilder();
        
        // SQ: Exposing connection string with credentials
        info.AppendLine($"Connection String: {ConnectionString}");
        info.AppendLine($"Connection State: {_sharedConnection?.State}");
        info.AppendLine($"Database: {_sharedConnection?.Database}");
        info.AppendLine($"Server Version: {_sharedConnection?.ServerVersion}");
        info.AppendLine($"Workstation Id: {_sharedConnection?.WorkstationId}");
        
        // SQ: Executing system queries that could expose sensitive information
        try
        {
            var versionCommand = new SqlCommand("SELECT @@VERSION", _sharedConnection);
            var version = versionCommand.ExecuteScalar()?.ToString();
            info.AppendLine($"SQL Server Version: {version}");
            
            var userCommand = new SqlCommand("SELECT SYSTEM_USER, ORIGINAL_LOGIN()", _sharedConnection);
            using (var reader = userCommand.ExecuteReader())
            {
                if (reader.Read())
                {
                    info.AppendLine($"System User: {reader[0]}");
                    info.AppendLine($"Original Login: {reader[1]}");
                }
            }
        }
        catch (Exception ex)
        {
            info.AppendLine($"Error getting database info: {ex.Message}");
        }
        
        return info.ToString();
    }

    // SQ: Static finalizer - improper cleanup
    static void Cleanup()
    {
        // SQ: Static cleanup method that might never be called
        try
        {
            _globalTransaction?.Rollback(); // SQ: Always rolling back without checking state
            _globalTransaction?.Dispose();
            _sharedConnection?.Close();
            _sharedConnection?.Dispose();
        }
        catch
        {
            // SQ: Silently ignoring cleanup errors
        }
    }
}
