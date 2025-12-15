using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Security.Claims;
using Volo.Abp.Users;

namespace MaterialClient.EFCore;

public class MaterialClientDbContextFactory : IDesignTimeDbContextFactory<MaterialClientDbContext>
{
    public MaterialClientDbContext CreateDbContext(string[] args)
    {
        // Use a simple connection string for design-time (migrations don't need actual database)
        // The connection string is only used to build the model, not to connect to a database
        var connectionString = "Data Source=:memory:";

        var optionsBuilder = new DbContextOptionsBuilder<MaterialClientDbContext>();
        optionsBuilder.UseSqlite(connectionString)
            .EnableDetailedErrors() // 启用详细的错误信息
            .EnableSensitiveDataLogging(); // 启用敏感数据日志记录（包含参数值）

        // Create a design-time ICurrentUser implementation
        var designTimeCurrentUser = new DesignTimeCurrentUser();

        return new MaterialClientDbContext(optionsBuilder.Options, designTimeCurrentUser);
    }

    /// <summary>
    /// 设计时的 ICurrentUser 实现（用于 EF Core 迁移等场景）
    /// </summary>
    private class DesignTimeCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => false;
        public Guid? Id => null;
        public string? UserName => null;
        public string? Name => null;
        public string? SurName => null;
        public string? PhoneNumber => null;
        public bool PhoneNumberVerified => false;
        public string? Email => null;
        public bool EmailVerified => false;
        public Guid? TenantId => null;
        public string[] Roles => Array.Empty<string>();

        public Claim? FindClaim(string claimType) => null;
        public Claim[] FindClaims(string claimType) => Array.Empty<Claim>();
        public Claim[] GetAllClaims() => Array.Empty<Claim>();
        public bool IsInRole(string roleName) => false;
    }
}

