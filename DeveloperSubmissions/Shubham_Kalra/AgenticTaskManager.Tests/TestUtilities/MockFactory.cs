using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace TestUtilities;

/// <summary>
/// Helper class for creating commonly used mocks in tests
/// </summary>
public static class MockFactory
{
    public static Mock<ILogger<T>> CreateMockLogger<T>()
    {
        return new Mock<ILogger<T>>();
    }

    public static Mock<IConfiguration> CreateMockConfiguration(Dictionary<string, string>? settings = null)
    {
        var mockConfig = new Mock<IConfiguration>();
        
        // Default settings
        var defaultSettings = new Dictionary<string, string>
        {
            ["ApiSettings:ApiKey"] = "valid-api-key",
            ["ExternalServices:NotificationUrl"] = "https://api.example.com/notify",
            ["FileUpload:MaxFileSizeMB"] = "10",
            ["FileUpload:Path"] = @"d:\temp\uploads"
        };

        // Merge with custom settings
        if (settings != null)
        {
            foreach (var setting in settings)
            {
                defaultSettings[setting.Key] = setting.Value;
            }
        }

        // Setup configuration indexer
        foreach (var setting in defaultSettings)
        {
            mockConfig.Setup(x => x[setting.Key]).Returns(setting.Value);
        }

        // Setup file upload allowed extensions
        var allowedExtensions = new[] { ".txt", ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(x => x.Get<string[]>()).Returns(allowedExtensions);
        mockConfig.Setup(x => x.GetSection("FileUpload:AllowedExtensions")).Returns(mockSection.Object);

        return mockConfig;
    }

    public static Mock<IHttpClientFactory> CreateMockHttpClientFactory()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        var mockHttpClient = new Mock<HttpClient>();
        
        mockFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(mockHttpClient.Object);
        
        return mockFactory;
    }
}
