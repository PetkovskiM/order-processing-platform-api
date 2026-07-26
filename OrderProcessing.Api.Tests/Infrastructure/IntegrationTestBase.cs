namespace OrderProcessing.Api.Tests.Infrastructure;

public abstract class IntegrationTestBase : IDisposable
{
    protected CustomWebApplicationFactory Factory { get; }

    protected HttpClient Client { get; }

    protected IntegrationTestBase()
    {
        Factory = new CustomWebApplicationFactory();
        Client = Factory.CreateClient();
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();

        GC.SuppressFinalize(this);
    }
}