# 3. 本プロジェクトのアーキテクチャと処理フロー

ここでは、前章までに学んだ概念が、本プロジェクトの中でどのように組み合わさっているかを俯瞰します。

## 3.1 全体構成図

```
┌─────────────────┐       毎時 0 分に起動
│  TimerTrigger   │ ───────────────────────┐
│   (Crawler)     │                         │
└─────────────────┘                         ▼
                                ┌─────────────────────────┐
                                │   オーケストレーター       │
                                │ (WatchWishlistOrchestrator) │
                                └─────────────────────────┘
                                             │
        ┌───────────────┬────────────────────┼────────────────────┬──────────────────┐
        ▼               ▼                    ▼                    ▼                  ▼
┌───────────────┐ ┌───────────────┐  ┌───────────────┐   ┌───────────────┐  ┌───────────────────┐
│GetWishlist    │ │GetAppDetails  │  │FilterSaleApps │   │GetLowestPrice │  │SendDiscord        │
│AppIds         │ │ (Fan-out)     │  │               │   │ (Fan-out)     │  │Notification       │
│ [Activity]    │ │ [Activity]    │  │ [Activity]    │   │ [Activity]    │  │ [Activity]        │
└───────────────┘ └───────────────┘  └───────────────┘   └───────────────┘  └───────────────────┘
                                             │                                          ▲
                                             ▼                                          │
                                  ┌───────────────────────┐                            │
                                  │ NotificationStateEntity│ ───────────────────────────┘
                                  │     [Entity]           │   (前回の通知状態を取得・更新)
                                  └───────────────────────┘
```

- **四角の実線**: Function (トリガー / オーケストレーター / アクティビティ / エンティティ)
- 矢印: 呼び出し・データの流れ

## 3.2 処理フローの詳細

[`Orchestrations/WatchWishlistOrchestrator.cs`](../Orchestrations/WatchWishlistOrchestrator.cs) に書かれている
処理の流れを、ステップごとに見ていきます (コード中のコメント番号と対応しています)。

### ステップ 1: ウィッシュリストの App ID 一覧を取得

```csharp
List<long> appIds = await context.CallActivityAsync<List<long>>(
    FunctionNames.GetWishlistAppIdsActivity, profileId, ActivityRetryOptions);
```

Steam の Web API (`IWishlistService/GetWishlist/v1`) を呼び出し、
プロフィールに登録されているゲームの App ID 一覧 (`List<long>`) を取得します。

### ステップ 2: 各アプリの詳細情報を並列取得 (Fan-out/Fan-in)

```csharp
List<AppDetails?> appDetailsResults = [];
long[][] appIdChunks = [.. appIds.Chunk(MaxFanOutConcurrency)];
for (int chunkIndex = 0; chunkIndex < appIdChunks.Length; chunkIndex++)
{
    IEnumerable<Task<AppDetails?>> appDetailsTasks = appIdChunks[chunkIndex].Select(appId =>
        context.CallActivityAsync<AppDetails?>(FunctionNames.GetAppDetailsActivity, appId, ActivityRetryOptions));
    appDetailsResults.AddRange(await Task.WhenAll(appDetailsTasks));

    bool isLastChunk = chunkIndex == appIdChunks.Length - 1;
    if (!isLastChunk)
    {
        await context.CreateTimer(context.CurrentUtcDateTime.Add(AppDetailsFanOutInterval), CancellationToken.None);
    }
}
List<AppDetails> appDetailsList = appDetailsResults.OfType<AppDetails>().ToList();
```

