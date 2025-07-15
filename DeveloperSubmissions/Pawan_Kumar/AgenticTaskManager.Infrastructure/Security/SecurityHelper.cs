using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgenticTaskManager.Infrastructure.Security;

// SQ: Class implementing security but with multiple vulnerabilities
public class SecurityHelper
{
    // Fixed: Proper 32-byte encryption key for AES-256
    private static readonly byte[] EncryptionKey = Encoding.UTF8.GetBytes("MyHardcodedKey123456789012345678"); // Exactly 32 bytes for AES-256
    private static readonly byte[] InitializationVector = Encoding.UTF8.GetBytes("MyHardcodedIV12"); // Must be 16 bytes
    
    // SQ: Static mutable collection storing sensitive data
    public static Dictionary<string, string> UserTokens = new Dictionary<string, string>();
    
    // Improved hash algorithm with salt
    public static string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));
            
        // Using BCrypt or PBKDF2 would be better, but for this example, using SHA256 with salt
        using (var sha256 = SHA256.Create())
        {
            // Generate a random salt
            var salt = GenerateRandomSalt();
            var saltedPassword = Encoding.UTF8.GetBytes(password + Convert.ToBase64String(salt));
            var hashedBytes = sha256.ComputeHash(saltedPassword);
            
            // Combine salt and hash
            var result = new byte[salt.Length + hashedBytes.Length];
            Array.Copy(salt, 0, result, 0, salt.Length);
            Array.Copy(hashedBytes, 0, result, salt.Length, hashedBytes.Length);
            
            return Convert.ToBase64String(result);
        }
    }
    
    private static byte[] GenerateRandomSalt()
    {
        var salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }
        return salt;
    }

    // Fixed encryption method with proper error handling and security
    public static string EncryptSensitiveData(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentException("Plain text cannot be null or empty", nameof(plainText));
        
        try
        {
            using (Aes aes = Aes.Create())
            {
                // Use proper key size validation
                if (EncryptionKey.Length != 32) // 256 bits
                    throw new InvalidOperationException("Encryption key must be 32 bytes for AES-256");
                
                aes.Key = EncryptionKey;
                aes.GenerateIV(); // Generate random IV for each encryption
                
                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    // Prepend IV to encrypted data
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        var plainBytes = Encoding.UTF8.GetBytes(plainText);
                        cs.Write(plainBytes, 0, plainBytes.Length);
                        cs.FlushFinalBlock();
                    }
                    
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }
        catch (Exception ex)
        {
            // Log error without sensitive details
            Console.WriteLine($"Encryption failed for data: {plainText.Substring(0, Math.Min(3, plainText.Length))}..., Error: {ex.Message}");
            throw new InvalidOperationException("Encryption failed", ex);
        }
    }

    // Fixed decryption method with proper error handling and security
    public static string DecryptSensitiveData(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            throw new ArgumentException("Encrypted text cannot be null or empty", nameof(encryptedText));
        
        try
        {
            var encryptedData = Convert.FromBase64String(encryptedText);
            
            using (Aes aes = Aes.Create())
            {
                // Use proper key size validation
                if (EncryptionKey.Length != 32) // 256 bits
                    throw new InvalidOperationException("Encryption key must be 32 bytes for AES-256");
                
                aes.Key = EncryptionKey;
                
                // Extract IV from the beginning of encrypted data
                var iv = new byte[16]; // AES block size
                var cipherText = new byte[encryptedData.Length - 16];
                
                Array.Copy(encryptedData, 0, iv, 0, 16);
                Array.Copy(encryptedData, 16, cipherText, 0, cipherText.Length);
                
                aes.IV = iv;
                
                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(cipherText))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var reader = new StreamReader(cs))
                {
                    return reader.ReadToEnd();
                }
            }
        }
        catch (Exception ex)
        {
            // Proper error handling with security considerations
            throw new InvalidOperationException("Decryption failed", ex);
        }
    }

    // SQ: Insecure token generation
    public static string GenerateUserToken(string username)
    {
        // SQ: Weak token generation using predictable data
        var timestamp = DateTime.Now.Ticks.ToString();
        var userHash = HashPassword(username); // Using weak MD5 hash
        var tokenData = $"{username}:{timestamp}:{userHash}";
        
        // SQ: Storing token in static collection - memory leak and security risk
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenData));
        UserTokens[username] = token;
        
        // SQ: Logging sensitive token information
        Console.WriteLine($"Generated token for user {username}: {token}");
        
        return token;
    }

    // SQ: Insecure token validation
    public static bool ValidateUserToken(string username, string token)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token))
            return false;
            
        // SQ: Direct comparison without timing attack protection
        if (UserTokens.ContainsKey(username))
        {
            var storedToken = UserTokens[username];
            return storedToken == token; // SQ: Vulnerable to timing attacks
        }
        
        return false;
    }

    // SQ: Method that exposes sensitive information
    public static string GetSystemConfiguration()
    {
        var config = new
        {
            EncryptionKeyLength = EncryptionKey.Length,
            EncryptionKey = Convert.ToBase64String(EncryptionKey), // SQ: Exposing encryption key!
            IV = Convert.ToBase64String(InitializationVector), // SQ: Exposing IV!
            ActiveTokens = UserTokens.Count,
            TokenDetails = UserTokens, // SQ: Exposing all user tokens!
            MachineName = Environment.MachineName,
            UserDomain = Environment.UserDomainName,
            OSVersion = Environment.OSVersion
        };
        
        return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    }

    // SQ: Method with SQL injection vulnerability for password reset
    public static bool ResetUserPassword(string username, string newPassword)
    {
        // SQ: Simulated SQL injection vulnerability
        var sql = $"UPDATE Users SET Password = '{HashPassword(newPassword)}' WHERE Username = '{username}'";
        
        // SQ: Logging SQL with sensitive data
        Console.WriteLine($"Executing password reset SQL: {sql}");
        
        // SQ: No validation of password strength
        return true; // Simulated success
    }

    // Performance: Inefficient password validation
    public static bool IsPasswordStrong(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        
        // Performance: Multiple string operations instead of single pass
        var hasUpper = false;
        var hasLower = false;
        var hasDigit = false;
        var hasSpecial = false;
        
        // Performance: Converting to char array multiple times
        foreach (char c in password.ToCharArray())
        {
            if (char.IsUpper(c)) hasUpper = true;
        }
        
        foreach (char c in password.ToCharArray())
        {
            if (char.IsLower(c)) hasLower = true;
        }
        
        foreach (char c in password.ToCharArray())
        {
            if (char.IsDigit(c)) hasDigit = true;
        }
        
        foreach (char c in password.ToCharArray())
        {
            if (!char.IsLetterOrDigit(c)) hasSpecial = true;
        }
        
        return password.Length >= 8 && hasUpper && hasLower && hasDigit && hasSpecial;
    }

    // SQ: Method that doesn't properly handle sensitive data cleanup
    public static void ClearUserSession(string username)
    {
        if (UserTokens.ContainsKey(username))
        {
            var token = UserTokens[username];
            // SQ: Not securely clearing sensitive data from memory
            UserTokens.Remove(username);
            
            // SQ: Logging sensitive cleanup operation
            Console.WriteLine($"Cleared session for user {username}, token was: {token}");
        }
    }

    // SQ: Dead code with security implications
    private static string GenerateBackdoorAccess()
    {
        // SQ: Backdoor functionality - major security risk
        return "ADMIN_BACKDOOR_" + DateTime.Now.ToString("yyyyMMdd");
    }

    // Secure method for input sanitization
    public static string SanitizeInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Remove potentially harmful HTML/script tags
        var sanitized = input;
        
        // Remove script tags
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"<script[^>]*>.*?</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        // Remove javascript: protocol
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"javascript:", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        // Remove event handlers
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"on\w+\s*=", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        // Remove img tags with onerror
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"<img[^>]*onerror[^>]*>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return sanitized;
    }

    // Secure token generation
    public static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes);
    }

    // Secure password verification with timing attack protection
    public static bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
            throw new ArgumentException("Password and hash cannot be null or empty");

        try
        {
            // Extract salt and hash from stored password
            var storedBytes = Convert.FromBase64String(hashedPassword);
            var salt = new byte[16];
            var storedHash = new byte[storedBytes.Length - 16];
            
            Array.Copy(storedBytes, 0, salt, 0, 16);
            Array.Copy(storedBytes, 16, storedHash, 0, storedHash.Length);
            
            // Hash the input password with the extracted salt
            using (var sha256 = SHA256.Create())
            {
                var saltedPassword = Encoding.UTF8.GetBytes(password + Convert.ToBase64String(salt));
                var inputHash = sha256.ComputeHash(saltedPassword);
                
                // Use a constant-time comparison to prevent timing attacks
                return CryptographicEquals(storedHash, inputHash);
            }
        }
        catch
        {
            return false;
        }
    }

    // Constant-time string comparison to prevent timing attacks
    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
    }

    // Constant-time byte array comparison to prevent timing attacks
    private static bool CryptographicEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
    }

    // File path validation
    public static bool IsValidFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        // Check for path traversal attempts
        var pathTraversalPatterns = new[]
        {
            "..",
            "%2e%2e",
            "..\\",
            "../",
            "....//",
            "....\\\\",
            "%2f",
            "%5c"
        };

        var normalizedPath = filePath.ToLowerInvariant();
        return !pathTraversalPatterns.Any(pattern => normalizedPath.Contains(pattern));
    }
}
