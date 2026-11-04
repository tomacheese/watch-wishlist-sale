namespace WatchWishlistSale.Models.Notification;

/// <summary>
/// Pair of a notified app ID and the price (yen) at notification time.
/// </summary>
/// <param name="appId">Steam app ID.</param>
/// <param name="price">Price (yen) at notification time.</param>
public record NotifiedEntry(long appId, decimal price);

/// <summary>
/// State snapshot of NotificationStateEntity.
/// </summary>
/// <param name="isFirstRun">Whether this is the first call to the entity.</param>
/// <param name="notifiedPrices">Map of app ID to the last notified price (yen).</param>
public record NotificationSnapshot(bool isFirstRun, Dictionary<long, decimal> notifiedPrices);

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
    public Dictionary<long, decimal> NotifiedPrices { get; } = [];
}
