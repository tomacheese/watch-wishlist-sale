namespace WatchWishlistSale.Models.Notification;

/// <summary>
/// 通知済みのアプリ ID と、その時点の通知価格 (円) の組
/// </summary>
/// <param name="appId">Steam アプリ ID</param>
/// <param name="price">通知時点の価格 (円)</param>
public record NotifiedEntry(long appId, decimal price);

/// <summary>
/// NotificationStateEntity の状態スナップショット
/// </summary>
/// <param name="isFirstRun">エンティティが今回が初回呼び出しであるかどうか</param>
/// <param name="notifiedPrices">アプリ ID と、最後に通知した価格 (円) のマップ</param>
public record NotificationSnapshot(bool isFirstRun, Dictionary<long, decimal> notifiedPrices);

/// <summary>
/// NotificationStateEntity が保持する内部状態
/// </summary>
public class NotificationState
{
    /// <summary>
    /// このエンティティが一度でも操作されたことがあるかどうか。
    /// 旧実装の「notified.json が存在するか」(初回起動判定) に相当する。
    /// </summary>
    public bool Initialized { get; set; }

    /// <summary>アプリ ID と、最後に通知した価格 (円) のマップ (旧実装の notified.json に相当)</summary>
    public Dictionary<long, decimal> NotifiedPrices { get; } = [];
}
