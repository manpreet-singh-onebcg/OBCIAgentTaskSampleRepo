using AgenticTaskManager.Domain.Entities;
using AgenticTaskManager.Application.DTOs;

namespace AgenticTaskManager.TestUtilities
{
    public static class TestDataBuilder
    {
        public static TaskItem CreateTaskItem(
            int id = 1,
            string title = "Test Task",
            string description = "Test Description",
            bool isCompleted = false,
            int priority = 1,
            int? assignedActorId = null,
            DateTime? createdAt = null,
            DateTime? updatedAt = null,
            DateTime? dueDate = null)
        {
            return new TaskItem
            {
                Id = id,
                Title = title,
                Description = description,
                IsCompleted = isCompleted,
                Priority = priority,
                AssignedActorId = assignedActorId,
                CreatedAt = createdAt ?? DateTime.UtcNow,
                UpdatedAt = updatedAt ?? DateTime.UtcNow,
                DueDate = dueDate
            };
        }

        public static TaskDto CreateTaskDto(
            int id = 0,
            string title = "Test Task",
            string description = "Test Description",
            string status = "Pending",
            Guid? assignedToId = null,
            DateTime? createdAt = null,
            DateTime? updatedAt = null,
            DateTime? dueDate = null)
        {
            return new TaskDto
            {
                Id = id,
                Title = title,
                Description = description,
                Status = status,
                AssignedToId = assignedToId,
                CreatedById = Guid.NewGuid(),
                CreatedAt = createdAt ?? DateTime.UtcNow,
                UpdatedAt = updatedAt,
                DueDate = dueDate
            };
        }

        public static Actor CreateActor(
            int id = 1,
            string name = "Test Actor",
            string email = "test@example.com",
            string role = "Developer",
            DateTime? createdAt = null,
            DateTime? updatedAt = null)
        {
            return new Actor
            {
                Id = id,
                Name = name,
                Email = email,
                Role = role,
                CreatedAt = createdAt ?? DateTime.UtcNow,
                UpdatedAt = updatedAt ?? DateTime.UtcNow
            };
        }

        public static List<TaskItem> CreateTaskItemList(int count = 3)
        {
            var tasks = new List<TaskItem>();
            for (int i = 1; i <= count; i++)
            {
                tasks.Add(CreateTaskItem(
                    id: i,
                    title: $"Task {i}",
                    description: $"Description for task {i}",
                    priority: i % 3 + 1,
                    isCompleted: i % 2 == 0
                ));
            }
            return tasks;
        }

        public static List<TaskDto> CreateTaskDtoList(int count = 3)
        {
            var tasks = new List<TaskDto>();
            for (int i = 1; i <= count; i++)
            {
                tasks.Add(CreateTaskDto(
                    id: i,
                    title: $"Task {i}",
                    description: $"Description for task {i}",
                    status: i % 2 == 0 ? "Completed" : "Pending"
                ));
            }
            return tasks;
        }

        public static List<Actor> CreateActorList(int count = 3)
        {
            var actors = new List<Actor>();
            for (int i = 1; i <= count; i++)
            {
                actors.Add(CreateActor(
                    id: i,
                    name: $"Actor {i}",
                    email: $"actor{i}@example.com",
                    role: i == 1 ? "Manager" : "Developer"
                ));
            }
            return actors;
        }

        public static TaskItem CreateOverdueTask(int id = 1, int daysOverdue = 1)
        {
            return CreateTaskItem(
                id: id,
                title: $"Overdue Task {id}",
                description: "This task is overdue",
                isCompleted: false,
                dueDate: DateTime.UtcNow.AddDays(-daysOverdue)
            );
        }

        public static TaskItem CreateCompletedTask(int id = 1)
        {
            return CreateTaskItem(
                id: id,
                title: $"Completed Task {id}",
                description: "This task is completed",
                isCompleted: true,
                updatedAt: DateTime.UtcNow.AddDays(-1)
            );
        }

        public static TaskItem CreateHighPriorityTask(int id = 1)
        {
            return CreateTaskItem(
                id: id,
                title: $"High Priority Task {id}",
                description: "This is a high priority task",
                priority: 5
            );
        }

        public static TaskItem CreateLowPriorityTask(int id = 1)
        {
            return CreateTaskItem(
                id: id,
                title: $"Low Priority Task {id}",
                description: "This is a low priority task",
                priority: 1
            );
        }

        public static string GenerateSecureToken()
        {
            var bytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }

        public static string EncryptSensitiveData(string data)
        {
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException("Data cannot be null or empty", nameof(data));

            // Simple encryption for testing purposes
            var bytes = System.Text.Encoding.UTF8.GetBytes(data);
            return Convert.ToBase64String(bytes.Reverse().ToArray());
        }

        public static string DecryptSensitiveData(string encryptedData)
        {
            if (string.IsNullOrEmpty(encryptedData))
                throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));

            try
            {
                var bytes = Convert.FromBase64String(encryptedData);
                return System.Text.Encoding.UTF8.GetString(bytes.Reverse().ToArray());
            }
            catch
            {
                throw new ArgumentException("Invalid encrypted data format", nameof(encryptedData));
            }
        }

        public static bool IsValidFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            // Check for path traversal attempts
            var pathTraversalPatterns = new[]
            {
                "..",
                "%2e%2e",
                "..\\",
                "../",
                "....//",
                "....\\\\",
                "%2f",
                "%5c"
            };

            var normalizedPath = filePath.ToLowerInvariant();
            return !pathTraversalPatterns.Any(pattern => normalizedPath.Contains(pattern));
        }
    }
}
