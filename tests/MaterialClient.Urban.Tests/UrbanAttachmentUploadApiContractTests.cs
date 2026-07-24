using System.Reflection;
using System.Text.Json;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Urban.Api;
using MaterialClient.Urban.Dtos;
using Refit;
using Shouldly;
using Xunit;

namespace MaterialClient.Urban.Tests;

public class UrbanAttachmentUploadApiContractTests
{
    [Fact]
    public void Multipart_method_has_no_forced_json_content_type()
    {
        var method = typeof(IUrbanManagementApi).GetMethod(nameof(IUrbanManagementApi.UploadAttachmentsMultipartAsync));
        method.ShouldNotBeNull();

        var headers = method!.GetCustomAttributes<HeadersAttribute>().ToList();
        headers.ShouldNotContain(h =>
            h.Headers.Any(header =>
                header.Contains("application/json", StringComparison.OrdinalIgnoreCase)));

        var interfaceHeaders = typeof(IUrbanManagementApi).GetCustomAttributes<HeadersAttribute>().ToList();
        interfaceHeaders.ShouldBeEmpty();
    }

    [Fact]
    public void Legacy_base64_upload_method_still_present_with_json_header()
    {
        var method = typeof(IUrbanManagementApi).GetMethod(nameof(IUrbanManagementApi.UploadAttachmentsAsync));
        method.ShouldNotBeNull();

        var headers = method!.GetCustomAttributes<HeadersAttribute>().ToList();
        headers.ShouldContain(h =>
            h.Headers.Any(header =>
                header.Contains("application/json", StringComparison.OrdinalIgnoreCase)));

        var post = method.GetCustomAttribute<PostAttribute>();
        post.ShouldNotBeNull();
        post!.Path.ShouldBe("/api/app/urban-attachment/upload");
    }

    [Fact]
    public void Base64_request_dto_still_serializes_attach_type_as_number()
    {
        var dto = new UrbanAttachmentUploadRequestDto
        {
            BuildLicenseNo = "BL001",
            AttachType = AttachType.UrbanPhoto,
            Images = ["abc"]
        };

        var json = JsonSerializer.Serialize(dto);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("attachType").GetInt32().ShouldBe(6);
        doc.RootElement.GetProperty("images")[0].GetString().ShouldBe("abc");
    }
}
