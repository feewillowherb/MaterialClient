using MaterialClient.Common.Services.Authentication;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests;

/// <summary>
/// 密码加密服务单元测试
/// </summary>
public class PasswordEncryptionServiceTests
{
    private readonly IPasswordEncryptionService _encryptionService;
    private const string TestKey = "TestKeyForUnitTests1234567890123=";

    public PasswordEncryptionServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Encryption:AesKey", TestKey }
            })
            .Build();

        _encryptionService = new PasswordEncryptionService(configuration);
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
    public void Encrypt_SameInputShouldProduceSameOutput()
    {
        // Arrange
        var plainText = "test-password-123";

        // Act
        var encrypted1 = _encryptionService.Encrypt(plainText);
        var encrypted2 = _encryptionService.Encrypt(plainText);

        // Assert - deterministic encryption (same IV)
        encrypted1.ShouldBe(encrypted2);
    }

    [Fact]
    public void Encrypt_EmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        var plainText = string.Empty;

        // Act
        var encrypted = _encryptionService.Encrypt(plainText);

        // Assert
        encrypted.ShouldBe(string.Empty);
    }

    [Fact]
    public void Decrypt_EmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        var cipherText = string.Empty;

        // Act
        var decrypted = _encryptionService.Decrypt(cipherText);

        // Assert
        decrypted.ShouldBe(string.Empty);
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

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new PasswordEncryptionService(configuration));
    }

    [Fact]
    public void Decrypt_InvalidCipherText_ShouldThrowException()
    {
        // Arrange
        var invalidCipherText = "this-is-not-valid-base64-encrypted-data";

        // Act & Assert
        Should.Throw<FormatException>(() => _encryptionService.Decrypt(invalidCipherText));
    }
}

