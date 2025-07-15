using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace AgenticTaskManager.Infrastructure.Configuration;

public class SecurityConfiguration
{
    private readonly IConfiguration _configuration;

    public SecurityConfiguration(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    // Encryption settings
    public byte[] GetEncryptionKey()
    {
        var keyString = _configuration["Security:EncryptionKey"];
        if (string.IsNullOrEmpty(keyString))
        {
            throw new InvalidOperationException("Encryption key not configured. Please set Security:EncryptionKey in user secrets or environment variables.");
        }

        return Convert.FromBase64String(keyString);
    }

    public byte[] GenerateRandomIV()
    {
        using var aes = Aes.Create();
        aes.GenerateIV();
        return aes.IV;
    }

    // API settings - Made virtual for testing
    public virtual string GetApiKey(string serviceName)
    {
        var apiKey = _configuration[$"ApiKeys:{serviceName}"];
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException($"API key for service '{serviceName}' not configured.");
        }
        return apiKey;
    }

    // Database settings
    public string GetConnectionString(string name = "DefaultConnection")
    {
        var connectionString = _configuration.GetConnectionString(name);
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{name}' not configured.");
        }
        return connectionString;
    }

    // Admin settings
    public string GetAdminPasswordHash()
    {
        var adminHash = _configuration["Security:AdminPasswordHash"];
        if (string.IsNullOrEmpty(adminHash))
        {
            throw new InvalidOperationException("Admin password hash not configured. Please set Security:AdminPasswordHash in user secrets.");
        }
        return adminHash;
    }

    // JWT settings
    public string GetJwtSecretKey()
    {
        var jwtSecret = _configuration["Security:JwtSecretKey"];
        if (string.IsNullOrEmpty(jwtSecret))
        {
            throw new InvalidOperationException("JWT secret key not configured. Please set Security:JwtSecretKey in user secrets.");
        }
        return jwtSecret;
    }

    public TimeSpan GetJwtExpiration()
    {
        var expirationMinutes = GetValue<int>("Security:JwtExpirationMinutes", 60);
        return TimeSpan.FromMinutes(expirationMinutes);
    }

    // Configuration value helper
    public T GetValue<T>(string key, T defaultValue = default(T))
    {
        try
        {
            var value = _configuration[key];
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
}