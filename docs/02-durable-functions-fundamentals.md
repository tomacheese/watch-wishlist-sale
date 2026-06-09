# 2. Durable Functions の基礎

本プロジェクトの中核は **Durable Functions** という拡張機能です。
通常の Azure Functions だけでは難しい「複数の処理を順番に・並列に・状態を持たせて実行する」
といったことを実現できます。

## 2.1 なぜ Durable Functions が必要なのか

本プロジェクトがやりたいことを思い出してみましょう。

1. ウィッシュリストの App ID を取得する
2. **各 App ID ごとに** 詳細情報を取得する (件数は実行時にならないとわからない)
3. セール中のものだけ抽出する
4. **前回の通知状態と比較する** (= 状態を保持する必要がある)
5. 対象アプリの過去最安値を取得する
6. Discord に通知する
7. 通知した内容を次回のために記録する

これを普通の Function 1 つで書こうとすると、

- 「件数が可変な並列処理」をどう表現するか
- 処理の途中で Function の実行時間制限 (タイムアウト) に達したらどうするか
- 「前回の状態」をどこに、どう安全に保存するか

といった課題にぶつかります。Durable Functions は、これらを解決するために
**オーケストレーター・アクティビティ・エンティティ** という 3 つの役割を提供します。

## 2.2 オーケストレーター (Orchestrator)

**全体の処理の流れ (ワークフロー) を記述する** 役割です。
本プロジェクトでは [`Orchestrations/WatchWishlistOrchestrator.cs`](../Orchestrations/WatchWishlistOrchestrator.cs) がこれにあたります。

```csharp
[Function(FunctionNames.CrawlerOrchestrator)]
public static async Task RunOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    // 1. ウィッシュリストの App ID 一覧を取得
    List<long> appIds = await context.CallActivityAsync<List<long>>(FunctionNames.GetWishlistAppIdsActivity, profileId, ActivityRetryOptions);

    // 2. 各 App ID の詳細情報を並列取得 (Fan-out)
    IEnumerable<Task<AppDetails?>> appDetailsTasks = appIds.Select(appId =>
        context.CallActivityAsync<AppDetails?>(FunctionNames.GetAppDetailsActivity, appId, ActivityRetryOptions));
    AppDetails?[] appDetailsResults = await Task.WhenAll(appDetailsTasks);

    // ... 以下、3〜9 のステップが続く
}
```

ぱっと見は普通の C# の `async`/`await` を使った逐次処理のように見えますが、
これは Durable Functions が提供する特別な実行モデル ―― **オーケストレーション** ―― の上で動いています。

### オーケストレーションの正体: イベントソーシングとリプレイ

Durable Functions のオーケストレーターは、実は **「最初から最後まで一気に動き続ける」わけではありません**。

- アクティビティを呼び出す (`await context.CallActivityAsync(...)`) たびに、いったん実行が中断される
- アクティビティの実行が完了すると、そのオーケストレーターの関数が **最初から再実行 (リプレイ) される**
- ただし、すでに完了したアクティビティの呼び出し結果は「履歴」として保存されているため、
  再実行時には実際には呼び出さず、保存された結果を即座に返す (これを「リプレイ」と呼びます)
- こうして少しずつ「前回の続き」へと進んでいき、最終的に関数全体が完了する

これは「イベントソーシング」と呼ばれる設計パターンの応用です。
すべての操作の履歴 (どのアクティビティを呼び、どんな結果が返ってきたか) をストレージに保存しておくことで、

- 実行途中でホストが再起動・スケールインしても、続きから再開できる
- 数日かかるような長時間のワークフローでも、タイムアウトを気にせず実行できる

という利点が得られます。

### 決定論性 (Determinism) という制約

リプレイの仕組みが正しく機能するためには、
**オーケストレーターのコードは「同じ入力に対して常に同じ手順で動く」必要があります**。これを **決定論性** と呼びます。

具体的には、オーケストレーターのコード内で以下のようなことをしてはいけません。

| NG な操作 | 理由 | 代わりにどうするか |
|---|---|---|
| `DateTime.Now` / `DateTime.UtcNow` | リプレイのたびに違う値になる | `context.CurrentUtcDateTime` を使う |
| `Guid.NewGuid()` | リプレイのたびに違う値になる | `context.NewGuid()` を使う |
| `Random` | リプレイのたびに違う値になる | アクティビティ内で生成し、結果を受け取る |
| HTTP 呼び出しなどの I/O | 非決定的・リプレイのたびに実行されると困る | アクティビティに処理を移譲する |
| `Task.Delay` / `Thread.Sleep` | スレッドをブロックしてしまう | `context.CreateTimer(...)` を使う |
| 非同期処理に直接 `ILogger` を使う | リプレイ中にもログが出力されてしまう | `context.CreateReplaySafeLogger(...)` を使う |

