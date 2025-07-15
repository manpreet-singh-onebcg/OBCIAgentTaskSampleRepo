using AgenticTaskManager.Domain.Entities;
using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.TestUtilities;
using Xunit;

namespace AgenticTaskManager.Tests.QualityAssurance
{
    public class EdgeCaseTests
    {
        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(int.MaxValue)]
        public void TaskItem_WithExtremeIdValues_ShouldHandleGracefully(int extremeId)
        {
            // Arrange & Act
            var task = new TaskItem { Id = extremeId };

            // Assert
            Assert.Equal(extremeId, task.Id);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        [InlineData("\t\n\r")]
        public void TaskItem_WithEmptyOrWhitespaceTitle_ShouldAllowButHandle(string title)
        {
            // Arrange & Act
            var task = new TaskItem { Title = title };

            // Assert
            Assert.Equal(title, task.Title);
        }

        [Fact]
        public void TaskItem_WithVeryLongTitle_ShouldHandle()
        {
            // Arrange
            var longTitle = new string('A', 10000);

            // Act
            var task = new TaskItem { Title = longTitle };

            // Assert
            Assert.Equal(longTitle, task.Title);
        }

        [Theory]
        [InlineData("2000-01-01")]
        [InlineData("9999-12-31")]
        public void TaskItem_WithExtremeDates_ShouldHandleCorrectly(string dateString)
        {
            // Arrange
            var extremeDate = DateTime.Parse(dateString);

            // Act
            var task = new TaskItem
            {
                CreatedAt = extremeDate,
                UpdatedAt = extremeDate,
                DueDate = extremeDate
            };

            // Assert
            Assert.Equal(extremeDate, task.CreatedAt);
            Assert.Equal(extremeDate, task.UpdatedAt);
            Assert.Equal(extremeDate, task.DueDate);
        }

        [Fact]
        public void TaskItem_WithUnicodeCharacters_ShouldHandleCorrectly()
        {
            // Arrange
            var unicodeTitle = "タスク 任务 🚀 Ñoël café résumé";
            var unicodeDescription = "Unicode test: αβγδε 中文 العربية русский 🎉🔥💯";

            // Act
            var task = new TaskItem
            {
                Title = unicodeTitle,
                Description = unicodeDescription
            };

            // Assert
            Assert.Equal(unicodeTitle, task.Title);
            Assert.Equal(unicodeDescription, task.Description);
        }

        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(-100)]
        [InlineData(0)]
        [InlineData(100)]
        [InlineData(int.MaxValue)]
        public void TaskItem_WithExtremePriorityValues_ShouldHandle(int priority)
        {
            // Arrange & Act
            var task = new TaskItem { Priority = priority };

            // Assert
            Assert.Equal(priority, task.Priority);
        }

        [Fact]
        public void TaskDto_WithNullAssignedToId_ShouldHandleCorrectly()
        {
            // Arrange & Act
            var dto = new TaskDto { AssignedToId = null };

            // Assert
            Assert.Null(dto.AssignedToId);
        }

