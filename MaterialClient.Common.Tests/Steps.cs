using Reqnroll;
using Shouldly;
using MaterialClient.Common.Security;
using Volo.Abp.Domain.Repositories;

namespace MaterialClient.Common;

[Binding]
public sealed partial class Steps : MaterialClientDomainTestBase<MaterialClientDomainTestModule>
{
    private DateTime _now;

    private Exception? _exception;

    private bool _isThrow = true;

    // TODO: Add your test manager or service references here
    // Example:
    // private TestManager M => GetRequiredService<TestManager>();

    [Given(@"Now is (.*)")]
    public void GivenNowIs(DateTime p0)
    {
        _now = p0;
    }

    [Given(@"Current user is ""(.*)""")]
    public void GivenCurrentUserIs(string p0)
    {
        FakeCurrentPrincipalAccessor.UserId = TestData.UserIdDict[p0];
    }

    [Given("Throwing an exception")]
    public void GivenThrowingAnException()
    {
        _isThrow = true;
    }

    [Given("Not throwing an exception")]
    public void GivenNotThrowingAnException()
    {
        _isThrow = false;
    }

    [Then("Exception is {string}")]
    public void ThenExceptionIs(string exceptionMessage)
    {
        _exception.ShouldNotBeNull();
        // TODO: Add specific exception type checking based on your domain
        // _exception.ShouldBeOfType<UserFriendlyException>();
        // var domainException = (UserFriendlyException)_exception;
        // domainException.Message.ShouldBe(exceptionMessage);
    }
}

