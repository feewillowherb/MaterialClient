using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Configuration;

public sealed record UrbanLprCapabilities(bool HasScale, bool HasCheckpoint, bool HasFinishedProduct)
{
    public static UrbanLprCapabilities FromConfigs(IReadOnlyList<LicensePlateRecognitionConfig>? configs)
    {
        if (configs is null || configs.Count == 0)
            return new UrbanLprCapabilities(false, false, false);

        var hasScale = false;
        var hasCheckpoint = false;
        var hasFinishedProduct = false;
        foreach (var config in configs)
        {
            if (!config.IsValid())
                continue;
            switch (config.SiteType)
            {
                case LprSiteType.Scale:
                    hasScale = true;
                    break;
                case LprSiteType.Checkpoint:
                    hasCheckpoint = true;
                    break;
                case LprSiteType.FinishedProduct:
                    hasFinishedProduct = true;
                    break;
            }
        }

        return new UrbanLprCapabilities(hasScale, hasCheckpoint, hasFinishedProduct);
    }
}
