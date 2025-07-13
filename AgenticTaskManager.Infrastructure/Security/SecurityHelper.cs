using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgenticTaskManager.Infrastructure.Security;

// SQ: Class implementing security but with multiple vulnerabilities
public class SecurityHelper
{
    // SQ: Hardcoded encryption keys - major security issue
    private static readonly byte[] EncryptionKey = Encoding.UTF8.GetBytes("MyHardcodedKey123"); // Must be 32 bytes for AES-256
    private static readonly byte[] InitializationVector = Encoding.UTF8.GetBytes("MyHardcodedIV12"); // Must be 16 bytes
    
    // SQ: Static mutable collection storing sensitive data
    public static Dictionary<string, string> UserTokens = new Dictionary<string, string>();
    
    // SQ: Weak hash algorithm
    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) // Some validation, but not comprehensive
            return string.Empty;
            
        // SQ: Using obsolete and weak MD5 algorithm
        using (var md5 = MD5.Create())
        {
            var hashedBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(password));
            
            // Performance: Inefficient string building
            var result = "";
            foreach (byte b in hashedBytes)
            {
                result += b.ToString("x2");
            }
            return result;
        }
    }

    // SQ: Encryption method with hardcoded key and weak implementation
    public static string EncryptSensitiveData(string plainText)
    {
        if (plainText == null) return null; // SQ: Returning null instead of throwing exception
        
        try
        {
            // SQ: Using hardcoded key and IV
            using (Aes aes = Aes.Create())
            {
                aes.Key = EncryptionKey;
                aes.IV = InitializationVector; // SQ: Reusing IV - major security flaw
                
                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    var plainBytes = Encoding.UTF8.GetBytes(plainText);
                    cs.Write(plainBytes, 0, plainBytes.Length);
                    cs.FlushFinalBlock();
                    
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }
        catch (Exception ex)
        {
            // SQ: Logging sensitive operation details
            Console.WriteLine($"Encryption failed for data: {plainText.Substring(0, Math.Min(10, plainText.Length))}..., Error: {ex.Message}");
            return plainText; // SQ: Returning plaintext on encryption failure!
        }
    }

    // SQ: Decryption with same security issues
    public static string DecryptSensitiveData(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText)) return "";
        
        try
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = EncryptionKey;
                aes.IV = InitializationVector; // SQ: Same hardcoded IV
                
                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(Convert.FromBase64String(encryptedText)))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var reader = new StreamReader(cs))
                {
                    return reader.ReadToEnd();
                }
            }
        }
        catch
        {
            // SQ: Empty catch block - security issue
            return encryptedText; // SQ: Returning encrypted text on decryption failure
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
}
