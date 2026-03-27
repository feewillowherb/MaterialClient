using System.ComponentModel;
using System.Linq;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Configuration;

public sealed class GateConfigurationValidationResult
{
    public bool IsValid { get; init; }
    public string? Reason { get; init; }
    public int CountA { get; init; }
    public int CountB { get; init; }
    public List<string> DevicesA { get; init; } = new();
    public List<string> DevicesB { get; init; } = new();
}

public static class GateConfigurationValidation
{
    public static GateConfigurationValidationResult Validate(IEnumerable<LicensePlateRecognitionConfig> configs)
    {
        var enabledConfigs = configs
            .Where(c => c.EnableGateIo)
            .ToList();

        var countA = enabledConfigs.Count(c => c.Direction == LicensePlateDirection.A);
        var countB = enabledConfigs.Count(c => c.Direction == LicensePlateDirection.B);
        var devicesA = enabledConfigs.Where(c => c.Direction == LicensePlateDirection.A).Select(c => c.Name).ToList();
        var devicesB = enabledConfigs.Where(c => c.Direction == LicensePlateDirection.B).Select(c => c.Name).ToList();

        if (countA == 0 && countB == 0)
        {
            return new GateConfigurationValidationResult
            {
                IsValid = true,
                CountA = 0,
                CountB = 0
            };
        }

        if (countA == 1 && countB == 1)
        {
            return new GateConfigurationValidationResult
            {
                IsValid = true,
                CountA = countA,
                CountB = countB,
                DevicesA = devicesA,
                DevicesB = devicesB
            };
        }

        var sideAText = GetDirectionDescription(LicensePlateDirection.A);
        var sideBText = GetDirectionDescription(LicensePlateDirection.B);
        var reason = countA == 0 ? $"缺少{sideAText}侧道闸配置" :
            countB == 0 ? $"缺少{sideBText}侧道闸配置" :
            countA > 1 ? $"{sideAText}侧道闸配置过多（{countA}个），期望恰好1个" :
            $"{sideBText}侧道闸配置过多（{countB}个），期望恰好1个";

        return new GateConfigurationValidationResult
        {
            IsValid = false,
            Reason = reason,
            CountA = countA,
            CountB = countB,
            DevicesA = devicesA,
            DevicesB = devicesB
        };
    }

    private static string GetDirectionDescription(LicensePlateDirection direction)
    {
        var fieldInfo = direction.GetType().GetField(direction.ToString());
        var attribute = fieldInfo?.GetCustomAttributes(typeof(DescriptionAttribute), false)
            .FirstOrDefault() as DescriptionAttribute;
        return attribute?.Description ?? direction.ToString();
    }
}