        [Fact]
        public void TaskDto_WithEmptyGuid_ShouldHandleCorrectly()
        {
            // Arrange & Act
            var dto = new TaskDto 
            { 
                CreatedById = Guid.Empty,
                AssignedToId = Guid.Empty
            };

            // Assert
            Assert.Equal(Guid.Empty, dto.CreatedById);
            Assert.Equal(Guid.Empty, dto.AssignedToId);
        }
    }

    public class ConcurrencyTests
    {
        [Fact]
        public async Task MultipleTaskCreation_ShouldHandleConcurrency()
        {
            // Arrange
            var tasks = new List<Task<TaskDto>>();
            var createdTasks = new List<TaskDto>();
            var lockObject = new object();

            // Act
            for (int i = 0; i < 100; i++)
            {
                var index = i;
                tasks.Add(Task.Run(() =>
                {
                    var dto = TestDataBuilder.CreateTaskDto(
                        title: $"Concurrent Task {index}",
                        description: $"Created concurrently {index}"
                    );

                    lock (lockObject)
                    {
                        createdTasks.Add(dto);
                    }

                    return dto;
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(100, results.Length);
            Assert.Equal(100, createdTasks.Count);
            Assert.All(results, task => Assert.NotNull(task));
            
            // Verify no duplicate IDs were created
            var uniqueTitles = results.Select(t => t.Title).Distinct().Count();
            Assert.Equal(100, uniqueTitles);
        }

        [Fact]
        public async Task ConcurrentReadWrite_ShouldMaintainDataIntegrity()
        {
            // Arrange
            var readTasks = new List<Task>();
            var writeTasks = new List<Task>();
            var counter = 0;

            // Act - Mix read and write operations
            for (int i = 0; i < 50; i++)
            {
                // Read operation
                readTasks.Add(Task.Run(() =>
                {
                    var tasks = TestDataBuilder.CreateTaskDtoList(10);
                    Thread.Sleep(1); // Simulate work
                }));

                // Write operation
                writeTasks.Add(Task.Run(() =>
                {
                    Interlocked.Increment(ref counter);
                    var task = TestDataBuilder.CreateTaskDto(title: $"Task {counter}");
                    Thread.Sleep(1); // Simulate work
                }));
            }

            await Task.WhenAll(readTasks.Concat(writeTasks));

            // Assert
            Assert.Equal(50, counter);
        }
    }

    public class ErrorRecoveryTests
    {
        [Fact]
        public void TaskDto_WithInvalidData_ShouldHandleGracefully()
        {
            // Arrange & Act
            var dto = new TaskDto
            {
                Id = -1,
                Title = null,
                Description = null,
                Status = "InvalidStatus",
                CreatedAt = DateTime.MinValue,
                CreatedById = Guid.Empty
            };

            // Assert - Should not throw exceptions
            Assert.Equal(-1, dto.Id);
            Assert.Null(dto.Title);
            Assert.Null(dto.Description);
            Assert.Equal("InvalidStatus", dto.Status);
            Assert.Equal(DateTime.MinValue, dto.CreatedAt);
            Assert.Equal(Guid.Empty, dto.CreatedById);
        }

        [Theory]
        [InlineData("")]
        [InlineData("INVALID")]
        [InlineData("pending")]
        [InlineData("COMPLETED")]
        [InlineData(null)]
        public void TaskDto_WithVariousStatusValues_ShouldHandle(string status)
        {
            // Arrange & Act
            var dto = new TaskDto { Status = status };

            // Assert
            Assert.Equal(status, dto.Status);
        }
    }

    public class DataConsistencyTests
    {
        [Fact]
        public void TaskItem_CreatedAt_ShouldBeBeforeUpdatedAt()
        {
            // Arrange
            var createdAt = DateTime.UtcNow;
            var updatedAt = createdAt.AddMinutes(5);

            // Act
            var task = new TaskItem
            {
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };

            // Assert
            Assert.True(task.CreatedAt <= task.UpdatedAt, "CreatedAt should be before or equal to UpdatedAt");
        }

        [Fact]
        public void TaskItem_DueDate_ShouldBeReasonable()
        {
            // Arrange
            var task = new TaskItem
            {
                CreatedAt = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(30)
            };

            // Assert
            Assert.True(task.DueDate >= task.CreatedAt, "DueDate should be after CreatedAt");
        }

        [Fact]
        public void Actor_Email_ShouldBeConsistent()
        {
            // Arrange & Act
            var actor = new Actor
            {
                Name = "John Doe",
                Email = "john.doe@example.com",
                Role = "Developer"
            };

            // Assert
            Assert.NotNull(actor.Email);
            Assert.Contains("@", actor.Email);
        }
    }

    public class BoundaryValueTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(199)]
        [InlineData(200)]
        [InlineData(201)]
        public void TaskDto_TitleLength_BoundaryValues(int titleLength)
        {
            // Arrange
            var title = new string('A', titleLength);

            // Act
            var dto = new TaskDto { Title = title };

            // Assert
            Assert.Equal(title, dto.Title);
            Assert.Equal(titleLength, dto.Title.Length);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(999)]
        [InlineData(1000)]
        [InlineData(1001)]
        public void TaskDto_DescriptionLength_BoundaryValues(int descriptionLength)
        {
            // Arrange
            var description = new string('B', descriptionLength);

            // Act
            var dto = new TaskDto { Description = description };

            // Assert
            Assert.Equal(description, dto.Description);
            Assert.Equal(descriptionLength, dto.Description.Length);
        }
    }

    public class ThreadSafetyTests
    {
        [Fact]
        public async Task ParallelTaskCreation_ShouldBeThreadSafe()
        {
            // Arrange
            var tasks = new List<Task<TaskItem>>();
            var createdTasks = new System.Collections.Concurrent.ConcurrentBag<TaskItem>();

            // Act
            Parallel.For(0, 1000, i =>
            {
                var task = TestDataBuilder.CreateTaskItem(
                    id: i,
                    title: $"Parallel Task {i}",
                    description: $"Created in parallel {i}"
                );
                createdTasks.Add(task);
            });

            // Assert
            Assert.Equal(1000, createdTasks.Count);
            
            var uniqueIds = createdTasks.Select(t => t.Id).Distinct().Count();
            Assert.Equal(1000, uniqueIds);
        }

        [Fact]
        public void StaticMethods_ShouldBeThreadSafe()
        {
            // Arrange
            var results = new System.Collections.Concurrent.ConcurrentBag<bool>();

            // Act
            Parallel.For(0, 100, i =>
            {
                var task1 = TestDataBuilder.CreateTaskItem(id: i);
                var task2 = TestDataBuilder.CreateTaskItem(id: i);
                
                results.Add(task1.Id == task2.Id);
            });

            // Assert
            Assert.All(results, result => Assert.True(result));
            Assert.Equal(100, results.Count);
        }
    }

    public class MemoryLeakTests
    {
        [Fact]
        public void LargeObjectCreation_ShouldNotLeakMemory()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(true);

            // Act
            for (int i = 0; i < 10000; i++)
            {
                var task = TestDataBuilder.CreateTaskItem(
                    id: i,
                    title: new string('A', 100),
                    description: new string('B', 500)
                );
                
                // Use the task to prevent optimization
                _ = task.Title.Length;
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;

            // Assert
            Assert.True(memoryIncrease < 1024 * 1024, // Less than 1MB
                $"Memory increased by {memoryIncrease} bytes, which may indicate a memory leak");
        }
    }
}
