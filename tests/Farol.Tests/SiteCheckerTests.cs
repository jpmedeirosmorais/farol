using System.Net;
using Farol.Web.Data;
using Farol.Web.Services;

namespace Farol.Tests;

public class SiteCheckerTests
{
    // http:// de propósito: com https o leitor de certificado tentaria
    // uma conexão real. Com http ele retorna cedo e o teste fica offline.
    private static readonly Site TestSite = new()
    {
        Id = 1,
        Name = "Test site",
        Url = "http://test.local"
    };

    private static SiteChecker CreateChecker(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond,
        TimeSpan? timeout = null)
    {
        var handler = new StubHttpMessageHandler(respond);
        var client = new HttpClient(handler)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(15)
        };
        return new SiteChecker(client);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(204, true)]
    [InlineData(301, true)]
    [InlineData(400, false)]
    [InlineData(404, false)]
    [InlineData(500, false)]
    [InlineData(503, false)]
    public async Task CheckAsync_SetsIsUpFromStatusCode(int statusCode, bool expectedIsUp)
    {
        var checker = CreateChecker((_, _) =>
            Task.FromResult(new HttpResponseMessage((HttpStatusCode)statusCode)));

        var result = await checker.CheckAsync(TestSite);

        Assert.Equal(statusCode, result.StatusCode);
        Assert.Equal(expectedIsUp, result.IsUp);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task CheckAsync_WhenDnsFails_RecordsDnsMessage()
    {
        var checker = CreateChecker((_, _) => throw new HttpRequestException(
            HttpRequestError.NameResolutionError, "no such host"));

        var result = await checker.CheckAsync(TestSite);

        Assert.False(result.IsUp);
        Assert.Null(result.StatusCode);
        Assert.Equal("DNS did not resolve", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckAsync_WhenConnectionFails_RecordsConnectionMessage()
    {
        var checker = CreateChecker((_, _) => throw new HttpRequestException(
            HttpRequestError.ConnectionError, "refused"));

        var result = await checker.CheckAsync(TestSite);

        Assert.False(result.IsUp);
        Assert.Equal("Connection refused or unreachable", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckAsync_WhenTlsFails_RecordsTlsMessage()
    {
        var checker = CreateChecker((_, _) => throw new HttpRequestException(
            HttpRequestError.SecureConnectionError, "bad certificate"));

        var result = await checker.CheckAsync(TestSite);

        Assert.False(result.IsUp);
        Assert.Equal("TLS handshake failed", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckAsync_WhenRequestTimesOut_RecordsTimeout()
    {
        var checker = CreateChecker(
            async (_, token) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            },
            timeout: TimeSpan.FromMilliseconds(100));

        var result = await checker.CheckAsync(TestSite);

        Assert.False(result.IsUp);
        Assert.Equal("Timed out", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckAsync_AlwaysRecordsSiteAndTimestamp()
    {
        var before = DateTimeOffset.UtcNow;

        var checker = CreateChecker((_, _) => throw new HttpRequestException(
            HttpRequestError.ConnectionError, "refused"));

        var result = await checker.CheckAsync(TestSite);

        Assert.Equal(TestSite.Id, result.SiteId);
        Assert.InRange(result.CheckedAt, before, DateTimeOffset.UtcNow);
    }
}
