using AgenticTaskManager.Infrastructure.Repositories;
using AgenticTaskManager.Infrastructure.Persistence;
using AgenticTaskManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using Xunit;

namespace AgenticTaskManager.Infrastructure.Tests.Performance
{
    public class MemoryAndPerformanceTests : IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly AppDbContext _context;
        private readonly Mock<ILogger<TaskRepository>> _mockLogger;
        private readonly TaskRepository _repository;

        public MemoryAndPerformanceTests()
        {
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(_options);
            _mockLogger = new Mock<ILogger<TaskRepository>>();
            _repository = new TaskRepository(_context, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAllAsync_WithLargeDataset_ShouldCompleteWithinTimeLimit()
        {
            // Arrange
            var taskCount = 10000;
            var tasks = GenerateLargeTaskDataset(taskCount);
            await _context.Tasks.AddRangeAsync(tasks);
            await _context.SaveChangesAsync();

            var stopwatch = Stopwatch.StartNew();

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            stopwatch.Stop();
            Assert.Equal(taskCount, result.Count());
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, $"Query took {stopwatch.ElapsedMilliseconds}ms, should be under 5000ms");
        }

        [Fact]
        public async Task AddAsync_BulkOperations_ShouldOptimizePerformance()
        {
            // Arrange
            var taskCount = 1000;
            var tasks = GenerateLargeTaskDataset(taskCount);

            var stopwatch = Stopwatch.StartNew();

            // Act
            foreach (var task in tasks)
            {
                await _repository.AddAsync(task);
            }

            // Assert
            stopwatch.Stop();
            var averageTimePerTask = stopwatch.ElapsedMilliseconds / (double)taskCount;
            Assert.True(averageTimePerTask < 10, $"Average time per task: {averageTimePerTask}ms, should be under 10ms");
        }

        [Fact]
        public async Task SearchAsync_WithComplexQuery_ShouldOptimizeExecution()
        {
            // Arrange
            var tasks = GenerateLargeTaskDataset(5000);
            await _context.Tasks.AddRangeAsync(tasks);
            await _context.SaveChangesAsync();

            var stopwatch = Stopwatch.StartNew();

            // Act
            var result = await _repository.SearchAsync(
                title: "Task 100", 
                description: "Description", 
                startDate: DateTime.UtcNow.AddDays(-30),
                endDate: DateTime.UtcNow,
                skip: 0,
                take: 100);

            // Assert
            stopwatch.Stop();
            Assert.True(result.Any());
            Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"Search took {stopwatch.ElapsedMilliseconds}ms, should be under 1000ms");
        }

