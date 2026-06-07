# 5. ベストプラクティスとその理由

ここまでのドキュメントでは「何が書かれているか」を中心に見てきましたが、
本章では「なぜそう書かれているのか」というベストプラクティスの観点から、コードを振り返ります。

## 5.1 アクティビティのリトライポリシー

[`Orchestrations/WatchWishlistOrchestrator.cs`](../Orchestrations/WatchWishlistOrchestrator.cs) には、
以下のような静的フィールドが定義されています。

```csharp
/// <summary>
/// 外部 API 呼び出しを伴うアクティビティの再試行ポリシー。
/// 一時的なエラー (ネットワーク不調やレート制限など) に対し、間隔を空けながら最大 3 回まで試行する。
/// </summary>
private static readonly TaskOptions ActivityRetryOptions = TaskOptions.FromRetryPolicy(new RetryPolicy(
    maxNumberOfAttempts: 3,
    firstRetryInterval: TimeSpan.FromSeconds(5),
    backoffCoefficient: 2.0));
```

外部 API (Steam・Discord・IsThereAnyDeal・CheapShark) の呼び出しは、ネットワークの瞬断や
レート制限 (429) など、**一時的なエラー** に遭遇することがあります。これをそのまま失敗として扱うと、
たまたま発生した一過性の問題でオーケストレーション全体が失敗してしまいます。

そこで `context.CallActivityAsync` の第 3 引数に `TaskOptions` を渡すことで、
アクティビティ呼び出しが失敗したときの再試行を Durable Functions に任せています。

```csharp
List<long> appIds = await context.CallActivityAsync<List<long>>(
    FunctionNames.GetWishlistAppIdsActivity, profileId, ActivityRetryOptions);
```

`RetryPolicy` の各パラメーターの意味:

| パラメーター | 値 | 意味 |
|---|---|---|
| `maxNumberOfAttempts` | `3` | 最大 3 回まで試行する (初回 + 再試行 2 回) |
| `firstRetryInterval` | `5` 秒 | 最初の再試行までの待機時間 |
| `backoffCoefficient` | `2.0` | 再試行のたびに待機時間を 2 倍に伸ばす (指数バックオフ) |

つまり「5 秒待って再試行 → ダメなら 10 秒待って再試行 → それでもダメなら失敗とする」という挙動になります。
**指数バックオフ** を使うことで、障害発生直後に一斉に再試行が集中して外部サービスをさらに圧迫してしまう
("thundering herd" 問題) ことを避けられます。

> 💡 **なぜ `FilterSaleAppsActivity` には適用されていないのか**
>
> [`FilterSaleApps`](../Activities/FilterSaleApps.cs) は外部 I/O を伴わない純粋な絞り込み処理です。
> 一時的に失敗する要因 (ネットワーク・外部サービスの調子など) がそもそも存在しないため、
> 再試行ポリシーを設定する意味がありません。「なんとなく全部に付ける」のではなく、
> **再試行が意味を持つ箇所にだけ適用する** という判断がされています。

このように、再試行ポリシーは「呼び出し元 (オーケストレーター) が、呼び出し先の特性に応じて指定する」
という形で表現されます。アクティビティ自身に再試行ロジックを書き込む必要がないのは、
Durable Functions が提供する大きな利点の 1 つです。

## 5.2 シングルトンオーケストレーターパターン

[`Triggers/Crawler.cs`](../Triggers/Crawler.cs) では、オーケストレーターを開始する前に
「すでに同じインスタンスが実行中でないか」を確認しています。

```csharp
string instanceId = $"{FunctionNames.CrawlerOrchestrator}-{profileId}";
OrchestrationMetadata? existingInstance = await client.GetInstanceAsync(instanceId);
if (existingInstance is { RuntimeStatus: OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending })
{
    logger.LogInformation(
        "Orchestration instance {instanceId} is already running (status: {status}). Skipping this run.",
        instanceId, existingInstance.RuntimeStatus);
    return;
}

await client.ScheduleNewOrchestrationInstanceAsync(
    FunctionNames.CrawlerOrchestrator,
    profileId,
    new StartOrchestrationOptions(InstanceId: instanceId));
```

