using AgenticTaskManager.Domain.Entities;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgenticTaskManager.Infrastructure.Data;

// Optimized: Refactored class to use proper dependency injection and connection management
public class LegacyDataAccess
{
    private readonly string _connectionString;
    private readonly ILogger<LegacyDataAccess>? _logger;

    // Fixed: Constructor-based dependency injection instead of static dependencies
    public LegacyDataAccess(IConfiguration configuration, ILogger<LegacyDataAccess>? logger = null)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration");
        _logger = logger;
    }

    // Fixed: SQL injection vulnerability eliminated with parameterized queries
    public List<TaskItem> GetTasksByUser(string userId)
    {
        var tasks = new  List<TaskItem>();
        // Fixed: Parameterized query to prevent SQL injection
        var sql = "SELECT * FROM Tasks WHERE CreatedById = @UserId OR AssignedToId = @UserId";
        
        // Fixed: Secure logging without exposing sensitive data
        _logger?.LogInformation("Executing GetTasksByUser query for user");
        
        // Fixed: Proper connection management with using statements
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        
        command.Parameters.AddWithValue("@UserId", userId);
        
        try
        {
            connection.Open();
            using var reader = command.ExecuteReader();
            
            while (reader.Read())
            {
                // Fixed: Safe type conversion with proper null handling
                var task = new TaskItem
                {
                    Id = reader.GetGuid("Id"),
                    Title = reader.GetString("Title"),
                    Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                    CreatedById = reader.GetGuid("CreatedById"),
                    AssignedToId = reader.IsDBNull("AssignedToId") ? Guid.Empty : reader.GetGuid("AssignedToId"),
                    DueDate = reader.IsDBNull("DueDate") ? DateTime.MinValue : reader.GetDateTime("DueDate"),
                    Status = Enum.Parse<Domain.Entities.TaskStatus>(reader.GetString("Status"))
                };
                
                tasks.Add(task);
            }
        }
        catch (Exception ex)
        {
            // Fixed: Proper exception handling without exposing sensitive information
            _logger?.LogError(ex, "Error retrieving tasks for user");
            throw;
        }
        
        return tasks;
    }

    // Fixed: Proper transaction management with state validation
    public SqlTransaction BeginTransaction(SqlConnection connection)
    {
        try
        {
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }
            
            var transaction = connection.BeginTransaction();
            _logger?.LogInformation("Transaction started successfully");
            return transaction;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start transaction");
            throw;
        }
    }

    // Fixed: Safe transaction commit with state validation
    public void CommitTransaction(SqlTransaction transaction)
    {
        if (transaction == null)
        {
            _logger?.LogWarning("Attempted to commit null transaction");
            return;
        }

        try
        {
            // Fixed: Check transaction state before committing
            if (transaction.Connection != null && transaction.Connection.State == ConnectionState.Open)
            {
                transaction.Commit();
                _logger?.LogInformation("Transaction committed successfully");
            }
            else
            {
                _logger?.LogWarning("Cannot commit transaction - connection is not open");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error committing transaction");
            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    // Fixed: Safe transaction rollback with state validation
    public void RollbackTransaction(SqlTransaction transaction)
    {
        if (transaction == null)
        {
            _logger?.LogWarning("Attempted to rollback null transaction");
            return;
        }

        try
        {
            // Fixed: Check transaction state before rolling back
            if (transaction.Connection != null && transaction.Connection.State == ConnectionState.Open)
            {
                transaction.Rollback();
                _logger?.LogInformation("Transaction rolled back successfully");
            }
            else
            {
                _logger?.LogWarning("Cannot rollback transaction - connection is not open");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error rolling back transaction");
            // Don't re-throw here as we're likely already in an error state
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    // Fixed: Secure parameterized insert with proper resource management
    public bool InsertTask(TaskItem task, SqlTransaction? transaction = null)
    {
        if (task == null)
        {
            _logger?.LogWarning("Attempted to insert null task");
            return false;
        }

        // Fixed: Parameterized query instead of string concatenation
        var sql = @"INSERT INTO Tasks (Id, Title, Description, CreatedById, AssignedToId, DueDate, Status) 
                    VALUES (@Id, @Title, @Description, @CreatedById, @AssignedToId, @DueDate, @Status)";
        
        _logger?.LogInformation("Inserting task with ID: {TaskId}", task.Id);
        
        SqlConnection? connectionToDispose = null;
        try
        {
            SqlConnection connection;
            if (transaction != null)
            {
                connection = transaction.Connection!;
            }
            else
            {
                connection = new SqlConnection(_connectionString);
                connectionToDispose = connection;
                connection.Open();
            }

            using var command = new SqlCommand(sql, connection, transaction);
            
            // Fixed: Proper parameter binding
            command.Parameters.AddWithValue("@Id", task.Id);
            command.Parameters.AddWithValue("@Title", task.Title ?? string.Empty);
            command.Parameters.AddWithValue("@Description", (object?)task.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@CreatedById", task.CreatedById);
            command.Parameters.AddWithValue("@AssignedToId", task.AssignedToId == Guid.Empty ? DBNull.Value : task.AssignedToId);
            command.Parameters.AddWithValue("@DueDate", task.DueDate);
            command.Parameters.AddWithValue("@Status", task.Status.ToString());
            
            var result = command.ExecuteNonQuery();
            return result > 0;
        }
        catch (SqlException ex)
        {
            // Fixed: Secure logging without exposing database structure
            _logger?.LogError(ex, "SQL error occurred while inserting task");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error occurred while inserting task");
            return false;
        }
        finally
        {
            // Fixed: Proper resource disposal
            connectionToDispose?.Dispose();
        }
    }

    // Fixed: Optimized query to eliminate N+1 problem using JOIN
    public List<TaskItem> GetTasksWithAssigneeNames()
    {
        var tasks = new List<TaskItem>();
        
        // Fixed: Single query with JOIN to avoid N+1 problem
        var sql = @"SELECT t.*, u.Name as AssigneeName 
                   FROM Tasks t 
                   LEFT JOIN Users u ON t.AssignedToId = u.Id";
        
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        
        try
        {
            connection.Open();
            using var reader = command.ExecuteReader();
            
            while (reader.Read())
            {
                var task = new TaskItem
                {
                    Id = reader.GetGuid("Id"),
                    Title = reader.GetString("Title"),
                    Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                    CreatedById = reader.GetGuid("CreatedById"),
                    AssignedToId = reader.IsDBNull("AssignedToId") ? Guid.Empty : reader.GetGuid("AssignedToId"),
                    DueDate = reader.GetDateTime("DueDate"),
                    Status = Enum.Parse<Domain.Entities.TaskStatus>(reader.GetString("Status"))
                };
                
                tasks.Add(task);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving tasks with assignee names");
            throw;
        }
        
        return tasks;
    }

    // Fixed: Secure bulk insert using SqlBulkCopy for better performance
    public void BulkInsertTasks(List<TaskItem> tasks)
    {
        if (tasks == null)
        {
            _logger?.LogWarning("BulkInsertTasks called with null tasks list");
            return;
        }

        if (tasks.Count == 0)
        {
            _logger?.LogInformation("BulkInsertTasks called with empty tasks list");
            return;
        }

        // Validate all tasks before processing
        for (int i = 0; i < tasks.Count; i++)
        {
            if (tasks[i] == null)
            {
                throw new ArgumentException($"Task at index {i} is null", nameof(tasks));
            }
        }

        _logger?.LogInformation("Starting bulk insert of {TaskCount} tasks", tasks.Count);

        using var connection = new SqlConnection(_connectionString);
        
        try
        {
            connection.Open();
            
            // Fixed: Use SqlBulkCopy for better performance instead of building huge SQL strings
            using var bulkCopy = new SqlBulkCopy(connection)
            {
                DestinationTableName = "Tasks",
                BatchSize = 1000,
                BulkCopyTimeout = 300 // 5 minutes timeout
            };

            // Create a DataTable to hold the data
            var dataTable = CreateTaskDataTable(tasks);
            
            // Map columns
            bulkCopy.ColumnMappings.Add("Id", "Id");
            bulkCopy.ColumnMappings.Add("Title", "Title");
            bulkCopy.ColumnMappings.Add("Description", "Description");
            bulkCopy.ColumnMappings.Add("CreatedById", "CreatedById");
            bulkCopy.ColumnMappings.Add("AssignedToId", "AssignedToId");
            bulkCopy.ColumnMappings.Add("DueDate", "DueDate");
            bulkCopy.ColumnMappings.Add("Status", "Status");

            bulkCopy.WriteToServer(dataTable);
            
            _logger?.LogInformation("Successfully completed bulk insert of {TaskCount} tasks", tasks.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Bulk insert failed for {TaskCount} tasks", tasks.Count);
            throw;
        }
    }

    // Helper method to create DataTable from TaskItem list
    private static DataTable CreateTaskDataTable(List<TaskItem> tasks)
    {
        var dataTable = new DataTable();
        dataTable.Columns.Add("Id", typeof(Guid));
        dataTable.Columns.Add("Title", typeof(string));
        dataTable.Columns.Add("Description", typeof(string));
        dataTable.Columns.Add("CreatedById", typeof(Guid));
        dataTable.Columns.Add("AssignedToId", typeof(Guid));
        dataTable.Columns.Add("DueDate", typeof(DateTime));
        dataTable.Columns.Add("Status", typeof(string));

        foreach (var task in tasks)
        {
            dataTable.Rows.Add(
                task.Id,
                task.Title,
                task.Description,
                task.CreatedById,
                task.AssignedToId == Guid.Empty ? DBNull.Value : task.AssignedToId,
                task.DueDate,
                task.Status.ToString()
            );
        }

        return dataTable;
    }

    // Fixed: Secure method that doesn't expose sensitive system information
    public string GetDatabaseInfo()
    {
        var info = new StringBuilder();
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            
            // Fixed: Only expose non-sensitive database information
            info.AppendLine($"Database: {connection.Database}");
            info.AppendLine($"Connection State: {connection.State}");
            
            // Fixed: Safe system queries that don't expose sensitive information
            using var versionCommand = new SqlCommand("SELECT @@VERSION", connection);
            var version = versionCommand.ExecuteScalar()?.ToString();
            // Only show SQL Server version, not detailed system info
            if (!string.IsNullOrEmpty(version))
            {
                var versionParts = version.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (versionParts.Length > 0)
                {
                    info.AppendLine($"SQL Server Version: {versionParts[0]}");
                }
            }
            
            _logger?.LogInformation("Database info retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting database info");
            info.AppendLine("Error retrieving database information");
        }
        
        return info.ToString();
    }
}
