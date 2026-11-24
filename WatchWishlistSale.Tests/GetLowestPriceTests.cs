using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WatchWishlistSale.Activities;

namespace WatchWishlistSale.Tests;

public class GetLowestPriceTests
{
    [Fact]
    public async Task StoreLowRequestBodyIsArrayOfGameIdStrings()
    {
        const string GameId = "018d937f-1b6d-7a5e-8c2e-5b9c1b1c0a11";
        string? storeLowBody = null;
        StubHandler handler = new(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/games/lookup/v1")
            {
                return Json($$$"""{"found":true,"game":{"id":"{{{GameId}}}"}}""");
            }

            storeLowBody = await request.Content!.ReadAsStringAsync();
            return Json($$$"""[{"id":"{{{GameId}}}","lows":[{"shop":{"id":61},"price":{"amount":100,"currency":"JPY"}}]}]""");
        });
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ITAD_API_KEY"] = "test-key" })
            .Build();
        GetLowestPrice activity = new(new StubHttpClientFactory(handler), configuration, NullLogger<GetLowestPrice>.Instance);

        var result = await activity.GetLowestPriceActivity(10);

        Assert.Equal($"[\"{GameId}\"]", storeLowBody);
        Assert.Equal("ITAD", result?.Source);
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
