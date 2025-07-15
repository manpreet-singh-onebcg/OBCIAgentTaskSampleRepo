using System.Security.Cryptography;
using System.Text;

namespace AgenticTaskManager.Infrastructure.Utilities;

/// <summary>
/// Utility class for generating cryptographically secure keys and tokens.
/// Use this to generate keys for your configuration instead of hardcoding them.
/// </summary>
public static class KeyGenerator
{
    /// <summary>
    /// Generates a cryptographically secure AES-256 encryption key (32 bytes).
    /// </summary>
    /// <returns>Base64-encoded encryption key</returns>
    public static string GenerateAes256Key()
    {
        using var aes = Aes.Create();
        aes.KeySize = 256; // 256-bit key
        aes.GenerateKey();
        return Convert.ToBase64String(aes.Key);
    }

    /// <summary>
    /// Generates a cryptographically secure JWT signing key.
    /// </summary>
    /// <param name="keyLength">Key length in bytes (minimum 32 recommended)</param>
    /// <returns>Base64-encoded JWT key</returns>
    public static string GenerateJwtKey(int keyLength = 64)
    {
        if (keyLength < 32)
            throw new ArgumentException("Key length must be at least 32 bytes for security", nameof(keyLength));

        byte[] key = new byte[keyLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(key);
        }
        return Convert.ToBase64String(key);
    }

    /// <summary>
    /// Generates a cryptographically secure random password.
    /// </summary>
    /// <param name="length">Password length (minimum 12)</param>
    /// <param name="includeSpecialChars">Whether to include special characters</param>
    /// <returns>Generated password</returns>
    public static string GenerateSecurePassword(int length = 16, bool includeSpecialChars = true)
    {
        if (length < 12)
            throw new ArgumentException("Password length must be at least 12 characters", nameof(length));

        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits = "0123456789";
        const string specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        var allChars = lowercase + uppercase + digits;
        if (includeSpecialChars)
            allChars += specialChars;

        using var rng = RandomNumberGenerator.Create();
        var password = new StringBuilder(length);
        
        // Ensure at least one character from each required category
        password.Append(GetRandomChar(lowercase, rng));
        password.Append(GetRandomChar(uppercase, rng));
        password.Append(GetRandomChar(digits, rng));
        
        if (includeSpecialChars)
            password.Append(GetRandomChar(specialChars, rng));

        // Fill the rest randomly
        var remainingLength = length - password.Length;
        for (int i = 0; i < remainingLength; i++)
        {
            password.Append(GetRandomChar(allChars, rng));
        }

        // Shuffle the password to avoid predictable patterns
        return ShuffleString(password.ToString(), rng);
    }

    /// <summary>
    /// Generates a cryptographically secure API key.
    /// </summary>
    /// <param name="prefix">Optional prefix for the API key</param>
    /// <param name="keyLength">Key length in bytes</param>
    /// <returns>Generated API key</returns>
    public static string GenerateApiKey(string prefix = "ak", int keyLength = 32)
    {
        byte[] key = new byte[keyLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(key);
        }
        
        var keyString = Convert.ToBase64String(key)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");

        return string.IsNullOrEmpty(prefix) ? keyString : $"{prefix}_{keyString}";
    }

    private static char GetRandomChar(string chars, RandomNumberGenerator rng)
    {
        byte[] randomBytes = new byte[4];
        rng.GetBytes(randomBytes);
        int randomIndex = Math.Abs(BitConverter.ToInt32(randomBytes, 0)) % chars.Length;
        return chars[randomIndex];
    }

    private static string ShuffleString(string input, RandomNumberGenerator rng)
    {
        var array = input.ToCharArray();
        
        for (int i = array.Length - 1; i > 0; i--)
        {
            byte[] randomBytes = new byte[4];
            rng.GetBytes(randomBytes);
            int j = Math.Abs(BitConverter.ToInt32(randomBytes, 0)) % (i + 1);
            
            // Swap
            (array[i], array[j]) = (array[j], array[i]);
        }
        
        return new string(array);
    }
}

/// <summary>
/// Console application helper to generate keys for configuration.
/// Run this to generate secure keys for your environment.
/// </summary>
public static class KeyGeneratorConsole
{
    public static void PrintSecureKeys()
    {
        Console.WriteLine("=== SECURE KEY GENERATOR ===");
        Console.WriteLine();
        
        Console.WriteLine("AES-256 Encryption Key:");
        Console.WriteLine($"Security:EncryptionKey = {KeyGenerator.GenerateAes256Key()}");
        Console.WriteLine();
        
        Console.WriteLine("JWT Signing Key:");
        Console.WriteLine($"Security:JwtSecretKey = {KeyGenerator.GenerateJwtKey()}");
        Console.WriteLine();
        
        Console.WriteLine("API Keys:");
        Console.WriteLine($"ApiKeys:ExternalService = {KeyGenerator.GenerateApiKey("ext")}");
        Console.WriteLine($"ApiKeys:BackupService = {KeyGenerator.GenerateApiKey("bak")}");
        Console.WriteLine();
        
        Console.WriteLine("Secure Admin Password:");
        Console.WriteLine($"Admin Password: {KeyGenerator.GenerateSecurePassword(16)}");
        Console.WriteLine("(Remember to hash this password before storing)");
        Console.WriteLine();
        
        Console.WriteLine("=== USER SECRETS COMMANDS ===");
        Console.WriteLine("Run these commands in your API project directory:");
        Console.WriteLine();
        Console.WriteLine($"dotnet user-secrets set \"Security:EncryptionKey\" \"{KeyGenerator.GenerateAes256Key()}\"");
        Console.WriteLine($"dotnet user-secrets set \"Security:JwtSecretKey\" \"{KeyGenerator.GenerateJwtKey()}\"");
        Console.WriteLine($"dotnet user-secrets set \"ApiKeys:ExternalService\" \"{KeyGenerator.GenerateApiKey("ext")}\"");
        Console.WriteLine($"dotnet user-secrets set \"ApiKeys:BackupService\" \"{KeyGenerator.GenerateApiKey("bak")}\"");
        Console.WriteLine();
        Console.WriteLine("WARNING: These keys are displayed once. Store them securely!");
    }
}