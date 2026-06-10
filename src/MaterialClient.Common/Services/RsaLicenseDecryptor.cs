using System.Security.Cryptography;
using System.Xml;

namespace MaterialClient.Common.Services;

/// <summary>
///     RSA 解密授权结果
/// </summary>
/// <param name="AuthEndTime">授权过期时间</param>
/// <param name="BuildLicenseNo">施工许可证号（从 xmlString 节点解密）</param>
/// <param name="ProId">项目ID（从 proId 节点解密后解析为 Guid）</param>
/// <param name="IsExpired">授权是否已过期</param>
/// <param name="DaysRemaining">剩余天数（负数表示已过期天数）</param>
public record RsaLicenseDecryptResult(
    DateTime AuthEndTime,
    string BuildLicenseNo,
    Guid ProId,
    bool IsExpired,
    int DaysRemaining);

/// <summary>
///     RSA XML 授权文件解密工具类
///     从 RSA.xml 文件中读取加密数据并使用 RSA 私钥解密验证
/// </summary>
public static class RsaLicenseDecryptor
{
    /// <summary>
    ///     使用 RSA 私钥（XML 格式）解密 Base64 编码的密文
    /// </summary>
    /// <param name="privateKeyXml">RSA 私钥 XML 字符串（RSAKeyValue 格式）</param>
    /// <param name="encryptedBase64">Base64 编码的密文</param>
    /// <returns>UTF-8 解码后的明文字符串</returns>
    public static string Decrypt(string privateKeyXml, string encryptedBase64)
    {
        using var rsa = RSA.Create();
        rsa.FromXmlString(privateKeyXml);

        var encryptedBytes = Convert.FromBase64String(encryptedBase64);
        var decryptedBytes = rsa.Decrypt(encryptedBytes, RSAEncryptionPadding.Pkcs1);

        return System.Text.Encoding.UTF8.GetString(decryptedBytes);
    }

    /// <summary>
    ///     从 RSA.xml 文件中读取并解密所有授权数据
    /// </summary>
    /// <param name="xmlFilePath">RSA.xml 文件路径</param>
    /// <returns>包含所有解密数据的授权结果</returns>
    public static RsaLicenseDecryptResult ReadAndDecrypt(string xmlFilePath)
    {
        var doc = new XmlDocument();
        doc.Load(xmlFilePath);

        var privateKeyNode = doc.SelectSingleNode("/config/privateKey")
            ?? throw new InvalidDataException("RSA.xml 中缺少 privateKey 节点");
        var authEndTimeNode = doc.SelectSingleNode("/config/authEndTime")
            ?? throw new InvalidDataException("RSA.xml 中缺少 authEndTime 节点");
        var xmlStringNode = doc.SelectSingleNode("/config/xmlString")
            ?? throw new InvalidDataException("RSA.xml 中缺少 xmlString 节点");
        var proIdNode = doc.SelectSingleNode("/config/proId")
            ?? throw new InvalidDataException("RSA.xml 中缺少 proId 节点");

        var privateKey = privateKeyNode.InnerText?.Trim()
            ?? throw new InvalidDataException("RSA.xml 中 privateKey 节点内容为空");
        var encryptedAuthEndTime = authEndTimeNode.InnerText?.Trim()
            ?? throw new InvalidDataException("RSA.xml 中 authEndTime 节点内容为空");
        var encryptedXmlString = xmlStringNode.InnerText?.Trim()
            ?? throw new InvalidDataException("RSA.xml 中 xmlString 节点内容为空");
        var encryptedProId = proIdNode.InnerText?.Trim()
            ?? throw new InvalidDataException("RSA.xml 中 proId 节点内容为空");

        if (string.IsNullOrEmpty(privateKey))
            throw new InvalidDataException("RSA.xml 中 privateKey 节点内容为空");
        if (string.IsNullOrEmpty(encryptedAuthEndTime))
            throw new InvalidDataException("RSA.xml 中 authEndTime 节点内容为空");
        if (string.IsNullOrEmpty(encryptedXmlString))
            throw new InvalidDataException("RSA.xml 中 xmlString 节点内容为空");
        if (string.IsNullOrEmpty(encryptedProId))
            throw new InvalidDataException("RSA.xml 中 proId 节点内容为空");

        // 解密所有加密字段
        var authEndTimeStr = Decrypt(privateKey, encryptedAuthEndTime);
        var buildLicenseNo = Decrypt(privateKey, encryptedXmlString);
        var proIdStr = Decrypt(privateKey, encryptedProId);

        // 解析授权过期时间
        var authEndTime = DateTime.Parse(authEndTimeStr);

        // 解析项目ID
        var proId = Guid.Parse(proIdStr);

        // 计算是否过期和剩余天数
        var now = DateTime.Now;
        var daysRemaining = (authEndTime - now).Days;
        var isExpired = authEndTime.Date < now.Date;

        return new RsaLicenseDecryptResult(
            AuthEndTime: authEndTime,
            BuildLicenseNo: buildLicenseNo,
            ProId: proId,
            IsExpired: isExpired,
            DaysRemaining: daysRemaining);
    }
}
