# 4. コードウォークスルー

ここでは、主要なコンポーネントのコードを実際に読みながら、
「なぜこう書かれているのか」を 1 つずつ確認していきます。

## 4.1 トリガー: `Triggers/Crawler.cs`

すべての処理の起点となる、タイマートリガーです。

```csharp
public class Crawler(IConfiguration configuration, ILogger<Crawler> logger)
{
    [Function(FunctionNames.RunCrawler)]
    public async Task RunCrawler(
        [TimerTrigger("0 0 * * * *")] TimerInfo myTimer,
        [DurableClient] DurableTaskClient client)
    {
        logger.LogInformation("Timer trigger function executed at: {executionTime}", DateTime.Now);
        if (myTimer.ScheduleStatus is not null)
        {
            logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }

        string profileId = configuration["STEAM_PROFILE_ID"]
            ?? throw new InvalidOperationException("STEAM_PROFILE_ID is not configured");

        // プロフィール ID ごとにインスタンス ID を固定し、同一プロフィールに対するオーケストレーターの多重起動を防ぐ (シングルトンパターン)
        string instanceId = $"{FunctionNames.CrawlerOrchestrator}-{profileId}";
        OrchestrationMetadata? existingInstance = await client.GetInstanceAsync(instanceId);
        if (existingInstance is { RuntimeStatus: OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending })
        {
            logger.LogInformation(
                "Orchestration instance {instanceId} is already running (status: {status}). Skipping this run.",
                instanceId,
                existingInstance.RuntimeStatus);
            return;
        }

        await client.ScheduleNewOrchestrationInstanceAsync(
            FunctionNames.CrawlerOrchestrator,
            profileId,
            new StartOrchestrationOptions(InstanceId: instanceId));
        logger.LogInformation("Started orchestration with ID = {instanceId} for profile id: {profileId}", instanceId, profileId);
    }
}
```

読み解きポイント:

1. **設定値の取得**: `configuration["STEAM_PROFILE_ID"]` で、`local.settings.json` (ローカル) や
   Azure portal の「アプリケーション設定」(本番) から監視対象のプロフィール ID を取得します。
   設定されていなければ即座に例外を投げ、誤った状態で処理を進めないようにしています。

2. **インスタンス ID の固定化**: `$"{FunctionNames.CrawlerOrchestrator}-{profileId}"` のように、
   オーケストレーターの実行インスタンスを識別する ID をプロフィール ID から **決定的に** 組み立てています。
   通常、`ScheduleNewOrchestrationInstanceAsync` を呼ぶたびにランダムな ID が払い出されますが、
   それでは「前回の実行がまだ終わっていないのに、新しい実行を開始してしまう」ことを防げません。

3. **多重起動チェック**: `client.GetInstanceAsync(instanceId)` で同じ ID のインスタンスが存在するかを調べ、
   実行中 (`Running`) または開始待ち (`Pending`) であれば、新規実行をスキップします。
   これは「シングルトンオーケストレーターパターン」と呼ばれる設計です。詳しくは
   [05-best-practices.md](./05-best-practices.md) で解説します。

## 4.2 オーケストレーター: `Orchestrations/WatchWishlistOrchestrator.cs`

[03-architecture-and-flow.md](./03-architecture-and-flow.md) で処理の流れ自体は解説したので、
ここではコードの「書き方」に注目してみましょう。

```csharp
private static readonly TaskOptions ActivityRetryOptions = TaskOptions.FromRetryPolicy(new RetryPolicy(
    maxNumberOfAttempts: 3,
    firstRetryInterval: TimeSpan.FromSeconds(5),
    backoffCoefficient: 2.0));
```

クラスの先頭で、外部 API 呼び出しを伴うアクティビティに共通で使うリトライポリシーを定義しています。
これを `CallActivityAsync` の第 3 引数として渡すことで、「一時的なエラーに対しては自動で再試行する」
という振る舞いを宣言的に組み込めます (詳細は [05-best-practices.md](./05-best-practices.md))。

```csharp
ILogger logger = context.CreateReplaySafeLogger(nameof(WatchWishlistOrchestrator));
```

