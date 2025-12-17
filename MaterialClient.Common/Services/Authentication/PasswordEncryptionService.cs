using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Services.Authentication;

/// <summary>
/// 密码加密服务接口
/// </summary>
public interface IPasswordEncryptionService
{
    /// <summary>
    /// 加密密码（AES-256-CBC）
    /// </summary>
    /// <param name="plainText">明文密码</param>
    /// <returns>加密后的Base64编码字符串（包含IV）</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// 解密密码
    /// </summary>
    /// <param name="cipherText">加密的Base64编码字符串</param>
    /// <returns>明文密码</returns>
    string Decrypt(string cipherText);
}

/// <summary>
/// 密码加密服务实现（AES-256-CBC）
/// </summary>
public class PasswordEncryptionService : IPasswordEncryptionService
{
    private readonly byte[] _key;
    private readonly ILogger<PasswordEncryptionService> _logger;

    public PasswordEncryptionService(
        IConfiguration configuration,
        ILogger<PasswordEncryptionService> logger)
    {
        _logger = logger;

        var keyString = configuration["Encryption:AesKey"];

        if (string.IsNullOrEmpty(keyString))
        {
            throw new InvalidOperationException(
                "Encryption:AesKey is not configured in appsettings.json");
        }

        try
        {
            _key = Convert.FromBase64String(keyString);

            if (_key.Length != 32)
            {
                throw new ArgumentException(
                    $"AES key must be 256 bits (32 bytes), but got {_key.Length} bytes");
            }

            _logger.LogInformation("Password encryption service initialized successfully");
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Encryption:AesKey must be a valid Base64 string", ex);
        }
    }

    /// <summary>
    /// 加密密码（使用随机IV确保每次加密结果不同）
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            throw new ArgumentException("Plain text cannot be null or empty", nameof(plainText));
        }

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV(); // 生成随机IV

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // 将IV和密文组合：[IV][CipherText]
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        var encryptedText = Convert.ToBase64String(result);

        _logger.LogDebug("Password encrypted successfully (length: {Length})", encryptedText.Length);

        return encryptedText;
    }

    /// <summary>
    /// 解密密码
    /// </summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            throw new ArgumentException("Cipher text cannot be null or empty", nameof(cipherText));
        }

        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _key;

            // 提取IV（前16字节）
            var iv = new byte[aes.IV.Length];
            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            aes.IV = iv;

            // 提取密文（剩余字节）
            var cipher = new byte[fullCipher.Length - iv.Length];
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

            var plainText = Encoding.UTF8.GetString(plainBytes);

            _logger.LogDebug("Password decrypted successfully");

            return plainText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt password");
            throw new InvalidOperationException("Failed to decrypt password", ex);
        }
    }
}

