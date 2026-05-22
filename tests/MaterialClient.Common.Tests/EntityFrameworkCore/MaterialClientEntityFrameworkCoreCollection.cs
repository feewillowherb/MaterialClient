using MaterialClient.Common;
using Xunit;

namespace MaterialClient.Common.EntityFrameworkCore;

[CollectionDefinition(MaterialClientTestConsts.CollectionDefinitionName)]
public class MaterialClientEntityFrameworkCoreCollection : ICollectionFixture<MaterialClientEntityFrameworkCoreFixture>
{

}

