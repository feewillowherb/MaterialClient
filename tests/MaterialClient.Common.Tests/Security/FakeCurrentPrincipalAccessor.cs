using System.Security.Claims;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;

namespace MaterialClient.Common.Security;

[Dependency(ReplaceServices = true)]
public class FakeCurrentPrincipalAccessor : ThreadCurrentPrincipalAccessor
{
    public static Guid UserId { get; set; } = Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d");


    protected override ClaimsPrincipal GetClaimsPrincipal()
    {
        return GetPrincipal();
    }

    private ClaimsPrincipal GetPrincipal()
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
        {
            new Claim(AbpClaimTypes.UserId, UserId.ToString()),
            new Claim(AbpClaimTypes.UserName, "admin"),
            new Claim(AbpClaimTypes.Email, "admin@abp.io")
        }));
    }
}

