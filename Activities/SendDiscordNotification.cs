using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WatchWishlistSale.Common;
using WatchWishlistSale.Models.Notification;
using WatchWishlistSale.Models.Wishlist;

namespace WatchWishlistSale.Activities;

/// <summary>
/// セール情報を Discord Webhook 経由で通知する Activity
/// </summary>
public class SendDiscordNotification(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SendDiscordNotification> logger)
{
    /// <summary>Discord の embed.fields に設定できる件数の上限</summary>
    private const int EmbedFieldLimit = 25;

    /// <summary>通知 embed の色 (オレンジ)</summary>
    private const int EmbedColor = 0xff_80_00;

    [Function(FunctionNames.SendDiscordNotificationActivity)]
    public async Task SendDiscordNotificationActivity([ActivityTrigger] List<SaleNotification> notifications)
    {
        if (notifications.Count == 0)
        {
            logger.LogInformation("No sale notifications to send");
            return;
        }

        string? webhookUrl = configuration["DISCORD_WEBHOOK_URL"];
        if (string.IsNullOrEmpty(webhookUrl))
        {
            logger.LogWarning("⚠️ DISCORD_WEBHOOK_URL is not configured. Skipping Discord notification.");
            return;
        }

        HttpClient client = httpClientFactory.CreateClient(nameof(SendDiscordNotification));

        // Discord の embed.fields は 25 件までという制限があるため、チャンク分割して送信する
        foreach (SaleNotification[] chunk in notifications.Chunk(EmbedFieldLimit))
        {
            DiscordEmbed embed = new()
            {
                Title = "Steam Sale Alert",
                Fields = chunk.Select(BuildField).ToList(),
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                Color = EmbedColor,
            };
            DiscordWebhookPayload payload = new() { Embeds = [embed] };

            using StringContent content = new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client.PostAsync(webhookUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Failed to send Discord notification: {(int)response.StatusCode} {response.ReasonPhrase} ({body})");
            }

            logger.LogInformation("🔔 Sent Discord notification for {count} apps", chunk.Length);
        }
    }

    /// <summary>1 アプリ分の通知情報から Discord embed のフィールドを組み立てる</summary>
    private static DiscordEmbedField BuildField(SaleNotification notification)
    {
        AppDetails app = notification.App;
        PriceOverview price = app.PriceOverview!;
        decimal initialPrice = price.Initial / 100m;
        decimal currentPrice = price.Final / 100m;
        string lowestPriceText = notification.LowestPrice is { } lowest
          ? $"{lowest.Price}{lowest.Currency}"
          : "不明";

        string urlSteamDb = $"https://steamdb.info/app/{app.SteamAppId}/";
        string urlSteam = $"https://store.steampowered.com/app/{app.SteamAppId}/";

        return new DiscordEmbedField
        {
            Name = $"{app.Name} -{price.DiscountPercent}%",
            Value = $"{initialPrice}円 -> {currentPrice}円[{price.Currency}] (最安値: {lowestPriceText})\n{urlSteamDb}\n{urlSteam}",
            Inline = true,
        };
    }
}
