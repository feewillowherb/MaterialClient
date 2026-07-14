namespace MaterialClient.Common.Services.AttendedWeighing.Records;

/// <summary>
///     本称重周期内的 LPR 图片候选（单槽择优）。
/// </summary>
public sealed record CycleLprCandidate(
    string RelativePath,
    bool HasPlate,
    DateTime ReceivedAt);
