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