### なぜこれが必要なのか

`TimerTrigger` は「指定したスケジュールで実行する」だけであり、
**前回の実行が終わっているかどうかは保証してくれません**。

例えば、何らかの理由で 1 回の実行が 1 時間以上かかってしまった場合、
次の `TimerTrigger` が発火すると「前回のオーケストレーションがまだ動いているのに、
新しいオーケストレーションが起動してしまう」ということが起こり得ます。

これが起きると、

- Steam / Discord / IsThereAnyDeal などの外部 API に対して二重にリクエストが飛ぶ (レート制限を消費する)
- `NotificationStateEntity` に対して同時に書き込みが発生し、意図しない順序で状態が更新される
- Discord に同じ内容の通知が二重に送信される

といった問題につながります。

### 固定の `InstanceId` による実現方法

これを防ぐ鍵が `InstanceId` です。Durable Functions のオーケストレーションインスタンスは、
インスタンス ID によって一意に識別されます。何も指定しなければランダムな ID が自動生成されますが、
本プロジェクトでは `$"{FunctionNames.CrawlerOrchestrator}-{profileId}"`
(例: `CrawlerOrchestrator-76561198072825180`) という **固定の ID** を明示的に指定しています。

固定の ID にすることで、

1. `client.GetInstanceAsync(instanceId)` で「同じ ID のインスタンスが今どんな状態か」を問い合わせられる
2. その状態が `Running` (実行中) または `Pending` (開始待ち) であれば、新規開始をスキップする
3. すでに完了 (`Completed`) または失敗 (`Failed`) していれば、新しいオーケストレーションとして開始する

という「**同時に 1 つしか実行させない**」制御が可能になります。これを **シングルトンオーケストレーターパターン**
と呼びます。プロフィール ID を ID の一部に含めているのは、将来的に複数プロフィールを監視する拡張をしても
プロフィールごとに独立したシングルトンとして扱えるようにするためです。

## 5.3 ロガーの使い分け

本プロジェクトでは、ロガーの取得方法が「どこで使われるか」によって明確に使い分けられています。

| 場所 | 取得方法 | 理由 |
|---|---|---|
| トリガー / アクティビティ / エンティティ | コンストラクター注入の `ILogger<T>` | 通常の DI コンテナーから供給される、ごく標準的な方法 |
| オーケストレーター | `context.CreateReplaySafeLogger(...)` | リプレイ中の重複ログ出力を抑制する必要があるため |

### コンストラクター注入の `ILogger<T>`

[`Triggers/Crawler.cs`](../Triggers/Crawler.cs) や各アクティビティでは、以下のように
プライマリコンストラクターで `ILogger<T>` を受け取っています。

```csharp
public class Crawler(IConfiguration configuration, ILogger<Crawler> logger)
```

これは [01-azure-functions-fundamentals.md](./01-azure-functions-fundamentals.md) で説明した
「ホストが標準で DI コンテナーに登録してくれているサービスを、コンストラクターで受け取るだけで使える」
という仕組みそのものです。`executionContext.GetLogger(...)` のように `FunctionContext` から
都度取得する方法もありますが、コンストラクター注入の方が

- クラスの依存関係が宣言の時点で明確になる (テスト時にモックを注入しやすい)
- `FunctionContext` を引数に取る必要がなくなり、メソッドのシグネチャがすっきりする

という利点があるため、本プロジェクトではこちらに統一されています。

### オーケストレーターだけは `CreateReplaySafeLogger`

