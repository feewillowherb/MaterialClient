using System.Text.Json;
using MaterialClient.Common.Configuration;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class SystemSettingsChunkedUploadTests
{
    [Fact]
    public void Missing_EnableChunkedAttachmentUpload_deserializes_as_false()
    {
        const string json = """{"EnableAutoStart":true}""";
        var settings = JsonSerializer.Deserialize<SystemSettings>(json);
        settings.ShouldNotBeNull();
        settings!.EnableChunkedAttachmentUpload.ShouldBeFalse();
        settings.EnableAutoStart.ShouldBeTrue();
    }

    [Fact]
    public void EnableChunkedAttachmentUpload_roundtrips()
    {
        var settings = new SystemSettings { EnableChunkedAttachmentUpload = true };
        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<SystemSettings>(json);
        restored!.EnableChunkedAttachmentUpload.ShouldBeTrue();
    }
}
