namespace IntegrationTests.Setup;

public sealed class RealJwtWebApplicationFactory : CustomWebApplicationFactory
{
    public RealJwtWebApplicationFactory()
        : base(useTestAuthentication: false)
    {
    }
}