一方、オーケストレーターでは話が変わります。
[02-durable-functions-fundamentals.md](./02-durable-functions-fundamentals.md#決定論性determinismという制約)
で説明した通り、オーケストレーターのコードは「アクティビティの完了のたびに最初から再実行 (リプレイ) される」
という特殊な実行モデルの上で動いています。

もし通常の `ILogger<T>` をオーケストレーター内でそのまま使うと、リプレイが起きるたびに
**同じログが何度も出力されてしまい**、ログが大量に汚染されます。これを防ぐために、
`TaskOrchestrationContext` が提供する `CreateReplaySafeLogger` を使います。

```csharp
ILogger logger = context.CreateReplaySafeLogger(nameof(WatchWishlistOrchestrator));
```

このメソッドで取得したロガーは、「リプレイ中かどうか」を自動的に判定し、
リプレイ中であればログ出力を抑制してくれます。「オーケストレーター内で何かをログに残したいときは、
必ず `context` 経由でロガーを取得する」と覚えておくとよいでしょう。

## 5.4 `IHttpClientFactory` による `HttpClient` の管理

[`Program.cs`](../Program.cs) では、以下のように `HttpClient` を DI に登録しています。

```csharp
// Steam / Discord / IsThereAnyDeal などへの HTTP アクセスに使用する HttpClient を DI に登録する
builder.Services.AddHttpClient();
```

そして各アクティビティでは、`new HttpClient()` ではなく `IHttpClientFactory` 経由で取得しています。

```csharp
public class GetAppDetails(IHttpClientFactory httpClientFactory, ILogger<GetAppDetails> logger)
{
    // ...
    HttpClient client = httpClientFactory.CreateClient(nameof(GetAppDetails));
```

### なぜ `new HttpClient()` を避けるのか

`HttpClient` は `IDisposable` を実装しているため、「使い終わったら `using` で破棄する」のが
一見正しい使い方に見えます。しかし `HttpClient` を頻繁に `new` して破棄すると、
基盤となるソケットがすぐには解放されず、再利用もされないために枯渇してしまう
(**ソケット枯渇問題**, socket exhaustion) ことが知られています。
特に本プロジェクトのように 1 時間ごとに何百もの外部 API 呼び出しを行うアプリケーションでは、
深刻な問題に発展しかねません。

`IHttpClientFactory` は、内部で `HttpMessageHandler` をプールして再利用しつつ、
DNS の変更などにも追従できるようにした、.NET が公式に提供する解決策です。
`builder.Services.AddHttpClient()` のように DI に登録しておけば、
利用側は「コンストラクターで `IHttpClientFactory` を受け取り、`CreateClient(名前)` で取得するだけ」
で済み、ソケット管理について意識する必要がなくなります。

`CreateClient(nameof(GetAppDetails))` のように **クラス名を名前付きクライアントの名前として使う**
ことで、将来的にクラスごとに異なる設定 (タイムアウトやベース URL など) を `AddHttpClient("名前", ...)`
で個別にカスタマイズできる余地も残しています。

## 5.5 `FunctionNames` の定数を直接参照する理由

[`Common/FunctionNames.cs`](../Common/FunctionNames.cs) には、各 Function 名が定数として定義されています。

```csharp
public class FunctionNames
{
    public const string RunCrawler = "RunCrawler";
    public const string CrawlerOrchestrator = "CrawlerOrchestrator";
    public const string GetWishlistAppIdsActivity = "GetWishlistAppIdsActivity";
    // ...
}
```

そして `[Function(...)]` 属性や `CallActivityAsync` の呼び出しでは、`nameof()` ではなく
**この定数を直接参照** しています。

```csharp
[Function(FunctionNames.GetWishlistAppIdsActivity)]
public async Task<List<long>> GetWishlistAppIdsActivity([ActivityTrigger] string profileId)
```

一見 `nameof(GetWishlistAppIds.GetWishlistAppIdsActivity)` のように書いた方が
「リファクタリング時に名前が自動追従する」ように思えるかもしれません。
しかし、これは **罠** です。

### `nameof()` の落とし穴

`nameof(式)` は、式が指す **シンボルの名前そのもの** (識別子の文字列表現) を返すのであって、
定数に代入されている **値** を返すわけではありません。

```csharp
public class FunctionNames
{
    public const string GetWishlistAppIdsActivity = "GetWishlistAppIdsActivity";
}

// nameof(FunctionNames.GetWishlistAppIdsActivity) は
// "GetWishlistAppIdsActivity" という、定数の "値" と "メンバー名" がたまたま
// 一致しているために、誤って正しく動いているように見えてしまう
```

つまり、もし将来誰かが「定数の値だけをリネームしたい」(例えば `GetWishlistAppIdsActivity = "FetchWishlistAppIds"`
のように、Functions の表示名だけを変えたい) と思って値を変更しても、
`nameof(FunctionNames.GetWishlistAppIdsActivity)` の評価結果は `"GetWishlistAppIdsActivity"` のまま変わりません。
その結果、`[Function]` 属性に登録された名前と `CallActivityAsync` で指定する名前が食い違い、
**実行時に「該当する Function が見つからない」というエラーになって初めて気付く** という、
発見しづらいバグを生んでしまいます。

定数 `FunctionNames.GetWishlistAppIdsActivity` を直接参照していれば、定数の値を変更した瞬間に
参照している側すべてに変更が反映されるため、このような不整合は原理的に起こり得ません。
「コンパイル時に名前のブレを防ぎたい」という目的に対しては、`nameof()` よりも
**値を持つ定数を直接参照する方が適切** であるという判断がされています。

## 5.6 Fan-out のレート制御

[02-durable-functions-fundamentals.md](./02-durable-functions-fundamentals.md#25-fan-outfan-in-パターン)
で紹介した Fan-out/Fan-in パターンの最小限の例は、次のように「対象をすべて同時に Fan-out する」ものでした。

```csharp
IEnumerable<Task<AppDetails?>> appDetailsTasks = appIds.Select(appId =>
    context.CallActivityAsync<AppDetails?>(FunctionNames.GetAppDetailsActivity, appId, ActivityRetryOptions));
AppDetails?[] appDetailsResults = await Task.WhenAll(appDetailsTasks);
```

しかし、実際の [`WatchWishlistOrchestrator.cs`](../Orchestrations/WatchWishlistOrchestrator.cs) では、
ステップ 2 (`GetAppDetailsActivity`) とステップ 6 (`GetLowestPriceActivity`) の Fan-out が、
もう一段踏み込んだ形で実装されています。

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
```

### なぜ「全件同時に Fan-out」では不十分なのか

ウィッシュリストの登録数は実行時にならないとわからない、可変の値です。
もし全件を一度に Fan-out すると、登録数が多いほど **瞬間的に大量のリクエストが外部 API に押し寄せる**
ことになります。これは、呼び出し先がレート制限 (一定時間あたりのリクエスト数の上限) を持つ
外部サービスである場合、致命的な問題につながります。

実際に、本プロジェクトが利用している外部 API の制限を調べると、以下のようになっています
(2026 年時点の調査結果)。

| 呼び出し先 | レート制限 | 換算 (おおよその秒間リクエスト数) |
|---|---|---|
| Steam ストア `appdetails` ([`GetAppDetails`](../Activities/GetAppDetails.cs) が利用) | 約 200 リクエスト / 5 分 | 約 0.67 req/sec (≒ 1.5 秒に 1 件) |
| IsThereAnyDeal ([`GetLowestPrice`](../Activities/GetLowestPrice.cs) が利用) | 約 1000 リクエスト / 5 分 (メール認証済みアカウント) | 約 3.3 req/sec |
| Discord Webhook ([`SendDiscordNotification`](../Activities/SendDiscordNotification.cs) が利用) | 5 リクエスト / 2 秒 (Webhook 単位) | 約 2.5 req/sec |

特に Steam の `appdetails` は **1 リクエストあたり平均 1.5 秒** というかなり厳しい制限です。
しかも、このエンドポイントは 2015 年の仕様変更により **複数の App ID をまとめて 1 リクエストで
取得することができません** (`appids=730,440` のようなカンマ区切り指定は機能しなくなっています)。
つまり「リクエストの回数自体を減らす」という選択肢は取れず、
**1 件ずつ呼び出さざるを得ないリクエストの実効レートを、呼び出し側で能動的に制御する**
以外に方法がありません。

### 「同時実行数を絞るだけ」でも不十分

では「`Task.WhenAll` で待つ件数を絞ればよいのでは?」と考えるかもしれませんが、それだけでは
不十分です。例えば 3 件ずつ Fan-out したとしても、3 件が完了し次第すぐに次の 3 件を Fan-out すれば、
結局は「3 件のバーストが連続する」だけで、**平均的なリクエストレートはほとんど下がりません**。

レート制限は「一定時間あたりの回数」という形で課されるものなので、
これに対抗するには「同時実行数」ではなく **「単位時間あたりに送出するリクエスト数 (= レート)」**
そのものを制御する必要があります。

### `Chunk` + `context.CreateTimer` による実効レートの制御

そこで本プロジェクトでは、次の 2 つを組み合わせています。

1. **`IEnumerable<T>.Chunk(N)`** で対象を `N` 件ずつのチャンクに分割する
2. チャンクを 1 つ処理し終えるたびに、**`context.CreateTimer(...)` で一定時間待機してから次のチャンクへ進む**

```csharp
private const int MaxFanOutConcurrency = 3;

private static readonly TimeSpan AppDetailsFanOutInterval = TimeSpan.FromSeconds(5);
private static readonly TimeSpan LowestPriceFanOutInterval = TimeSpan.FromSeconds(2);
```

`MaxFanOutConcurrency` 件 (= 3 件) のチャンクを `AppDetailsFanOutInterval` (= 5 秒) ごとに送出すると、
平均レートは「3 件 ÷ 5 秒 = 約 0.6 req/sec」となり、Steam `appdetails` の制限 (約 0.67 req/sec) を
わずかに下回ります。同様に `GetLowestPriceActivity` 側も、IsThereAnyDeal の制限 (約 3.3 req/sec) を
踏まえつつ「1 回のアクティビティ呼び出しで最大 2 回の HTTP リクエストが発生し得る」という
内部実装を考慮し、余裕を持った間隔 (2 秒) を設定しています。**呼び出し先ごとに別々の定数として
間隔を定義している** のは、レート制限の厳しさが API ごとに異なるためです。

> 💡 **なぜ `Task.Delay` ではなく `context.CreateTimer` を使うのか**
>
> [02-durable-functions-fundamentals.md](./02-durable-functions-fundamentals.md#決定論性determinismという制約)
> で説明した通り、オーケストレーター内でスレッドをブロックする `Task.Delay` / `Thread.Sleep` を
> 使うことはできません。`context.CreateTimer(指定した日時, キャンセルトークン)` は、
> Durable Functions のランタイムにスケジュールされる「永続化されたタイマー」であり、
> リプレイされても同じ結果になる (= 決定論的である) ため、オーケストレーター内で安全に
> 待機を表現できます。

### トレードオフ: 全体の処理時間が伸びる

この対策には、当然ながら代償もあります。チャンクの間に待機を挟む分、
**全体の処理時間が延びる** ということです。例えばウィッシュリストが 100 件登録されている場合、

```
100 件 ÷ 3 件/チャンク ≈ 34 チャンク
34 チャンク × 5 秒 ≈ 170 秒 (約 2.8 分)
```

程度の時間が `GetAppDetailsActivity` の Fan-out だけでかかる計算になります。
本プロジェクトのタイマートリガーは毎時 0 分に実行されるため、この程度の所要時間増加は
許容範囲内と判断できます。また、[5.2](#52-シングルトンオーケストレーターパターン) で説明した
シングルトンオーケストレーターパターンのおかげで、処理が長引いて次回のタイマー発火と
重なってしまっても、二重実行が防止される仕組みになっています。

「外部 API のレート制限を守りながら、許容できる範囲で処理時間を抑える」という、
**正しさと実用性のバランスを取った設計判断** の一例として捉えるとよいでしょう。

## まとめ

- 再試行ポリシーは「外部 I/O を伴うアクティビティ」にだけ、指数バックオフ付きで設定する
- シングルトンオーケストレーターパターンによって、タイマーの多重発火による二重実行を防ぐ
- ロガーは「どこで使われるか」によって取得方法を使い分ける (オーケストレーターは必ず `CreateReplaySafeLogger`)
- `HttpClient` はソケット枯渇を避けるため、必ず `IHttpClientFactory` 経由で取得する
- Function 名は `nameof()` ではなく、値を持つ定数 (`FunctionNames`) を直接参照する
- Fan-out では「同時実行数を絞る」だけでなく、`Chunk` + `context.CreateTimer` で
  **実効的なリクエストレートそのもの** を制御することで、外部 API のレート制限を守る

次は [06-local-development.md](./06-local-development.md) で、
このプロジェクトをローカル環境で動かし、デバッグする方法を見ていきます。
