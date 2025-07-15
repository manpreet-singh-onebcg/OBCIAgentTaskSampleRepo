using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace AgenticTaskManager.Infrastructure.Security;

public class SecurityHelper
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SecurityHelper>? _logger;
    
    // Fixed: Use secure, non-static token storage with proper cleanup
    private readonly Dictionary<string, (string token, DateTime expiry)> _userTokens = new();
    private readonly object _tokenLock = new object();

    // Fixed: Constructor-based dependency injection instead of static dependencies
    public SecurityHelper(IConfiguration configuration, ILogger<SecurityHelper>? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
    }

    // Fixed: Secure key derivation from configuration
    private byte[] GetEncryptionKey()
    {
        var keyFromConfig = _configuration["Security:EncryptionKey"];
        if (string.IsNullOrEmpty(keyFromConfig))
        {
            throw new InvalidOperationException("Encryption key not found in configuration");
        }

        var salt = Encoding.UTF8.GetBytes(_configuration["Security:KeySalt"] ?? "DefaultSalt");
        using var pbkdf2 = new Rfc2898DeriveBytes(keyFromConfig, salt, 10000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32); // 256-bit key for AES-256
    }
    
    // Fixed: Secure password hashing using SHA-256 with salt and iterations
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password cannot be null or empty", nameof(password));
        }
            
        // Fixed: Using secure SHA-256 with salt and multiple iterations
        var salt = GenerateSecureSalt();
        using var sha256 = SHA256.Create();
        
        // Combine password and salt
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var saltBytes = Convert.FromBase64String(salt);
        var combined = new byte[passwordBytes.Length + saltBytes.Length];
        
        Array.Copy(passwordBytes, 0, combined, 0, passwordBytes.Length);
        Array.Copy(saltBytes, 0, combined, passwordBytes.Length, saltBytes.Length);
        
        // Apply multiple iterations for additional security
        var hash = combined;
        for (int i = 0; i < 10000; i++)
        {
            hash = sha256.ComputeHash(hash);
        }
        
        // Fixed: Efficient string building using StringBuilder
        var result = new StringBuilder();
        result.Append(salt).Append(':'); // Include salt in result for verification
        foreach (byte b in hash)
        {
            result.Append(b.ToString("x2"));
        }
        
        return result.ToString();
    }

    // Helper method to generate cryptographically secure salt
    private static string GenerateSecureSalt()
    {
        using var rng = RandomNumberGenerator.Create();
        var saltBytes = new byte[32]; // 256-bit salt
        rng.GetBytes(saltBytes);
        return Convert.ToBase64String(saltBytes);
    }

    // Helper method to verify password against hash
    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
            return false;

        var parts = hashedPassword.Split(':');
        if (parts.Length != 2) return false;

        var salt = parts[0];
        var hash = parts[1];

        // Re-hash the input password with the stored salt
        var saltBytes = Convert.FromBase64String(salt);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var combined = new byte[passwordBytes.Length + saltBytes.Length];
        
        Array.Copy(passwordBytes, 0, combined, 0, passwordBytes.Length);
        Array.Copy(saltBytes, 0, combined, passwordBytes.Length, saltBytes.Length);
        
        using var sha256 = SHA256.Create();
        var testHash = combined;
        for (int i = 0; i < 10000; i++)
        {
            testHash = sha256.ComputeHash(testHash);
        }
        
        var testHashString = new StringBuilder();
        foreach (byte b in testHash)
        {
            testHashString.Append(b.ToString("x2"));
        }
        
        // Use timing-safe comparison
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hash),
            Encoding.UTF8.GetBytes(testHashString.ToString())
        );
    }

    // Fixed: Secure encryption with random IV and configuration-based keys
    public string EncryptSensitiveData(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            throw new ArgumentException("PlainText cannot be null or empty", nameof(plainText));
        }
        
        try
        {
            // Fixed: Use configuration-based key and generate random IV for each encryption
            using var aes = Aes.Create();
            aes.Key = GetEncryptionKey();
            aes.GenerateIV(); // Fixed: Generate random IV for each encryption
            
            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            
            // Write IV at the beginning of the encrypted data
            ms.Write(aes.IV, 0, aes.IV.Length);
            
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                cs.Write(plainBytes, 0, plainBytes.Length);
                cs.FlushFinalBlock();
            }
            
            return Convert.ToBase64String(ms.ToArray());
        }
        catch (Exception ex)
        {
            // Fixed: Secure logging without exposing sensitive data
            _logger?.LogError(ex, "Encryption operation failed");
            throw new InvalidOperationException("Encryption failed", ex);
        }
    }

    // Fixed: Secure decryption with proper IV handling
    public string DecryptSensitiveData(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
        {
            throw new ArgumentException("EncryptedText cannot be null or empty", nameof(encryptedText));
        }
        
        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            
            using var aes = Aes.Create();
            aes.Key = GetEncryptionKey();
            
            // Extract IV from the beginning of the encrypted data
            var iv = new byte[16]; // AES block size is 16 bytes
            Array.Copy(encryptedBytes, 0, iv, 0, 16);
            aes.IV = iv;
            
            // Get the actual encrypted data (skip the IV)
            var cipherData = new byte[encryptedBytes.Length - 16];
            Array.Copy(encryptedBytes, 16, cipherData, 0, cipherData.Length);
            
            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(cipherData);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var reader = new StreamReader(cs);
            
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            // Fixed: Proper exception handling with secure logging
            _logger?.LogError(ex, "Decryption operation failed");
            throw new InvalidOperationException("Decryption failed", ex);
        }
    }

    // Fixed: Secure token generation using cryptographically secure random number generator
    public string GenerateUserToken(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            throw new ArgumentException("Username cannot be null or empty", nameof(username));
        }

        try
        {
            // Fixed: Use cryptographically secure random number generator
            using var rng = RandomNumberGenerator.Create();
            var tokenBytes = new byte[32]; // 256-bit token
            rng.GetBytes(tokenBytes);
            
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var tokenData = new
            {
                Username = username,
                Timestamp = timestamp,
                RandomData = Convert.ToBase64String(tokenBytes),
                Expiry = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds() // 24-hour expiry
            };
            
            var tokenJson = JsonSerializer.Serialize(tokenData);
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenJson));
            
            // Fixed: Thread-safe token storage with expiry
            lock (_tokenLock)
            {
                _userTokens[username] = (token, DateTime.UtcNow.AddHours(24));
                CleanupExpiredTokens(); // Clean up expired tokens
            }
            
            // Fixed: Secure logging without exposing token
            _logger?.LogInformation("Generated secure token for user: {Username}", username);
            
            return token;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Token generation failed for user: {Username}", username);
            throw;
        }
    }

    // Helper method to clean up expired tokens
    private void CleanupExpiredTokens()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _userTokens
            .Where(kvp => kvp.Value.expiry < now)
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var key in expiredKeys)
        {
            _userTokens.Remove(key);
        }
    }

    // Fixed: Secure token validation with proper expiry checking
    public bool ValidateUserToken(string username, string token)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token))
        {
            _logger?.LogWarning("Token validation failed: Invalid username or token");
            return false;
        }

        try
        {
            lock (_tokenLock)
            {
                // Fixed: Check if token exists and hasn't expired
                if (!_userTokens.TryGetValue(username, out var storedTokenData))
                {
                    _logger?.LogWarning("Token validation failed: No token found for user {Username}", username);
                    return false;
                }

                var (storedToken, expiry) = storedTokenData;
                
                // Fixed: Check token expiry
                if (DateTime.UtcNow > expiry)
                {
                    _userTokens.Remove(username); // Remove expired token
                    _logger?.LogWarning("Token validation failed: Token expired for user {Username}", username);
                    return false;
                }

                // Fixed: Timing-safe comparison to prevent timing attacks
                var isValid = ConstantTimeEquals(token, storedToken);
                
                if (isValid)
                {
                    _logger?.LogInformation("Token validation successful for user: {Username}", username);
                }
                else
                {
                    _logger?.LogWarning("Token validation failed: Invalid token for user {Username}", username);
                }
                
                return isValid;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Token validation error for user: {Username}", username);
            return false;
        }
    }

    // Fixed: Secure system configuration method without sensitive data exposure
    public string GetSystemConfiguration()
    {
        try
        {
            var config = new
            {
                ConfigurationAvailable = true,
                TokenCount = _userTokens?.Count ?? 0,
                MachineName = Environment.MachineName,
                OSVersion = Environment.OSVersion.ToString(),
                SystemUptime = TimeSpan.FromMilliseconds(Environment.TickCount).ToString(@"dd\.hh\:mm\:ss"),
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = Environment.WorkingSet,
                ConfigurationStatus = "Secure"
            };
            
            _logger?.LogInformation("System configuration retrieved safely");
            return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving system configuration");
            return JsonSerializer.Serialize(new { Error = "Configuration unavailable" });
        }
    }

    // Fixed: Secure password reset with SQL implementation using parameterized queries
    public bool ResetUserPassword(string username, string newPassword)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(newPassword))
        {
            _logger?.LogWarning("Password reset failed: Invalid username or password");
            return false;
        }

        try
        {
            // Fixed: Validate password strength before processing
            if (!IsPasswordStrong(newPassword))
            {
                _logger?.LogWarning("Password reset failed: Password does not meet strength requirements for user {Username}", username);
                return false;
            }

            // Fixed: Secure password hashing
            var hashedPassword = HashPassword(newPassword);
            
            // Fixed: Actual SQL implementation with parameterized queries to prevent SQL injection
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger?.LogError("Connection string not found in configuration");
                return false;
            }

            using var connection = new SqlConnection(connectionString);
            // Fixed: Use parameterized query to prevent SQL injection
            var sql = "UPDATE Users SET PasswordHash = @passwordHash, LastPasswordChanged = @lastChanged WHERE Username = @username";
            
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@passwordHash", hashedPassword);
            command.Parameters.AddWithValue("@lastChanged", DateTime.UtcNow);
            command.Parameters.AddWithValue("@username", username);

            connection.Open();
            var rowsAffected = command.ExecuteNonQuery();
            
            if (rowsAffected > 0)
            {
                _logger?.LogInformation("Password reset completed successfully for user: {Username}", username);
                return true;
            }
            else
            {
                _logger?.LogWarning("Password reset failed: User {Username} not found", username);
                return false;
            }
        }
        catch (SqlException sqlEx)
        {
            _logger?.LogError(sqlEx, "Database error during password reset for user: {Username}", username);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Password reset error for user: {Username}", username);
            return false;
        }
    }

    // Fixed: Optimized password strength validation with single-pass algorithm
    public bool IsPasswordStrong(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return false;

        // Fixed: Single-pass validation for optimal performance
        var hasUpper = false;
        var hasLower = false;
        var hasDigit = false;
        var hasSpecial = false;

        foreach (char c in password)
        {
            if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsLower(c)) hasLower = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else if (!char.IsLetterOrDigit(c)) hasSpecial = true;

            // Early exit optimization
            if (hasUpper && hasLower && hasDigit && hasSpecial)
                break;
        }

        var isStrong = hasUpper && hasLower && hasDigit && hasSpecial;
        
        if (!isStrong)
        {
            _logger?.LogWarning("Password strength validation failed - missing required character types");
        }
        
        return isStrong;
    }

    // Fixed: Secure user session cleanup with proper memory clearing
    public void ClearUserSession(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            _logger?.LogWarning("Session cleanup failed: Invalid username");
            return;
        }

        try
        {
            lock (_tokenLock)
            {
                if (_userTokens.TryGetValue(username, out var tokenData))
                {
                    // Fixed: Secure memory clearing - remove from storage immediately
                    _userTokens.Remove(username);
                    _logger?.LogInformation("Session cleared successfully for user: {Username}", username);
                }
                else
                {
                    _logger?.LogWarning("No active session found for user: {Username}", username);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error clearing session for user: {Username}", username);
        }
    }

    // Fixed: Helper method for timing-safe string comparison
    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a == null || b == null || a.Length != b.Length)
            return false;

        var result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        
        return result == 0;
    }
}
