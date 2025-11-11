using MaterialClient.Common.Services.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests;

/// <summary>
/// 密码加密服务单元测试
/// </summary>
public class PasswordEncryptionServiceTests
{
    private readonly IPasswordEncryptionService _encryptionService;
    // 32-byte key encoded as Base64 (for AES-256)
    private const string TestKey = "MTIzNDU2Nzg5MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTI=";

    public PasswordEncryptionServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Encryption:AesKey", TestKey }
            })
            .Build();

        var logger = Substitute.For<ILogger<PasswordEncryptionService>>();
        _encryptionService = new PasswordEncryptionService(configuration, logger);
    }

    [Fact]
    public void Encrypt_ShouldReturnNonEmptyString()
    {
        // Arrange
        var plainText = "test-password-123";

        // Act
        var encrypted = _encryptionService.Encrypt(plainText);

        // Assert
        encrypted.ShouldNotBeNullOrEmpty();
        encrypted.ShouldNotBe(plainText);
    }

    [Fact]
    public void Encrypt_ShouldReturnBase64String()
    {
        // Arrange
        var plainText = "test-password-123";

        // Act
        var encrypted = _encryptionService.Encrypt(plainText);

        // Assert - should be valid Base64
        var exception = Record.Exception(() => Convert.FromBase64String(encrypted));
        exception.ShouldBeNull();
    }

    [Fact]
    public void Decrypt_ShouldReturnOriginalPlainText()
    {
        // Arrange
        var plainText = "test-password-123";

        // Act
        var encrypted = _encryptionService.Encrypt(plainText);
        var decrypted = _encryptionService.Decrypt(encrypted);

        // Assert
        decrypted.ShouldBe(plainText);
    }

    [Fact]
    public void Encrypt_SameInputShouldProduceDifferentOutput()
    {
        // Arrange
        var plainText = "test-password-123";

        // Act
        var encrypted1 = _encryptionService.Encrypt(plainText);
        var encrypted2 = _encryptionService.Encrypt(plainText);

        // Assert - non-deterministic encryption (random IV for security)
        encrypted1.ShouldNotBe(encrypted2);
        
        // But both should decrypt to the same plaintext
        _encryptionService.Decrypt(encrypted1).ShouldBe(plainText);
        _encryptionService.Decrypt(encrypted2).ShouldBe(plainText);
    }

    [Fact]
    public void Encrypt_EmptyString_ShouldThrowException()
    {
        // Arrange
        var plainText = string.Empty;

        // Act & Assert
        Should.Throw<ArgumentException>(() => _encryptionService.Encrypt(plainText));
    }

    [Fact]
    public void Decrypt_EmptyString_ShouldThrowException()
    {
        // Arrange
        var cipherText = string.Empty;

        // Act & Assert
        Should.Throw<ArgumentException>(() => _encryptionService.Decrypt(cipherText));
    }

    [Fact]
    public void Encrypt_LongPassword_ShouldWork()
    {
        // Arrange
        var plainText = new string('a', 1000); // 1000 character password

        // Act
        var encrypted = _encryptionService.Encrypt(plainText);
        var decrypted = _encryptionService.Decrypt(encrypted);

        // Assert
        decrypted.ShouldBe(plainText);
    }

    [Fact]
    public void Encrypt_SpecialCharacters_ShouldWork()
    {
        // Arrange
        var plainText = "!@#$%^&*()_+-=[]{}|;':\"<>?,./`~密码测试";

        // Act
        var encrypted = _encryptionService.Encrypt(plainText);
        var decrypted = _encryptionService.Decrypt(encrypted);

        // Assert
        decrypted.ShouldBe(plainText);
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenKeyNotConfigured()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>())
            .Build();

        var logger = Substitute.For<ILogger<PasswordEncryptionService>>();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => new PasswordEncryptionService(configuration, logger));
    }

    [Fact]
    public void Decrypt_InvalidCipherText_ShouldThrowException()
    {
        // Arrange
        var invalidCipherText = "this-is-not-valid-base64-encrypted-data";

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => _encryptionService.Decrypt(invalidCipherText));
    }
}

