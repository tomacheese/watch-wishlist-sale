using System.Text.Json.Serialization;

namespace WatchWishlistSale.Models.Notification;

/// <summary>
/// Pair of a notified app ID and the price (yen) at notification time.
/// </summary>
/// <param name="AppId">Steam app ID.</param>
/// <param name="Price">Price (yen) at notification time.</param>
public record NotifiedEntry(
    [property: JsonPropertyName("appId")] long AppId,
    [property: JsonPropertyName("price")] decimal Price);

/// <summary>
/// State snapshot of NotificationStateEntity.
/// </summary>
/// <param name="IsFirstRun">Whether this is the first call to the entity.</param>
/// <param name="NotifiedPrices">Map of app ID to the last notified price (yen).</param>
public record NotificationSnapshot(
    [property: JsonPropertyName("isFirstRun")] bool IsFirstRun,
    [property: JsonPropertyName("notifiedPrices")] Dictionary<long, decimal> NotifiedPrices);

/// <summary>
/// Internal state held by NotificationStateEntity.
/// </summary>
public class NotificationState
{
    /// <summary>
    /// Whether the entity has ever been operated on. Used for first-run detection.
    /// </summary>
    public bool Initialized { get; set; }

    /// <summary>Map of app ID to the last notified price (yen).</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<long, decimal> NotifiedPrices { get; } = [];
}
