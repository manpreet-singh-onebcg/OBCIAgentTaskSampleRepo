using AgenticTaskManager.Domain.Entities;
using Xunit;

namespace AgenticTaskManager.Domain.Tests.Entities
{
    public class ActorTests
    {
        [Fact]
        public void Actor_Creation_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var id = 1;
            var name = "John Doe";
            var email = "john.doe@example.com";
            var role = "Developer";

            // Act
            var actor = new Actor
            {
                Id = id,
                Name = name,
                Email = email,
                Role = role,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Assert
            Assert.Equal(id, actor.Id);
            Assert.Equal(name, actor.Name);
            Assert.Equal(email, actor.Email);
            Assert.Equal(role, actor.Role);
            Assert.True(actor.CreatedAt <= DateTime.UtcNow);
            Assert.True(actor.UpdatedAt <= DateTime.UtcNow);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Actor_WithEmptyName_ShouldAllowEmptyOrNullName(string name)
        {
            // Arrange & Act
            var actor = new Actor { Name = name };

            // Assert
            Assert.Equal(name, actor.Name);
        }

        [Theory]
        [InlineData("test@example.com")]
        [InlineData("user.name@domain.co.uk")]
        [InlineData("admin@test.org")]
        public void Actor_WithValidEmail_ShouldSetEmailCorrectly(string email)
        {
            // Arrange & Act
            var actor = new Actor { Email = email };

            // Assert
            Assert.Equal(email, actor.Email);
        }

        [Fact]
        public void Actor_UpdateRole_ShouldChangeRole()
        {
            // Arrange
            var actor = new Actor { Role = "Developer" };

            // Act
            actor.Role = "Manager";
            actor.UpdatedAt = DateTime.UtcNow;

            // Assert
            Assert.Equal("Manager", actor.Role);
        }

        [Fact]
        public void Actor_ToString_ShouldReturnNameAndEmail()
        {
            // Arrange
            var actor = new Actor 
            { 
                Name = "John Doe", 
                Email = "john@example.com" 
            };

            // Act
            var result = $"{actor.Name} ({actor.Email})";

            // Assert
            Assert.Equal("John Doe (john@example.com)", result);
        }
    }
}