App ID ごとに Steam ストア API を呼び出し、価格情報を含む詳細データを取得します。
件数が可変 (ウィッシュリストの登録数次第) であるため、[Fan-out/Fan-in パターン](./02-durable-functions-fundamentals.md#25-fan-outfan-in-パターン)
で並列に取得しています。

[02-durable-functions-fundamentals.md](./02-durable-functions-fundamentals.md#25-fan-outfan-in-パターン) で紹介した
最小限の Fan-out/Fan-in (`appIds.Select(...)` → `Task.WhenAll`) とは異なり、ここでは
**`Chunk(N)` で一定件数ずつに分割し、チャンクの間に `context.CreateTimer` で待機を挟む** という、
一段階発展した形になっています。これは、呼び出し先の Steam ストア API に
「約 200 リクエスト / 5 分」という厳しいレート制限があるためで、
詳しい理由は [05-best-practices.md](./05-best-practices.md#56-fan-out-のレート制御) で解説します。

`OfType<AppDetails>()` によって、取得に失敗した (`null` が返ってきた) ものは自動的に除外されます
(`OfType<T>` は `null` 非許容型へのキャストに失敗する要素を取り除く LINQ メソッドです)。

### ステップ 3: セール中のアプリを抽出

```csharp
List<AppDetails> saleApps = await context.CallActivityAsync<List<AppDetails>>(
    FunctionNames.FilterSaleAppsActivity, appDetailsList);
```

[`FilterSaleApps`](../Activities/FilterSaleApps.cs) は、価格情報があり、かつ割引率が 0 でないアプリだけを残します。
これは外部 I/O を伴わない純粋な絞り込み処理ですが、あえてアクティビティとして実装しています
(理由は [05-best-practices.md](./05-best-practices.md) で説明します)。

### ステップ 4: 前回までの通知状態を取得

```csharp
EntityInstanceId entityId = new(FunctionNames.NotificationStateEntity, profileId);
NotificationSnapshot snapshot = await context.Entities.CallEntityAsync<NotificationSnapshot>(
    entityId, NotificationStateEntity.OperationGetSnapshot);
```

[`NotificationStateEntity`](../Entities/NotificationStateEntity.cs) から、
「前回どのアプリを・いくらで通知したか」のスナップショットを取得します。
このエンティティは旧実装の `data/notified.json` の役割を Durable Entity で置き換えたものです。

### ステップ 5: 通知対象を絞り込む

```csharp
List<AppDetails> targetApps = saleApps.Where(app =>
{
    decimal currentPrice = app.PriceOverview!.Final / 100m;
    return !snapshot.NotifiedPrices.TryGetValue(app.SteamAppId, out decimal notifiedPrice) || notifiedPrice != currentPrice;
}).ToList();
```

「まだ一度も通知していない」または「前回通知時から価格が変わった」アプリだけに絞り込みます。
これにより、同じセール情報を毎時間繰り返し通知してしまうことを防いでいます。

### ステップ 6: 過去最安値を並列取得 (Fan-out/Fan-in)

```csharp
List<LowestPriceResult?> lowestPrices = [];
AppDetails[][] targetAppChunks = [.. targetApps.Chunk(MaxFanOutConcurrency)];
for (int chunkIndex = 0; chunkIndex < targetAppChunks.Length; chunkIndex++)
{
    IEnumerable<Task<LowestPriceResult?>> lowestPriceTasks = targetAppChunks[chunkIndex].Select(app =>
        context.CallActivityAsync<LowestPriceResult?>(FunctionNames.GetLowestPriceActivity, app.SteamAppId, ActivityRetryOptions));
    lowestPrices.AddRange(await Task.WhenAll(lowestPriceTasks));

    bool isLastChunk = chunkIndex == targetAppChunks.Length - 1;
    if (!isLastChunk)
    {
        await context.CreateTimer(context.CurrentUtcDateTime.Add(LowestPriceFanOutInterval), CancellationToken.None);
    }
}
```

通知対象のアプリそれぞれについて、外部サービス (IsThereAnyDeal / CheapShark) から過去最安値を取得します。
ここでも、ステップ 2 と同様にチャンク分割 + 待機を組み合わせた Fan-out/Fan-in パターンが使われています
(待機時間はステップ 2 とは異なる `LowestPriceFanOutInterval` を使用しており、呼び出し先の API ごとに
適切な間隔を個別に設定していることがわかります)。

### ステップ 7: Discord に通知 (初回実行時を除く)

```csharp
if (snapshot.IsFirstRun)
{
    logger.LogInformation("First run detected. Recording state without sending Discord notification for {count} apps", notifications.Count);
}
else
{
    await context.CallActivityAsync(FunctionNames.SendDiscordNotificationActivity, notifications, ActivityRetryOptions);
}
```

ここで重要なのが `snapshot.IsFirstRun` のチェックです。
**初回実行時は、ウィッシュリスト全体が「まだ通知していないセール」として検出されてしまいます。**
これをそのまま Discord に送ると、大量の通知が一度に飛んでしまうため、
初回だけは送信をスキップし、状態の記録だけを行うようにしています。

### ステップ 8・9: 通知状態を更新する

```csharp
// 8. 新規・価格変更分を記録
foreach (AppDetails app in targetApps)
{
    decimal currentPrice = app.PriceOverview!.Final / 100m;
    await context.Entities.CallEntityAsync(entityId, NotificationStateEntity.OperationSetNotified, new NotifiedEntry(app.SteamAppId, currentPrice));
}

// 9. セールが終了し対象から外れたアプリの記録を削除
foreach (long notifiedAppId in snapshot.NotifiedPrices.Keys)
{
    if (saleApps.Any(app => app.SteamAppId == notifiedAppId))
    {
        continue;
    }
    await context.Entities.CallEntityAsync(entityId, NotificationStateEntity.OperationRemoveNotified, notifiedAppId);
}
```

最後に、今回通知した内容をエンティティに記録 (ステップ 8) し、
セールが終了して対象から外れたアプリの記録を削除 (ステップ 9) します。
こうすることで、次回実行時に正しく「前回からの差分」を計算できるようになります。

## 3.3 フォルダー構成と名前空間の設計

本プロジェクトのフォルダー構成は、**役割ベース (Triggers / Orchestrations / Activities / Entities / Models)**
を基本としつつ、補助的な部品をまとめる `Common/` フォルダーを設け、
さらに `Models/` 配下は **ドメインベース (Wishlist / Pricing / Notification)** で分割する、
というハイブリッドな設計を採用しています。

```
WatchWishlistSale/
├── Program.cs                                  ※ ルート直下はこのファイルのみ
├── Common/
│   └── FunctionNames.cs                        … namespace WatchWishlistSale.Common
├── Triggers/
│   └── Crawler.cs                              … namespace WatchWishlistSale.Triggers
├── Orchestrations/
│   └── WatchWishlistOrchestrator.cs            … namespace WatchWishlistSale.Orchestrations
├── Activities/
│   ├── GetWishlistAppIds.cs                    … namespace WatchWishlistSale.Activities
│   ├── GetAppDetails.cs
│   ├── FilterSaleApps.cs
│   ├── GetLowestPrice.cs
│   └── SendDiscordNotification.cs
├── Entities/
│   └── NotificationStateEntity.cs              … namespace WatchWishlistSale.Entities
└── Models/
    ├── Wishlist/                               … namespace WatchWishlistSale.Models.Wishlist
    │   ├── WishlistItem.cs                     (ウィッシュリスト API のレスポンス DTO 群)
    │   └── AppDetails.cs                       (アプリ詳細・価格情報の DTO 群)
    ├── Pricing/                                … namespace WatchWishlistSale.Models.Pricing
    │   ├── LowestPriceResult.cs
    │   ├── ItadResponses.cs                    (IsThereAnyDeal API のレスポンス DTO 群)
    │   └── CheapSharkResponses.cs              (CheapShark API のレスポンス DTO 群)
    └── Notification/                           … namespace WatchWishlistSale.Models.Notification
        ├── SaleNotification.cs
        ├── NotificationState.cs                (エンティティの状態・操作の DTO 群)
        └── DiscordPayload.cs                   (Discord Webhook のペイロード DTO 群)
```

設計上のポイント:

- **「役割」で大分類する**: 「これは何をするコードか (トリガーなのか、アクティビティなのか...)」が
  フォルダー名から一目でわかるようにする
- **`Common/` を切り出す**: 複数の役割から横断的に参照される部品 (本プロジェクトでは Function 名の定数) を
  独立したフォルダーにまとめ、特定の役割に属さないことを明示する
- **`Models/` だけは「ドメイン」で分ける**: DTO (データ転送オブジェクト) は数が多くなりがちなため、
  「どの関心事に関するデータか (ウィッシュリストか、価格か、通知か)」で整理した方が見通しが良い
- **小さな関連 DTO は 1 ファイルにまとめる**: 例えば `NotificationState.cs` には
  `NotifiedEntry` / `NotificationSnapshot` / `NotificationState` という 3 つの小さな型が同居しています。
  「1 ファイル 1 型」に固執せず、強い関連を持つ小さな型をまとめることでファイル数の増加を抑えています
- **名前空間はフォルダー構成と一致させる**: C# の慣習 (および Roslyn アナライザーの IDE0130 ルール) に従い、
  `WatchWishlistSale.Activities` のように、フォルダーパスをそのまま名前空間にしています。
  これにより「どのファイルがどこにあるか」を名前空間から推測できます

## まとめ

- 1 つのオーケストレーターが、複数のアクティビティとエンティティを呼び出して全体のワークフローを構成している
- `IsFirstRun` のチェックのように、「ビジネスロジック上どうしても必要な分岐」もオーケストレーターに書かれている
  (ただし、これ自体は非決定的な処理ではないため、決定論性の制約には違反しない)
- フォルダー構成・名前空間は「役割ベース + ドメインベースのハイブリッド」で設計されている

次は [04-code-walkthrough.md](./04-code-walkthrough.md) で、
個々のコンポーネントのコードをより詳細に読み解いていきます。
