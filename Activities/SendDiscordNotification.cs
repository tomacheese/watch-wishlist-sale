using System.Diagnostics.CodeAnalysis;
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

    /// <summary>
    /// アクティビティのエントリーポイント。セール通知一覧を Discord Webhook 経由で送信する。
    /// </summary>
    /// <param name="notifications">送信するセール通知の一覧</param>
    /// <returns>送信完了を表す非同期タスク</returns>
    [Function(FunctionNames.SendDiscordNotificationActivity)]
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Azure Functions Durable Activity receives List<T> as parameter via JSON deserialization.")]
    public async Task SendDiscordNotificationActivity([ActivityTrigger] List<SaleNotification> notifications)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        if (notifications.Count == 0)
        {
            logger.LogInformation("No sale notifications to send");
            return;
        }

        var webhookUrl = configuration["DISCORD_WEBHOOK_URL"];
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
                Fields = [.. chunk.Select(BuildField)],
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                Color = EmbedColor,
            };
            DiscordWebhookPayload payload = new() { Embeds = [embed] };

            using StringContent content = new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client.PostAsync(new Uri(webhookUrl), content);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Failed to send Discord notification: {(int)response.StatusCode} {response.ReasonPhrase} ({body})");
            }

            logger.LogInformation("🔔 Sent Discord notification for {Count} apps", chunk.Length);
        }
    }

    /// <summary>1 アプリ分の通知情報から Discord embed のフィールドを組み立てる</summary>
    private static DiscordEmbedField BuildField(SaleNotification notification)
    {
        AppDetails app = notification.app;
        PriceOverview price = app.PriceOverview!;
        var initialPrice = price.Initial / 100m;
        var currentPrice = price.Final / 100m;
        var lowestPriceText = notification.lowestPrice is { } lowest
          ? $"{lowest.price}{lowest.currency}"
          : "不明";

        var urlSteamDb = $"https://steamdb.info/app/{app.SteamAppId}/";
        var urlSteam = $"https://store.steampowered.com/app/{app.SteamAppId}/";

        return new DiscordEmbedField
        {
            Name = $"{app.Name} -{price.DiscountPercent}%",
            Value = $"{initialPrice}円 -> {currentPrice}円[{price.Currency}] (最安値: {lowestPriceText})\n{urlSteamDb}\n{urlSteam}",
            Inline = true,
        };
    }
}
