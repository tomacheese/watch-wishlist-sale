using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using WatchWishlistSale.Models.Notification;
using WatchWishlistSale.Models.Pricing;
using WatchWishlistSale.Models.Wishlist;

namespace WatchWishlistSale.Tests;

public class JsonDeserializationTests
{
    [Fact]
    public async Task WishlistResponsePopulatesItems()
    {
        using StringContent content = new("""{"response":{"items":[{"appid":10},{"appid":20}]}}""");
        var response = await content.ReadFromJsonAsync<WishlistApiResponse>();

        Assert.NotNull(response?.Response);
        Assert.Equal(new long[] { 10, 20 }, response.Response.Items.Select(item => item.AppId));
    }

    [Fact]
    public async Task ItadResponsePopulatesLows()
    {
        using StringContent content = new("""[{"id":"game","lows":[{"shop":{"id":61},"price":{"amount":123.45,"currency":"JPY"}}]}]""");
        var entries = await content.ReadFromJsonAsync<List<ItadStoreLowEntry>>();

        var low = Assert.Single(Assert.Single(Assert.IsType<List<ItadStoreLowEntry>>(entries)).Lows);
        Assert.Equal(61, low.Shop.Id);
        Assert.Equal(123.45m, low.Price.Amount);
        Assert.Equal("JPY", low.Price.Currency);
    }

    [Fact]
    public void NotificationStatePopulatesNotifiedPrices()
    {
        const string json = """{"Initialized":true,"NotifiedPrices":{"10":123.45,"20":67.89}}""";
        var state = JsonSerializer.Deserialize<NotificationState>(json);

        Assert.NotNull(state);
        Assert.True(state.Initialized);
        Assert.Equal(2, state.NotifiedPrices.Count);
        Assert.Equal(123.45m, state.NotifiedPrices[10]);
        Assert.Equal(67.89m, state.NotifiedPrices[20]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DurableRecordsPreserveLegacyJson(bool webDefaults)
    {
        JsonSerializerOptions options = webDefaults ? new(JsonSerializerDefaults.Web) : new();
        AssertLegacyRoundTrip<NotifiedEntry>("""{"appId":10,"price":123.45}""", options);
        AssertLegacyRoundTrip<NotificationSnapshot>("""{"isFirstRun":true,"notifiedPrices":{"10":123.45}}""", options);
        AssertLegacyRoundTrip<LowestPriceResult>("""{"appId":10,"price":123.45,"currency":"JPY","source":"ITAD"}""", options);
        AssertLegacyRoundTrip<SaleNotification>("""{"app":{"type":"game","name":"Example","steam_appid":10,"price_overview":null},"lowestPrice":{"appId":10,"price":123.45,"currency":"JPY","source":"ITAD"}}""", options);
    }

    private static void AssertLegacyRoundTrip<T>(string json, JsonSerializerOptions options)
    {
        T? value = JsonSerializer.Deserialize<T>(json, options);
        Assert.NotNull(value);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonSerializer.SerializeToNode(value, options)));
    }
}
