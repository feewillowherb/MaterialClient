using MaterialClient.Common.Urban.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Urban.Migrations;

[DbContext(typeof(UrbanDbContext))]
[Migration("20260828040000_AddUrbanPassageRecords")]
partial class AddUrbanPassageRecords
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        UrbanDbContextModelSnapshot.BuildUrbanModel(modelBuilder);
    }
}
