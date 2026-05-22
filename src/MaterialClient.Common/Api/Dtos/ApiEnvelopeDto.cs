namespace MaterialClient.Common.Api.Dtos;

public class ApiEnvelopeDto<T>
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string TraceId { get; set; } = string.Empty;

    public T? Data { get; set; }

    public bool IsSuccess => string.Equals(Code, "OK", StringComparison.OrdinalIgnoreCase);
}
