using AgenticTaskManager.API;
using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AgenticTaskManager.API.Tests.Integration
{
    public class TasksControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public TasksControllerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the existing DbContext registration
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    // Add in-memory database for testing
                    services.AddDbContext<AppDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("InMemoryDbForTesting");
                    });
                });
            });

            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task GetAllTasks_ShouldReturnSuccessAndCorrectContentType()
        {
            // Act
            var response = await _client.GetAsync("/api/tasks");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        }

        [Fact]
        public async Task CreateTask_ShouldCreateTaskSuccessfully()
        {
            // Arrange
            var newTask = new TaskDto
            {
                Title = "Integration Test Task",
                Description = "This is a test task created during integration testing",
                CreatedById = Guid.NewGuid()
            };

            var json = JsonSerializer.Serialize(newTask);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/tasks", content);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(201, (int)response.StatusCode); // Created

            var responseContent = await response.Content.ReadAsStringAsync();
            var createdTask = JsonSerializer.Deserialize<TaskDto>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(createdTask);
            Assert.Equal(newTask.Title, createdTask.Title);
            Assert.Equal(newTask.Description, createdTask.Description);
            Assert.True(createdTask.Id > 0);
        }

        [Fact]
        public async Task GetTaskById_WithValidId_ShouldReturnTask()
        {
            // Arrange - First create a task
            var newTask = new TaskDto
            {
                Title = "Test Task for Get",
                Description = "This task will be retrieved",
                CreatedById = Guid.NewGuid()
            };

            var json = JsonSerializer.Serialize(newTask);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var createResponse = await _client.PostAsync("/api/tasks", content);
            createResponse.EnsureSuccessStatusCode();

            var createResponseContent = await createResponse.Content.ReadAsStringAsync();
            var createdTask = JsonSerializer.Deserialize<TaskDto>(createResponseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Act
            var getResponse = await _client.GetAsync($"/api/tasks/{createdTask?.Id}");

            // Assert
            getResponse.EnsureSuccessStatusCode();
            var getResponseContent = await getResponse.Content.ReadAsStringAsync();
            var retrievedTask = JsonSerializer.Deserialize<TaskDto>(getResponseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(retrievedTask);
            Assert.Equal(createdTask?.Id, retrievedTask.Id);
            Assert.Equal(newTask.Title, retrievedTask.Title);
        }

        [Fact]
        public async Task UpdateTask_ShouldUpdateTaskSuccessfully()
        {
            // Arrange - First create a task
            var originalTask = new TaskDto
            {
                Title = "Original Task",
                Description = "Original Description",
                CreatedById = Guid.NewGuid()
            };

            var createJson = JsonSerializer.Serialize(originalTask);
            var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
            var createResponse = await _client.PostAsync("/api/tasks", createContent);
            createResponse.EnsureSuccessStatusCode();

            var createResponseContent = await createResponse.Content.ReadAsStringAsync();
            var createdTask = JsonSerializer.Deserialize<TaskDto>(createResponseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Update the task
            var updateTask = new TaskDto
            {
                Id = createdTask?.Id ?? 0,
                Title = "Updated Task",
                Description = "Updated Description",
                CreatedById = createdTask?.CreatedById ?? Guid.NewGuid()
            };

            var updateJson = JsonSerializer.Serialize(updateTask);
            var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");

            // Act
            var updateResponse = await _client.PutAsync($"/api/tasks/{createdTask?.Id}", updateContent);

            // Assert
            updateResponse.EnsureSuccessStatusCode();
            var updateResponseContent = await updateResponse.Content.ReadAsStringAsync();
            var updatedTask = JsonSerializer.Deserialize<TaskDto>(updateResponseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(updatedTask);
            Assert.Equal(updateTask.Title, updatedTask.Title);
            Assert.Equal(updateTask.Description, updatedTask.Description);
        }

        [Fact]
        public async Task DeleteTask_ShouldDeleteTaskSuccessfully()
        {
            // Arrange - First create a task
            var newTask = new TaskDto
            {
                Title = "Task to Delete",
                Description = "This task will be deleted",
                CreatedById = Guid.NewGuid()
            };

            var json = JsonSerializer.Serialize(newTask);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var createResponse = await _client.PostAsync("/api/tasks", content);
            createResponse.EnsureSuccessStatusCode();

            var createResponseContent = await createResponse.Content.ReadAsStringAsync();
            var createdTask = JsonSerializer.Deserialize<TaskDto>(createResponseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Act
            var deleteResponse = await _client.DeleteAsync($"/api/tasks/{createdTask?.Id}");

            // Assert
            deleteResponse.EnsureSuccessStatusCode();
            Assert.Equal(204, (int)deleteResponse.StatusCode); // No Content

            // Verify the task is deleted
            var getResponse = await _client.GetAsync($"/api/tasks/{createdTask?.Id}");
            Assert.Equal(404, (int)getResponse.StatusCode); // Not Found
        }

        [Fact]
        public async Task GetTaskById_WithInvalidId_ShouldReturnNotFound()
        {
            // Act
            var response = await _client.GetAsync("/api/tasks/99999");

            // Assert
            Assert.Equal(404, (int)response.StatusCode);
        }

        [Fact]
        public async Task CreateTask_WithInvalidData_ShouldReturnBadRequest()
        {
            // Arrange - Task with no title
            var invalidTask = new TaskDto
            {
                Title = "", // Invalid: empty title
                Description = "Valid description",
                CreatedById = Guid.NewGuid()
            };

            var json = JsonSerializer.Serialize(invalidTask);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/tasks", content);

            // Assert
            Assert.Equal(400, (int)response.StatusCode); // Bad Request
        }

        [Fact]
        public async Task SearchTasks_WithTitle_ShouldReturnMatchingTasks()
        {
            // Arrange - Create some test tasks
            var tasks = new[]
            {
                new TaskDto { Title = "Search Task 1", Description = "Description 1", CreatedById = Guid.NewGuid() },
                new TaskDto { Title = "Search Task 2", Description = "Description 2", CreatedById = Guid.NewGuid() },
                new TaskDto { Title = "Different Task", Description = "Description 3", CreatedById = Guid.NewGuid() }
            };

            foreach (var task in tasks)
            {
                var json = JsonSerializer.Serialize(task);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _client.PostAsync("/api/tasks", content);
            }

            // Act
            var response = await _client.GetAsync("/api/tasks/search?title=Search");

            // Assert
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var searchResults = JsonSerializer.Deserialize<List<TaskDto>>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(searchResults);
            // Should find at least the tasks with "Search" in the title
            Assert.True(searchResults.Any(t => t.Title.Contains("Search")));
        }
    }
}
