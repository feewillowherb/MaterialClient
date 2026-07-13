using MaterialClient.Common;
using MaterialClient.UI;
using Volo.Abp.Modularity;

namespace MaterialClient.AttendedWeighing;

[DependsOn(typeof(MaterialClientCommonModule), typeof(MaterialClientUiModule))]
public class MaterialClientAttendedWeighingModule : AbpModule;
