using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WatchWishlistSale.Activities;
using WatchWishlistSale.Models.Notification;
using WatchWishlistSale.Models.Wishlist;

namespace WatchWishlistSale.Tests;

public class SendDiscordNotificationTests
{
    private static SendDiscordNotification CreateActivity(string? webhookUrl)
    {
        Dictionary<string, string?> settings = [];
        if (webhookUrl is not null)
        {
            settings["DISCORD_WEBHOOK_URL"] = webhookUrl;
        }

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        StubHttpMessageHandler handler = new(HttpStatusCode.NoContent);
        return new SendDiscordNotification(new StubHttpClientFactory(handler), configuration, NullLogger<SendDiscordNotification>.Instance);
    }

    private static List<SaleNotification> OneNotification() =>
    [
        new(
            new AppDetails
            {
                SteamAppId = 1,
                Name = "x",
                PriceOverview = new PriceOverview { Initial = 100000, Final = 50000, DiscountPercent = 50 },
            },
            null),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task MissingWebhookThrows(string? webhookSetting)
    {
        SendDiscordNotification activity = CreateActivity(webhookSetting);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => activity.SendDiscordNotificationActivity(OneNotification()));

        Assert.Equal("DISCORD_WEBHOOK_URL is not configured", exception.Message);
    }

    [Fact]
    public async Task EmptyNotificationsWithoutWebhookDoNotThrow()
    {
        SendDiscordNotification activity = CreateActivity(null);

        await activity.SendDiscordNotificationActivity([]);
    }
}