本プロジェクトのオーケストレーターでも、ロガーをこのように取得しています。

```csharp
ILogger logger = context.CreateReplaySafeLogger(nameof(WatchWishlistOrchestrator));
```

`CreateReplaySafeLogger` で取得したロガーは、リプレイ中の重複ログ出力を自動的に抑制してくれます。
通常の `ILogger<T>` をオーケストレーター内で使うと、リプレイのたびに同じログが何度も出力されてしまうため、
このメソッドを使うことが推奨されています。

> 💡 では「外部 API の呼び出し」や「ファイル I/O」のような非決定的な処理はどう書けばよいのでしょうか?
> その答えが、次に説明する **アクティビティ** です。

## 2.3 アクティビティ (Activity)

**実際の作業 (I/O を伴う処理など) を行う役割** です。
オーケストレーターから「これをやって」と指示を受けて実行され、結果をオーケストレーターに返します。

本プロジェクトでは [`Activities/`](../Activities/) フォルダーの各クラスがこれにあたります。

| アクティビティ | 役割 |
|---|---|
| [`GetWishlistAppIds`](../Activities/GetWishlistAppIds.cs) | Steam Web API からウィッシュリストの App ID 一覧を取得する |
| [`GetAppDetails`](../Activities/GetAppDetails.cs) | 1 つの App ID について、価格などの詳細情報を取得する |
| [`FilterSaleApps`](../Activities/FilterSaleApps.cs) | アプリ一覧からセール中のものだけを抽出する (純粋な絞り込み処理) |
| [`GetLowestPrice`](../Activities/GetLowestPrice.cs) | 1 つの App ID について、過去最安値を取得する |
| [`SendDiscordNotification`](../Activities/SendDiscordNotification.cs) | Discord Webhook に通知を送信する |

アクティビティは「決定論性」の制約を受けません。
**HTTP 呼び出しや日時の取得など、非決定的な処理はすべてアクティビティ側に書く**、というのが基本方針です。

オーケストレーターからアクティビティを呼び出すには `context.CallActivityAsync<TResult>(関数名, 入力)` を使います。

```csharp
List<long> appIds = await context.CallActivityAsync<List<long>>(
    FunctionNames.GetWishlistAppIdsActivity,  // どのアクティビティを呼ぶか (関数名)
    profileId,                                 // アクティビティへの入力
    ActivityRetryOptions);                     // リトライポリシー (省略可)
```

## 2.4 エンティティ (Entity)

**状態を保持し、操作を通じて読み書きする役割** です。
旧来の「ファイルに JSON を保存して読み書きする」ような処理を、Durable Functions の世界で安全に行うための仕組みです。

本プロジェクトでは [`Entities/NotificationStateEntity.cs`](../Entities/NotificationStateEntity.cs) が、
旧実装での `data/notified.json` (通知済みのセール情報を記録するファイル) の役割を引き継いでいます。

```csharp
public class NotificationStateEntity : TaskEntity<NotificationState>
{
    public Task<NotificationSnapshot> GetSnapshot() { /* ... */ }
    public void SetNotified(NotifiedEntry entry) { /* ... */ }
    public void RemoveNotified(long appId) { /* ... */ }
}
```

エンティティには以下の特徴があります。

- `TaskEntity<TState>` を継承し、`TState` 型 (ここでは [`NotificationState`](../Models/Notification/NotificationState.cs)) の状態を持つ
- 公開メソッドが「操作 (Operation)」となり、外部から呼び出せる
- **同じエンティティに対する操作は、常に 1 つずつ順番に実行される** ことが保証される
  (= 複数のオーケストレーターから同時に呼ばれても、競合状態が発生しない)
- 状態は Durable Functions のストレージに永続化されるため、再起動してもデータが消えない

エンティティは **エンティティ ID** によって一意に識別されます。本プロジェクトでは
プロフィール ID をキーとして使うことで、「プロフィールごとに通知状態を分けて記録する」設計になっています。

```csharp
EntityInstanceId entityId = new(FunctionNames.NotificationStateEntity, profileId);
NotificationSnapshot snapshot = await context.Entities.CallEntityAsync<NotificationSnapshot>(
    entityId, NotificationStateEntity.OperationGetSnapshot);
```

