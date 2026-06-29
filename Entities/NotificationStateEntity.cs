using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;
using WatchWishlistSale.Common;
using WatchWishlistSale.Models.Notification;

namespace WatchWishlistSale.Entities;

/// <summary>
/// 通知済みのセール情報を保持する Durable Entity (旧実装の data/notified.json の代替)。
/// プロフィール ID をエンティティキーとして利用する。
/// </summary>
public class NotificationStateEntity : TaskEntity<NotificationState>
{
    /// <summary>状態スナップショットを取得する操作名</summary>
    public const string OperationGetSnapshot = nameof(GetSnapshot);

    /// <summary>通知済みとして記録する操作名</summary>
    public const string OperationSetNotified = nameof(SetNotified);

    /// <summary>通知記録を削除する操作名</summary>
    public const string OperationRemoveNotified = nameof(RemoveNotified);

    /// <summary>
    /// 現在の通知状態のスナップショットを取得する。
    /// 初回呼び出し時 (これまで一度も操作されていない場合) は IsFirstRun が true となり、
    /// 以後の呼び出しではこのエンティティが「初期化済み」として記録される。
    /// </summary>
    /// <returns>通知状態のスナップショット</returns>
    public Task<NotificationSnapshot> GetSnapshot()
    {
        var isFirstRun = !State.Initialized;
        State.Initialized = true;
        return Task.FromResult(new NotificationSnapshot(isFirstRun, new Dictionary<long, decimal>(State.NotifiedPrices)));
    }

    /// <summary>
    /// 指定したアプリを、指定した価格で通知済みとして記録する
    /// </summary>
    /// <param name="entry">通知済みエントリ (アプリ ID と価格)</param>
    public void SetNotified(NotifiedEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        State.NotifiedPrices[entry.appId] = entry.price;
    }

    /// <summary>
    /// 指定したアプリの通知済み記録を削除する (セールが終了し対象から外れた場合に利用)
    /// </summary>
    /// <param name="appId">Steam アプリ ID</param>
    public void RemoveNotified(long appId) => State.NotifiedPrices.Remove(appId);

    /// <summary>
    /// エンティティの状態が未生成の場合の初期値を返す
    /// </summary>
    /// <param name="entityOperation">エンティティに対して実行される操作の情報</param>
    /// <returns>初期状態の <see cref="NotificationState"/></returns>
    protected override NotificationState InitializeState(TaskEntityOperation entityOperation) => new();

    /// <summary>
    /// Durable Entity のエントリーポイント。受信した操作をエンティティクラスにディスパッチする。
    /// </summary>
    /// <param name="dispatcher">エンティティへの操作をディスパッチするオブジェクト</param>
    /// <returns>ディスパッチ完了を表す <see cref="Task"/></returns>
    [Function(FunctionNames.NotificationStateEntity)]
    public static Task DispatchAsync([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        return dispatcher.DispatchAsync<NotificationStateEntity>();
    }
}
