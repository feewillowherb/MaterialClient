using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <summary>
    /// Empty kernel sync: product tables stay in existing SQLite files; they are no longer in the kernel model.
    /// Do not DROP UrbanWeighingExtensions / RecycleWaybillExtensions from here.
    /// </summary>
    public partial class DetachProductTablesFromKernel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
