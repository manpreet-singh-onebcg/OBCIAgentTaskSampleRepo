using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace TestUtilities;

/// <summary>
/// Helper class for creating test HTTP contexts and controller contexts
/// </summary>
public static class HttpContextTestHelper
{
    public static ControllerContext CreateControllerContext(string? userId = null, string? apiKey = null, bool isValid = true)
    {
        var httpContext = new DefaultHttpContext();
        
        // Add user claims if userId provided
        if (!string.IsNullOrEmpty(userId))
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, $"User_{userId}")
            };
            
            if (isValid)
            {
                claims.Add(new Claim("role", "ValidUser"));
            }
            
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        }
        
        // Add API key to headers if provided
        if (!string.IsNullOrEmpty(apiKey))
        {
            httpContext.Request.Headers["X-API-Key"] = apiKey;
        }
        
        return new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public static IFormFile CreateMockFormFile(string fileName, string content, string contentType = "text/plain")
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(content);
        writer.Flush();
        stream.Position = 0;

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(stream.Length);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
               .Returns((Stream target, CancellationToken token) => stream.CopyToAsync(target, token));

        return mockFile.Object;
    }

    public static IFormFile CreateMaliciousFormFile(string fileName = "malicious.exe", int sizeInMB = 20)
    {
        var maliciousContent = new byte[sizeInMB * 1024 * 1024]; // Large file
        var stream = new MemoryStream(maliciousContent);

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(stream.Length);
        mockFile.Setup(f => f.ContentType).Returns("application/octet-stream");
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        return mockFile.Object;
    }
}
