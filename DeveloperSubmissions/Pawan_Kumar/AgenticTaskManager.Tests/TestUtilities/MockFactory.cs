using Microsoft.Extensions.Logging;
using Moq;

namespace AgenticTaskManager.TestUtilities
{
    public static class MockFactory
    {
        public static Mock<ILogger<T>> CreateMockLogger<T>()
        {
            return new Mock<ILogger<T>>();
        }

        public static void VerifyLogWasCalled<T>(Mock<ILogger<T>> mockLogger, LogLevel logLevel, string message)
        {
            mockLogger.Verify(
                x => x.Log(
                    logLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(message)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        public static void VerifyLogWasCalled<T>(Mock<ILogger<T>> mockLogger, LogLevel logLevel, Times times)
        {
            mockLogger.Verify(
                x => x.Log(
                    logLevel,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                times);
        }

        public static void VerifyNoLogsWereCalled<T>(Mock<ILogger<T>> mockLogger)
        {
            mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never);
        }

        public static void SetupMockLoggerToThrow<T>(Mock<ILogger<T>> mockLogger, Exception exception)
        {
            mockLogger.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()))
                .Throws(exception);
        }
    }
}
