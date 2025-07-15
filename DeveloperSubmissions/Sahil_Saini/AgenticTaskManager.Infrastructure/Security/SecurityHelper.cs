using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgenticTaskManager.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AgenticTaskManager.Infrastructure.Security;

public class SecurityHelper
{
    private readonly SecurityConfiguration _config;
    private readonly ILogger<SecurityHelper> _logger;
    
    // Thread-safe collection for user tokens with expiration
    private static readonly ConcurrentDictionary<string, (string Token, DateTime Expiration)> _userTokens = new();
    
    // Constants for security
    private const int SALT_SIZE = 32;
    private const int HASH_SIZE = 32;
    private const int ITERATIONS = 100000; // PBKDF2 iterations
    private const int TOKEN_LENGTH = 32;

    public SecurityHelper(SecurityConfiguration config, ILogger<SecurityHelper> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Secure password hashing using PBKDF2
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));
        }

        try
        {
            // Generate a random salt
            byte[] salt = new byte[SALT_SIZE];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Hash the password with PBKDF2
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, ITERATIONS, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(HASH_SIZE);

            // Combine salt and hash
            byte[] hashBytes = new byte[SALT_SIZE + HASH_SIZE];
            Array.Copy(salt, 0, hashBytes, 0, SALT_SIZE);
            Array.Copy(hash, 0, hashBytes, SALT_SIZE, HASH_SIZE);

            return Convert.ToBase64String(hashBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hash password");
            throw new InvalidOperationException("Password hashing failed", ex);
        }
    }

    // Verify password against hash
    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
        {
            return false;
        }

        try
        {
            // Extract the bytes
            byte[] hashBytes = Convert.FromBase64String(hashedPassword);

            // Get the salt
            byte[] salt = new byte[SALT_SIZE];
            Array.Copy(hashBytes, 0, salt, 0, SALT_SIZE);

            // Compute the hash on the password the user entered
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, ITERATIONS, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(HASH_SIZE);

            // Compare the results using constant-time comparison
            return ConstantTimeEquals(hash, hashBytes.Skip(SALT_SIZE).ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Password verification failed");
            return false;
        }
    }

    // Secure encryption with random IV
    public string EncryptSensitiveData(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            throw new ArgumentException("Plain text cannot be null or empty.", nameof(plainText));
        }

        try
        {
            using var aes = Aes.Create();
            aes.Key = _config.GetEncryptionKey();
            aes.GenerateIV(); // Generate random IV for each encryption

            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            
            // Write IV first, then encrypted data
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
            _logger.LogError(ex, "Encryption failed");
            throw new InvalidOperationException("Encryption failed", ex);
        }
    }

    // Secure decryption
    public string DecryptSensitiveData(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
        {
            throw new ArgumentException("Encrypted text cannot be null or empty.", nameof(encryptedText));
        }

        try
        {
            byte[] fullCipher = Convert.FromBase64String(encryptedText);

            using var aes = Aes.Create();
            aes.Key = _config.GetEncryptionKey();

            // Extract IV from the beginning
            byte[] iv = new byte[aes.BlockSize / 8];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var reader = new StreamReader(cs);
            
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decryption failed");
            throw new InvalidOperationException("Decryption failed", ex);
        }
    }

    // Secure token generation
    public string GenerateUserToken(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            throw new ArgumentException("Username cannot be null or empty.", nameof(username));
        }

        try
        {
            // Generate cryptographically secure random token
            byte[] tokenBytes = new byte[TOKEN_LENGTH];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }

            var token = Convert.ToBase64String(tokenBytes);
            var expiration = DateTime.UtcNow.Add(_config.GetJwtExpiration());

            // Store token with expiration (thread-safe)
            _userTokens.TryAdd(username, (token, expiration));

            _logger.LogInformation("Token generated for user: {Username}", username);
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token generation failed for user: {Username}", username);
            throw new InvalidOperationException("Token generation failed", ex);
        }
    }

    // Secure token validation with constant-time comparison
    public bool ValidateUserToken(string username, string token)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token))
        {
            return false;
        }

        try
        {
            if (_userTokens.TryGetValue(username, out var storedTokenInfo))
            {
                // Check if token has expired
                if (DateTime.UtcNow > storedTokenInfo.Expiration)
                {
                    _userTokens.TryRemove(username, out _);
                    return false;
                }

                // Use constant-time comparison to prevent timing attacks
                return ConstantTimeEquals(
                    Encoding.UTF8.GetBytes(token),
                    Encoding.UTF8.GetBytes(storedTokenInfo.Token)
                );
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed for user: {Username}", username);
            return false;
        }
    }

    // Secure password strength validation (single pass)
    public bool IsPasswordStrong(string password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        if (password.Length < 8 || password.Length > 128)
            return false;

        bool hasUpper = false, hasLower = false, hasDigit = false, hasSpecial = false;

        // Single pass through the password
        foreach (char c in password)
        {
            if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsLower(c)) hasLower = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else if (!char.IsLetterOrDigit(c)) hasSpecial = true;

            // Early exit if all criteria met
            if (hasUpper && hasLower && hasDigit && hasSpecial)
                break;
        }

        return hasUpper && hasLower && hasDigit && hasSpecial;
    }

    // Secure session cleanup
    public void ClearUserSession(string username)
    {
        if (string.IsNullOrEmpty(username))
            return;

        try
        {
            if (_userTokens.TryRemove(username, out _))
            {
                _logger.LogInformation("Session cleared for user: {Username}", username);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear session for user: {Username}", username);
        }
    }

    // Clean expired tokens periodically
    public void CleanExpiredTokens()
    {
        try
        {
            var now = DateTime.UtcNow;
            var expiredUsers = _userTokens
                .Where(kvp => now > kvp.Value.Expiration)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var username in expiredUsers)
            {
                _userTokens.TryRemove(username, out _);
            }

            if (expiredUsers.Count > 0)
            {
                _logger.LogInformation("Cleaned {Count} expired tokens", expiredUsers.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean expired tokens");
        }
    }

    // Constant-time comparison to prevent timing attacks
    private static bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;

        int result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }

    // Get non-sensitive system information only
    public string GetSystemStatus()
    {
        try
        {
            var status = new
            {
                ActiveTokensCount = _userTokens.Count,
                ServerTime = DateTime.UtcNow,
                MachineName = Environment.MachineName.Substring(0, Math.Min(5, Environment.MachineName.Length)) + "***", // Partially masked
                OSVersion = Environment.OSVersion.Platform.ToString() // Only platform, not full version
            };

            return JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system status");
            return "{}";
        }
    }
}
