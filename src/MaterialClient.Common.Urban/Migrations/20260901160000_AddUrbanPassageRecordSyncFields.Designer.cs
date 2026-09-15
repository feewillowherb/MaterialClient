using MaterialClient.Common.Urban.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Urban.Migrations;

[DbContext(typeof(UrbanDbContext))]
[Migration("20260901160000_AddUrbanPassageRecordSyncFields")]
partial class AddUrbanPassageRecordSyncFields
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        UrbanDbContextModelSnapshot.BuildUrbanModel(modelBuilder);
    }
}
