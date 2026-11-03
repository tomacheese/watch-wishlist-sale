using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;
using WatchWishlistSale.Common;
using WatchWishlistSale.Models.Notification;

namespace WatchWishlistSale.Entities;

/// <summary>
/// Durable Entity that holds the sales already notified.
/// The Steam profile ID is used as the entity key.
/// </summary>
public class NotificationStateEntity : TaskEntity<NotificationState>
{
    /// <summary>Operation name for getting the state snapshot.</summary>
    public const string OperationGetSnapshot = nameof(GetSnapshot);

    /// <summary>Operation name for recording an app as notified.</summary>
    public const string OperationSetNotified = nameof(SetNotified);

    /// <summary>Operation name for removing a notification record.</summary>
    public const string OperationRemoveNotified = nameof(RemoveNotified);

    /// <summary>
    /// Gets a snapshot of the current notification state.
    /// On the first call (the entity has never been operated on) <c>isFirstRun</c> is true,
    /// and the entity is marked as initialized for subsequent calls.
    /// </summary>
    /// <returns>The notification state snapshot.</returns>
    public Task<NotificationSnapshot> GetSnapshot()
    {
        var isFirstRun = !State.Initialized;
        State.Initialized = true;
        return Task.FromResult(new NotificationSnapshot(isFirstRun, new Dictionary<long, decimal>(State.NotifiedPrices)));
    }

    /// <summary>
    /// Records the given app as notified at the given price.
    /// </summary>
    /// <param name="entry">Notified entry (app ID and price).</param>
    public void SetNotified(NotifiedEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        State.NotifiedPrices[entry.appId] = entry.price;
    }

    /// <summary>
    /// Removes the notified record of the given app (used when its sale has ended).
    /// </summary>
    /// <param name="appId">Steam app ID.</param>
    public void RemoveNotified(long appId) => State.NotifiedPrices.Remove(appId);

    /// <summary>
    /// Returns the initial state used when the entity state has not been created yet.
    /// </summary>
    /// <param name="entityOperation">Information about the operation being run on the entity.</param>
    /// <returns>The initial <see cref="NotificationState"/>.</returns>
    protected override NotificationState InitializeState(TaskEntityOperation entityOperation) => new();

    /// <summary>
    /// Durable Entity entry point. Dispatches the received operation to the entity class.
    /// </summary>
    /// <param name="dispatcher">Dispatcher for operations on the entity.</param>
    /// <returns>A <see cref="Task"/> that completes when dispatching is done.</returns>
    [Function(FunctionNames.NotificationStateEntity)]
    public static Task DispatchAsync([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        return dispatcher.DispatchAsync<NotificationStateEntity>();
    }
}