## 2.5 Fan-out/Fan-in パターン

「複数の対象に対して同じ処理を並列に行い (Fan-out)、すべて完了したら結果をまとめる (Fan-in)」
という非常によく使われるパターンです。本プロジェクトでは、ウィッシュリスト内の各アプリに対して
詳細情報を取得する箇所で使われています。

```csharp
// Fan-out: 各 App ID について、並列にアクティビティを呼び出す
IEnumerable<Task<AppDetails?>> appDetailsTasks = appIds.Select(appId =>
    context.CallActivityAsync<AppDetails?>(FunctionNames.GetAppDetailsActivity, appId, ActivityRetryOptions));

// Fan-in: すべての並列処理が完了するのを待ち、結果をまとめる
AppDetails?[] appDetailsResults = await Task.WhenAll(appDetailsTasks);
```

`context.CallActivityAsync` はそれぞれ「アクティビティの呼び出しを表すタスク」を返すだけで、
すぐには完了しません。これを `IEnumerable<Task<T>>` として複数集め、
`Task.WhenAll` で「すべて完了するまで待つ」ことで、Fan-out/Fan-in を実現しています。

これにより、ウィッシュリストに何百個アプリが登録されていても、
1 つずつ順番に問い合わせるよりずっと短い時間で全件の詳細情報を取得できます。

> 💡 **「全件同時に Fan-out」が常に正解とは限らない**
>
> 上記のコードは Fan-out/Fan-in の考え方を理解するための最小限の例です。
> しかし、呼び出し先が外部 API である場合、**全件を一度に Fan-out すると、
> 相手側のレート制限 (一定時間あたりのリクエスト数の上限) に抵触してしまう** ことがあります。
>
> 実際、本プロジェクトが利用している Steam ストアの `appdetails` API には
> 「約 200 リクエスト / 5 分」という厳しい制限があり、件数の多いウィッシュリストを
> そのまま全件 Fan-out すると簡単に超過してしまいます。
>
> そのため実際のコード ([`Orchestrations/WatchWishlistOrchestrator.cs`](../Orchestrations/WatchWishlistOrchestrator.cs))
> では、`appIds.Chunk(N)` で一定件数ずつのチャンクに分割し、チャンクとチャンクの間に
> `context.CreateTimer(...)` で待機を挟みながら Fan-out するという、一段階発展した形を取っています。
> 詳しくは [05-best-practices.md](./05-best-practices.md#56-fan-out-のレート制御) で解説します。

## 2.6 オーケストレーターを開始する

オーケストレーターは、それ自体ではトリガーされません。
何らかの Function (本プロジェクトではタイマートリガー) が `DurableTaskClient` を使って開始します。

```csharp
[Function(FunctionNames.RunCrawler)]
public async Task RunCrawler(
    [TimerTrigger("0 0 * * * *")] TimerInfo myTimer,
    [DurableClient] DurableTaskClient client)
{
    // ...
    await client.ScheduleNewOrchestrationInstanceAsync(
        FunctionNames.CrawlerOrchestrator,
        profileId,
        new StartOrchestrationOptions(InstanceId: instanceId));
}
```

`[DurableClient]` バインディングによって `DurableTaskClient` が注入され、
これを使って「どのオーケストレーターを」「どんな入力で」「どんなインスタンス ID で」開始するかを指定します。

> 💡 `InstanceId` を明示的に指定している理由 (シングルトンパターン) については、
> [05-best-practices.md](./05-best-practices.md) で詳しく解説します。

## まとめ

| 役割 | 何をするか | 制約 |
|---|---|---|
| オーケストレーター | 全体の流れを記述する | 決定論性が必須 (I/O 禁止、`context` 経由でのみ非決定的操作を行う) |
| アクティビティ | 実際の作業 (I/O など) を行う | 制約なし (通常の C# コードと同じ感覚で書ける) |
| エンティティ | 状態を保持・操作する | 操作は 1 つずつ順番に実行される (競合状態が起きない) |

- オーケストレーターは「リプレイ」によって長時間の処理や中断・再開を実現している
- そのために「決定論性」という制約があり、非決定的な処理はすべてアクティビティに任せる
- Fan-out/Fan-in パターンで、可変個数の並列処理を簡潔に書ける
- エンティティを使うことで、安全に状態を読み書きできる (ファイル I/O の代替)

次は [03-architecture-and-flow.md](./03-architecture-and-flow.md) で、
これらの要素が本プロジェクトの中でどう組み合わさっているかを見ていきましょう。