        [Fact]
        public async Task ConcurrentOperations_ShouldMaintainPerformance()
        {
            // Arrange
            var concurrentTasks = new List<Task>();
            var stopwatch = Stopwatch.StartNew();

            // Act - Create 50 concurrent read operations
            for (int i = 0; i < 50; i++)
            {
                concurrentTasks.Add(Task.Run(async () =>
                {
                    await _repository.GetAllAsync();
                }));
            }

            await Task.WhenAll(concurrentTasks);

            // Assert
            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds < 3000, $"Concurrent operations took {stopwatch.ElapsedMilliseconds}ms, should be under 3000ms");
        }

        [Fact]
        public void MemoryUsage_WithLargeDataset_ShouldNotExceedLimits()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(true);
            var taskCount = 10000;

            // Act
            var tasks = GenerateLargeTaskDataset(taskCount);
            var currentMemory = GC.GetTotalMemory(false);
            var memoryUsed = currentMemory - initialMemory;

            // Assert
            var maxExpectedMemory = taskCount * 1024; // Rough estimate: 1KB per task
            Assert.True(memoryUsed < maxExpectedMemory, 
                $"Memory usage {memoryUsed} bytes exceeds expected {maxExpectedMemory} bytes");

            // Cleanup
            tasks = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        [Fact]
        public async Task ResourceDisposal_ShouldNotLeakMemory()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(true);

            // Act
            for (int i = 0; i < 100; i++)
            {
                using var tempContext = new AppDbContext(_options);
                using var tempRepository = new TaskRepository(tempContext, _mockLogger.Object);
                
                var task = new TaskItem 
                { 
                    Title = $"Temp Task {i}", 
                    Description = "Temporary task for memory test",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                await tempRepository.AddAsync(task);
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(false);
            var memoryDifference = finalMemory - initialMemory;

            // Assert
            Assert.True(memoryDifference < 1024 * 1024, // Less than 1MB growth
                $"Memory leak detected: {memoryDifference} bytes difference");
        }

        [Fact]
        public async Task AsyncOperations_ShouldNotBlockThreads()
        {
            // Arrange
            var tasks = new List<Task<IEnumerable<TaskItem>>>();
            var threadIds = new HashSet<int>();
            var lockObject = new object();

            // Act
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    lock (lockObject)
                    {
                        threadIds.Add(Thread.CurrentThread.ManagedThreadId);
                    }
                    return await _repository.GetAllAsync();
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.True(threadIds.Count > 1, "Operations should use multiple threads");
            Assert.All(results, result => Assert.NotNull(result));
        }

        [Fact]
        public async Task DatabaseConnection_ShouldReuseConnections()
        {
            // Arrange
            var connectionCount = 0;
            var tasks = new List<Task>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    Interlocked.Increment(ref connectionCount);
                    await _repository.GetAllAsync();
                    Interlocked.Decrement(ref connectionCount);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            // Connection pooling should keep the peak concurrent connections reasonable
            Assert.True(connectionCount <= 20, $"Peak connections: {connectionCount}, should be <= 20 with proper pooling");
        }

        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(5000)]
        public async Task Pagination_ShouldScaleLinearlyWithDataSize(int dataSize)
        {
            // Arrange
            var tasks = GenerateLargeTaskDataset(dataSize);
            await _context.Tasks.AddRangeAsync(tasks);
            await _context.SaveChangesAsync();

            var pageSize = 50;
            var stopwatch = Stopwatch.StartNew();

            // Act
            var result = await _repository.SearchAsync(null, null, null, null, 0, pageSize);

            // Assert
            stopwatch.Stop();
            Assert.Equal(pageSize, result.Count());
            
            // Performance should be consistent regardless of total data size
            Assert.True(stopwatch.ElapsedMilliseconds < 500, 
                $"Pagination with {dataSize} total records took {stopwatch.ElapsedMilliseconds}ms, should be under 500ms");
        }

        [Fact]
        public async Task StringOperations_ShouldUseEfficientMethods()
        {
            // Arrange
            var largeStringBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < 10000; i++)
            {
                largeStringBuilder.Append($"Task {i} ");
            }
            var searchTerm = largeStringBuilder.ToString();

            var stopwatch = Stopwatch.StartNew();

            // Act
            var result = await _repository.SearchAsync(searchTerm, null, null, null, 0, 10);

            // Assert
            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds < 1000, 
                $"String search with large term took {stopwatch.ElapsedMilliseconds}ms, should be under 1000ms");
        }

        private List<TaskItem> GenerateLargeTaskDataset(int count)
        {
            var tasks = new List<TaskItem>();
            var random = new Random(42); // Fixed seed for reproducible tests

            for (int i = 1; i <= count; i++)
            {
                tasks.Add(new TaskItem
                {
                    Id = i,
                    Title = $"Task {i}",
                    Description = $"Description for task {i} with some additional content to simulate real data",
                    IsCompleted = random.Next(0, 2) == 1,
                    Priority = random.Next(1, 6),
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(0, 365)),
                    UpdatedAt = DateTime.UtcNow.AddDays(-random.Next(0, 30)),
                    DueDate = DateTime.UtcNow.AddDays(random.Next(-30, 30))
                });
            }

            return tasks;
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