[02-durable-functions-fundamentals.md](./02-durable-functions-fundamentals.md#決定論性determinism-という制約) で説明した通り、
オーケストレーター内では `context.CreateReplaySafeLogger(...)` を使うことで、
リプレイによるログの重複出力を防いでいます。

```csharp
string profileId = context.GetInput<string>()
    ?? throw new InvalidOperationException("Steam profile id is required as orchestrator input");
```

`context.GetInput<T>()` で、トリガー元 (`Crawler.RunCrawler`) から渡された入力 (`profileId`) を取得します。

## 4.3 アクティビティ: 個々のコンポーネント

### `GetWishlistAppIds` ── 外部 API 呼び出しの基本形

```csharp
public class GetWishlistAppIds(IHttpClientFactory httpClientFactory, ILogger<GetWishlistAppIds> logger)
{
    [Function(FunctionNames.GetWishlistAppIdsActivity)]
    public async Task<List<long>> GetWishlistAppIdsActivity([ActivityTrigger] string profileId)
    {
        logger.LogInformation("Getting wishlist app ids for profile id: {profileId}", profileId);

        string url = $"https://api.steampowered.com/IWishlistService/GetWishlist/v1/?steamid={profileId}";
        HttpClient client = httpClientFactory.CreateClient(nameof(GetWishlistAppIds));
        WishlistApiResponse? result = await client.GetFromJsonAsync<WishlistApiResponse>(url);
        List<WishlistItem>? items = result?.Response?.Items;
        if (items is null)
        {
            throw new InvalidOperationException($"Failed to get wishlist items for profile id {profileId} ({url})");
        }

        List<long> appIds = items.Select(item => item.AppId).ToList();
        logger.LogInformation("Got {appIdsCount} app ids for profile id: {profileId}", appIds.Count, profileId);
        return appIds;
    }
}
```

`[ActivityTrigger] string profileId` のように、`[ActivityTrigger]` 属性を付けた引数が
「オーケストレーターから渡される入力」になります。戻り値の型 (`Task<List<long>>`) が、
オーケストレーター側で `CallActivityAsync<List<long>>(...)` の型引数と対応します。

`client.GetFromJsonAsync<T>(url)` は `System.Net.Http.Json` 名前空間の拡張メソッドで、
HTTP GET リクエストを送り、レスポンスの JSON を直接 C# のオブジェクトにデシリアライズしてくれます。
レスポンスの構造は [`Models/Wishlist/WishlistItem.cs`](../Models/Wishlist/WishlistItem.cs) の
`WishlistApiResponse` / `WishlistResponseBody` / `WishlistItem` という入れ子構造の DTO で表現されています。

> 📝 **コラム: なぜ HTML スクレイピングではなく公式 API を使っているのか**
>
> 実はこのアクティビティは、開発の途中で実装方式を変更した経緯があります。
> 当初はウィッシュリストページの HTML に埋め込まれた `g_rgWishlistData` という JSON を
> 正規表現で抜き出す「スクレイピング」方式で実装していましたが、Steam 側がページを SPA 化したことで
> この埋め込み JSON 自体が無くなってしまい、動作しなくなりました。
>
> 代わりに見つけたのが `https://api.steampowered.com/IWishlistService/GetWishlist/v1/` という
> 公式 Web API です (API キー不要、ただし `steamid` には数値の SteamID64 を渡す必要があります)。
> この一件は、**「外部サービスの非公式な実装詳細に依存すると、相手の仕様変更で簡単に壊れる」**
> という典型的な教訓を示しています。可能な限り、公式に提供されている API を優先して使いましょう。

### `GetAppDetails` ── レスポンスの一部だけをモデル化する

```csharp
public async Task<AppDetails?> GetAppDetailsActivity([ActivityTrigger] long appId)
{
    string url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=JP";
    HttpClient client = httpClientFactory.CreateClient(nameof(GetAppDetails));
    using HttpResponseMessage response = await client.GetAsync(url);
    if (!response.IsSuccessStatusCode)
    {
        logger.LogWarning("⚠️ HTTP error: {statusCode} {reasonPhrase} ({url})", (int)response.StatusCode, response.ReasonPhrase, url);
        return null;
    }

    await using Stream stream = await response.Content.ReadAsStreamAsync();
    Dictionary<string, AppDetailsResult>? results = await JsonSerializer.DeserializeAsync<Dictionary<string, AppDetailsResult>>(stream);
    if (results is null
      || !results.TryGetValue(appId.ToString(), out AppDetailsResult? result)
      || !result.Success
      || result.Data is null)
    {
        logger.LogWarning("⚠️ Failed to get app data for app id {appId}", appId);
        return null;
    }

    return result.Data;
}
```

注目してほしいのは [`Models/Wishlist/AppDetails.cs`](../Models/Wishlist/AppDetails.cs) のモデル定義です。
Steam の `appdetails` API は非常に多くのフィールドを返しますが、
本プロジェクトが必要とするのは `type` / `name` / `steam_appid` / `price_overview` のごく一部だけです。

```csharp
public class AppDetails
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("steam_appid")]
    public long SteamAppId { get; set; }

    [JsonPropertyName("price_overview")]
    public PriceOverview? PriceOverview { get; set; }
}
```

`System.Text.Json` は、モデルに定義されていない JSON フィールドを **自動的に無視** します。
そのため、レスポンス全体を表すモデルを作る必要はなく、**自分が使うフィールドだけを定義すれば十分** です。
これは「外部 API のレスポンスをモデル化するときの定石」と言えます。

戻り値が `Task<AppDetails?>` (Nullable) になっている点にも注目してください。
HTTP エラーやデータ欠損が起きた場合は `null` を返すことで、
「1 件の取得失敗が、全体の処理を止めてしまう」ことを防いでいます
(オーケストレーター側で `OfType<AppDetails>()` によって `null` が除外される、という流れでした)。

### `FilterSaleApps` ── 純粋な絞り込み処理をアクティビティにする理由

```csharp
public class FilterSaleApps(ILogger<FilterSaleApps> logger)
{
    [Function(FunctionNames.FilterSaleAppsActivity)]
    public List<AppDetails> FilterSaleAppsActivity([ActivityTrigger] List<AppDetails> appDetails)
    {
        List<AppDetails> saleApps = appDetails.Where(app =>
        {
            if (app.PriceOverview is null)
            {
                // 価格情報がない => 未発売 or 販売終了
                return false;
            }
            // 割引率が 0 => 割引なし
            return app.PriceOverview.DiscountPercent != 0;
        }).ToList();

        return saleApps;
    }
}
```

このアクティビティは外部 I/O を一切行わない、ただのリスト絞り込み処理です。
「だったらオーケストレーターの中に直接書けばいいのでは?」と思うかもしれませんが、
あえてアクティビティとして切り出すことには意味があります。

- **オーケストレーターの本体をシンプルに保てる**: ビジネスロジックの詳細をオーケストレーターから追い出すことで、
  全体のワークフロー (「何をどんな順番で行うか」) が読みやすくなる
- **大量データの処理をオーケストレーターのスレッドから外せる**: オーケストレーターはリプレイのたびに再実行されるため、
  重い処理を直接書くと、リプレイのたびにそのコストがかかってしまう。アクティビティに切り出せば、
  完了済みの呼び出しはリプレイ時に再実行されず、保存された結果がそのまま使われる

### `GetLowestPrice` ── 複数の外部サービスを 1 つのアクティビティにまとめる設計判断

```csharp
public async Task<LowestPriceResult?> GetLowestPriceActivity([ActivityTrigger] long appId)
{
    LowestPriceResult? result = await this.GetFromItadAsync(appId)
      ?? await this.GetFromCheapSharkAsync(appId);
    if (result is null)
    {
        logger.LogWarning("⚠️ Failed to get lowest price for app id {appId}", appId);
    }

    return result;
}
```

このアクティビティは、IsThereAnyDeal (ITAD) API を主軸とし、そこで取得できなければ
CheapShark API にフォールバックする、という 2 段構えの処理を内部に持っています。
クラスの XML ドキュメントコメントにその設計判断の理由が書かれています。

> 設計上のトレードオフ: ITAD と CheapShark への複数回の HTTP 呼び出しを 1 つの Activity にまとめている。
> 「Activity は外部 I/O を 1 回だけ行うべき」という原則に厳密には反するが、
> (1) どちらも「同じアプリの過去最安値を取得する」という単一の責務を構成する一連の処理であり、
> (2) フォールバック判定 (ITAD で取得できない場合のみ CheapShark を呼ぶ) はオーケストレーター内では
>     決定論性の制約により行えず、Activity 側に委譲せざるを得ないため、
> あえて 1 つの Activity にまとめている。

ここで伝えたいのは、**「原則」と「現実のトレードオフ」のバランスをどう取るか** という考え方です。
理想だけを追い求めて「ITAD 用」「CheapShark 用」の 2 つのアクティビティに分割してしまうと、
今度はオーケストレーター側に「ITAD がダメだったら CheapShark を呼ぶ」という条件分岐が必要になります。
しかしこの分岐は外部呼び出しの結果に依存する非決定的な分岐であり、決定論性の制約に違反してしまいます。

このように、設計原則がぶつかり合う場面では「なぜそうしたのか」を明文化しておくことが重要です。
将来コードを読む人 (未来の自分を含む) が、同じ疑問を抱いたときに迷わないようにするためです。

### `SendDiscordNotification` ── API の制限に合わせてデータをチャンク分割する

```csharp
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
}
```

Discord の Embed には「1 つの埋め込みに含められるフィールド数は最大 25 件」という制限があります。
`IEnumerable<T>.Chunk(int)` (.NET 6 以降の LINQ メソッド) を使うことで、
「25 件ずつのかたまりに分割して、それぞれ別の埋め込みとして送信する」処理を簡潔に書いています。

外部 API を呼び出す処理では、こうした「相手側の制限にどう対応するか」を考える必要が
頻繁に出てきます。今回のように `Chunk` を使えば、自前でループとカウンターを書くよりも
ずっと読みやすいコードになります。

## 4.4 エンティティ: `Entities/NotificationStateEntity.cs`

```csharp
public class NotificationStateEntity : TaskEntity<NotificationState>
{
    public const string OperationGetSnapshot = nameof(GetSnapshot);
    public const string OperationSetNotified = nameof(SetNotified);
    public const string OperationRemoveNotified = nameof(RemoveNotified);

    public Task<NotificationSnapshot> GetSnapshot()
    {
        bool isFirstRun = !this.State.Initialized;
        this.State.Initialized = true;
        return Task.FromResult(new NotificationSnapshot(isFirstRun, new Dictionary<long, decimal>(this.State.NotifiedPrices)));
    }

    public void SetNotified(NotifiedEntry entry)
    {
        this.State.NotifiedPrices[entry.AppId] = entry.Price;
    }

    public void RemoveNotified(long appId)
    {
        this.State.NotifiedPrices.Remove(appId);
    }

    protected override NotificationState InitializeState(TaskEntityOperation operation) => new();

    [Function(FunctionNames.NotificationStateEntity)]
    public static Task DispatchAsync([EntityTrigger] TaskEntityDispatcher dispatcher)
      => dispatcher.DispatchAsync<NotificationStateEntity>();
}
```

読み解きポイント:

1. **`TaskEntity<NotificationState>` を継承する**: 型引数に渡した [`NotificationState`](../Models/Notification/NotificationState.cs)
   が、このエンティティが保持する状態の型になります。基底クラスの `this.State` プロパティを通じて読み書きできます。

2. **公開メソッドがそのまま「操作」になる**: `GetSnapshot` / `SetNotified` / `RemoveNotified` という
   3 つの公開メソッドが、外部 (オーケストレーター) から `CallEntityAsync` で呼び出せる操作として公開されます。

3. **`OperationXxx` という定数を用意している理由**: `CallEntityAsync` で操作を呼び出す際には、
   操作名を文字列で指定する必要があります。`nameof(GetSnapshot)` の結果である `"GetSnapshot"` という
   文字列をハードコードする代わりに `NotificationStateEntity.OperationGetSnapshot` という定数を経由することで、
   メソッド名が変更されてもコンパイルエラーで気づける (= タイプセーフになる) ようにしています。

4. **`InitializeState` で初期値を定義する**: エンティティが初めて呼び出されたとき
   (= まだ状態が保存されていないとき) に使われる初期値を返します。`new()` によって、
   `Initialized = false`、`NotifiedPrices = []` という初期状態が作られます。

5. **`IsFirstRun` の判定方法**: `GetSnapshot` の中で `!this.State.Initialized` を見て「初回呼び出しか」を判定し、
   呼び出し後に `this.State.Initialized = true` とすることで「次回以降は初回ではない」と記録しています。
   これは、旧実装における「`notified.json` ファイルが存在するかどうか」による初回判定を、
   エンティティの状態というより堅牢な仕組みに置き換えたものです。

6. **`DispatchAsync` という入り口**: `[EntityTrigger]` 属性を付けた静的メソッドが、
   このエンティティクラスを Azure Functions に登録するためのエントリーポイントです。
   `dispatcher.DispatchAsync<NotificationStateEntity>()` が、実際にどの操作が呼ばれたかを判定し、
   対応するメソッドを実行してくれます。

## まとめ

- トリガー: 設定値の検証とシングルトン制御を担う「入口」
- オーケストレーター: 共通のリトライポリシーやリプレイセーフなロガーを使い、全体のワークフローを記述する
- アクティビティ: 外部 API 呼び出し・データ整形など、実際の作業を行う。レスポンスは「使うフィールドだけ」モデル化する
- エンティティ: 公開メソッドが操作になり、状態を安全に読み書きできる

次は [05-best-practices.md](./05-best-practices.md) で、
ここまでに登場したパターンを「なぜそう書くべきなのか」という観点で整理します。
