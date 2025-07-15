using AgenticTaskManager.API.Controllers;
using AgenticTaskManager.Application.DTOs;
using AgenticTaskManager.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgenticTaskManager.API.Tests.Controllers
{
    public class TasksControllerTests
    {
        private readonly Mock<ITaskService> _mockTaskService;
        private readonly Mock<ILogger<TasksController>> _mockLogger;
        private readonly TasksController _controller;

        public TasksControllerTests()
        {
            _mockTaskService = new Mock<ITaskService>();
            _mockLogger = new Mock<ILogger<TasksController>>();
            _controller = new TasksController(_mockTaskService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAllTasks_ShouldReturnOkWithTasks()
        {
            // Arrange
            var tasks = new List<TaskDto>
            {
                new TaskDto { Id = 1, Title = "Task 1", Description = "Description 1", CreatedById = Guid.NewGuid() },
                new TaskDto { Id = 2, Title = "Task 2", Description = "Description 2", CreatedById = Guid.NewGuid() }
            };
            _mockTaskService.Setup(s => s.GetAllAsync()).ReturnsAsync(tasks);

            // Act
            var result = await _controller.GetAll();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedTasks = Assert.IsType<List<TaskDto>>(okResult.Value);
            Assert.Equal(2, returnedTasks.Count);
            _mockTaskService.Verify(s => s.GetAllAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllTasks_WhenServiceThrows_ShouldReturnInternalServerError()
        {
            // Arrange
            _mockTaskService.Setup(s => s.GetAllAsync()).ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetAll();

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        [Fact]
        public async Task GetById_WithValidId_ShouldReturnOkWithTask()
        {
            // Arrange
            var taskId = 1;
            var task = new TaskDto { Id = taskId, Title = "Test Task", Description = "Test Description", CreatedById = Guid.NewGuid() };
            _mockTaskService.Setup(s => s.GetByIdAsync(taskId)).ReturnsAsync(task);

            // Act
            var result = await _controller.GetById(taskId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedTask = Assert.IsType<TaskDto>(okResult.Value);
            Assert.Equal(taskId, returnedTask.Id);
            _mockTaskService.Verify(s => s.GetByIdAsync(taskId), Times.Once);
        }

        [Fact]
        public async Task GetById_WithInvalidId_ShouldReturnNotFound()
        {
            // Arrange
            var taskId = 999;
            _mockTaskService.Setup(s => s.GetByIdAsync(taskId)).ReturnsAsync((TaskDto?)null);

            // Act
            var result = await _controller.GetById(taskId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public async Task GetById_WithInvalidId_ShouldReturnBadRequest(int invalidId)
        {
            // Act
            var result = await _controller.GetById(invalidId);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Create_WithValidDto_ShouldReturnCreatedAtAction()
        {
            // Arrange
            var taskDto = new TaskDto
            {
                Title = "New Task",
                Description = "New Description",
                CreatedById = Guid.NewGuid()
            };

            var createdTask = new TaskDto
            {
                Id = 1,
                Title = taskDto.Title,
                Description = taskDto.Description,
                CreatedById = taskDto.CreatedById
            };

            _mockTaskService.Setup(s => s.CreateAsync(taskDto)).ReturnsAsync(createdTask);

            // Act
            var result = await _controller.Create(taskDto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(TasksController.GetById), createdResult.ActionName);
            var returnedTask = Assert.IsType<TaskDto>(createdResult.Value);
            Assert.Equal(createdTask.Id, returnedTask.Id);
            _mockTaskService.Verify(s => s.CreateAsync(taskDto), Times.Once);
        }

        [Fact]
        public async Task Create_WithNullDto_ShouldReturnBadRequest()
        {
            // Act
            var result = await _controller.Create(null);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Create_WhenServiceThrows_ShouldReturnInternalServerError()
        {
            // Arrange
            var taskDto = new TaskDto { Title = "Test", CreatedById = Guid.NewGuid() };
            _mockTaskService.Setup(s => s.CreateAsync(taskDto)).ThrowsAsync(new ArgumentException("Invalid title"));

            // Act
            var result = await _controller.Create(taskDto);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        [Fact]
        public async Task Update_WithValidData_ShouldReturnOkWithUpdatedTask()
        {
            // Arrange
            var taskId = 1;
            var taskDto = new TaskDto { Id = taskId, Title = "Updated Task", Description = "Updated Description", CreatedById = Guid.NewGuid() };
            var updatedTask = new TaskDto { Id = taskId, Title = "Updated Task", Description = "Updated Description", CreatedById = taskDto.CreatedById };
            
            _mockTaskService.Setup(s => s.UpdateAsync(taskId, taskDto)).ReturnsAsync(updatedTask);

            // Act
            var result = await _controller.Update(taskId, taskDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedTask = Assert.IsType<TaskDto>(okResult.Value);
            Assert.Equal(taskId, returnedTask.Id);
            _mockTaskService.Verify(s => s.UpdateAsync(taskId, taskDto), Times.Once);
        }

        [Fact]
        public async Task Update_WithNonExistentTask_ShouldReturnNotFound()
        {
            // Arrange
            var taskId = 999;
            var taskDto = new TaskDto { Title = "Updated Task", CreatedById = Guid.NewGuid() };
            _mockTaskService.Setup(s => s.UpdateAsync(taskId, taskDto)).ReturnsAsync((TaskDto?)null);

            // Act
            var result = await _controller.Update(taskId, taskDto);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task Update_WithNullDto_ShouldReturnBadRequest()
        {
            // Act
            var result = await _controller.Update(1, null);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Delete_WithValidId_ShouldReturnNoContent()
        {
            // Arrange
            var taskId = 1;
            _mockTaskService.Setup(s => s.DeleteAsync(taskId)).ReturnsAsync(true);

            // Act
            var result = await _controller.Delete(taskId);

            // Assert
            Assert.IsType<NoContentResult>(result);
            _mockTaskService.Verify(s => s.DeleteAsync(taskId), Times.Once);
        }

        [Fact]
        public async Task Delete_WithNonExistentTask_ShouldReturnNotFound()
        {
            // Arrange
            var taskId = 999;
            _mockTaskService.Setup(s => s.DeleteAsync(taskId)).ReturnsAsync(false);

            // Act
            var result = await _controller.Delete(taskId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public async Task Delete_WithInvalidId_ShouldReturnBadRequest(int invalidId)
        {
            // Act
            var result = await _controller.Delete(invalidId);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Delete_WhenServiceThrows_ShouldReturnInternalServerError()
        {
            // Arrange
            var taskId = 1;
            _mockTaskService.Setup(s => s.DeleteAsync(taskId)).ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Delete(taskId);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        [Theory]
        [InlineData("<script>alert('xss')</script>")]
        [InlineData("'; DROP TABLE Tasks; --")]
        [InlineData("<img src=x onerror=alert('xss')>")]
        public async Task Create_WithMaliciousInput_ShouldValidateInput(string maliciousInput)
        {
            // Arrange
            var taskDto = new TaskDto
            {
                Title = maliciousInput,
                Description = "Normal description",
                CreatedById = Guid.NewGuid()
            };

            // Act
            var result = await _controller.Create(taskDto);

            // Assert - Should either return BadRequest or safely handle the input
            // Depending on implementation, it might pass through to service layer for handling
            Assert.True(result is BadRequestObjectResult || result is CreatedAtActionResult);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public async Task Create_WithInvalidTitle_ShouldReturnBadRequest(string invalidTitle)
        {
            // Arrange
            var taskDto = new TaskDto
            {
                Title = invalidTitle,
                Description = "Valid description",
                CreatedById = Guid.NewGuid()
            };

            // Act
            var result = await _controller.Create(taskDto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Create_WithExcessivelyLongTitle_ShouldReturnBadRequest()
        {
            // Arrange
            var longTitle = new string('A', 250); // Longer than 200 characters
            var taskDto = new TaskDto
            {
                Title = longTitle,
                Description = "Valid description",
                CreatedById = Guid.NewGuid()
            };

            // Act
            var result = await _controller.Create(taskDto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SearchTasks_WithValidParameters_ShouldReturnOkWithTasks()
        {
            // Arrange
            var tasks = new List<TaskDto>
            {
                new TaskDto { Id = 1, Title = "Matching Task", Description = "Description", CreatedById = Guid.NewGuid() }
            };
            _mockTaskService.Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>()))
                           .ReturnsAsync(tasks);

            // Act
            var result = await _controller.SearchTasks("test");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedTasks = Assert.IsType<List<TaskDto>>(okResult.Value);
            Assert.Single(returnedTasks);
        }

        [Fact]
        public async Task Create_WhenServiceReturnsNull_ShouldReturnInternalServerError()
        {
            // Arrange
            var taskDto = new TaskDto { Title = "Test Task", CreatedById = Guid.NewGuid() };
            _mockTaskService.Setup(s => s.CreateAsync(taskDto)).ReturnsAsync((TaskDto?)null);

            // Act
            var result = await _controller.Create(taskDto);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        [Theory]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public async Task GetById_WithExtremeValues_ShouldHandleGracefully(int extremeId)
        {
            // Arrange
            _mockTaskService.Setup(s => s.GetByIdAsync(extremeId)).ReturnsAsync((TaskDto?)null);

            // Act
            var result = await _controller.GetById(extremeId);

            // Assert
            if (extremeId <= 0)
            {
                Assert.IsType<BadRequestObjectResult>(result);
            }
            else
            {
                Assert.IsType<NotFoundObjectResult>(result);
            }
        }

        [Fact]
        public async Task GetAll_WhenServiceThrowsUnauthorizedException_ShouldReturnInternalServerError()
        {
            // Arrange
            _mockTaskService.Setup(s => s.GetAllAsync()).ThrowsAsync(new UnauthorizedAccessException("Access denied"));

            // Act
            var result = await _controller.GetAll();

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusResult.StatusCode);
        }
    }
}
