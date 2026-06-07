# 1. Azure Functions の基礎

Durable Functions の話に入る前に、まず土台となる **Azure Functions** の基本概念を押さえておきましょう。

## 1.1 Azure Functions とは

Azure Functions は、Microsoft Azure が提供する **サーバーレスコンピューティング** サービスです。
「サーバーレス」とは、サーバーの管理 (OS のセットアップやスケーリング) を意識せずに、
**処理したい関数 (Function) を書くだけ** でアプリケーションを動かせる、という考え方を指します。

特徴:

- **イベント駆動**: HTTP リクエスト、タイマー、メッセージキューなど、何らかの「イベント」をきっかけに処理が実行される
- **従量課金**: 実行された分だけ課金される (常時起動するサーバーが不要)
- **自動スケーリング**: 負荷に応じて実行環境が自動的に増減する

## 1.2 Function (関数) の構成要素

Azure Functions における 1 つの「Function」は、大きく以下の要素で構成されます。

### トリガー (Trigger)

その Function が **いつ実行されるか** を決めるものです。1 つの Function には必ず 1 つのトリガーが必要です。

本プロジェクトで使われているトリガー:

| トリガー | 説明 | 使用箇所 |
|---|---|---|
| `TimerTrigger` | 指定したスケジュール (cron 形式) で定期実行する | [`Triggers/Crawler.cs`](../Triggers/Crawler.cs) |
| `OrchestrationTrigger` | Durable Functions のオーケストレーターを起動する (Durable Functions 拡張機能が提供) | [`Orchestrations/WatchWishlistOrchestrator.cs`](../Orchestrations/WatchWishlistOrchestrator.cs) |
| `ActivityTrigger` | Durable Functions のアクティビティを起動する (同上) | `Activities/*.cs` |
| `EntityTrigger` | Durable Functions のエンティティを起動する (同上) | [`Entities/NotificationStateEntity.cs`](../Entities/NotificationStateEntity.cs) |

例えば `Triggers/Crawler.cs` では、以下のように `[TimerTrigger("0 0 * * * *")]` という属性 (Attribute) を
引数に付けることで「毎時 0 分に実行する」という意味になります。

```csharp
[Function(FunctionNames.RunCrawler)]
public async Task RunCrawler(
    [TimerTrigger("0 0 * * * *")] TimerInfo myTimer,
    [DurableClient] DurableTaskClient client)
```

> 💡 `[Function("関数名")]` という属性が、その関数が Azure Functions のエントリーポイントであることを表します。
> 関数名は実行ログや管理画面で識別子として使われます。本プロジェクトでは、誤りに気づきにくい `nameof()`
> ではなく [`Common/FunctionNames.cs`](../Common/FunctionNames.cs) の定数を直接参照することで、
> 名前のブレをなくしています (詳しくは [05-best-practices.md](./05-best-practices.md) で解説します)。

### バインディング (Binding)

Function の入出力先 (例: Blob Storage、Queue、別の Function など) を、
**コードを書かずに宣言的に結び付ける** 仕組みです。トリガーもバインディングの一種 (入力バインディング) です。

例えば `[DurableClient] DurableTaskClient client` は「Durable Functions を操作するクライアントを注入してほしい」
という入力バインディングです。これにより、オーケストレーターを開始する `client.ScheduleNewOrchestrationInstanceAsync(...)`
のような操作が行えるようになります。

### cron 形式のスケジュール表現

`TimerTrigger` に渡す `"0 0 * * * *"` は、NCronTab 形式 (6 フィールド: 秒 分 時 日 月 曜日) のスケジュール表現です。

```
"0 0 * * * *"
 │ │ │ │ │ │
 │ │ │ │ │ └─ 曜日 (* = 毎日)
 │ │ │ │ └─── 月   (* = 毎月)
 │ │ │ └───── 日   (* = 毎日)
 │ │ └─────── 時   (* = 毎時)
 │ └───────── 分   (0  = 0 分)
 └─────────── 秒   (0  = 0 秒)
```

つまり「毎時 0 分 0 秒に実行する」という設定です。

## 1.3 分離ワーカーモデル (Isolated Worker Model)

.NET 向けの Azure Functions には、大きく分けて 2 つの実行モデルがあります。

- **インプロセスモデル**: Functions ホスト (実行基盤) と同じプロセス内でユーザーコードが動く (.NET 旧バージョン向け)
- **分離ワーカーモデル (Isolated Worker Model)**: ユーザーコードが Functions ホストとは別プロセスで動き、
  gRPC 経由で通信する

本プロジェクトは **分離ワーカーモデル** を採用しています (`.csproj` の `<OutputType>Exe</OutputType>` や
`Program.cs` の構成からもわかります)。分離ワーカーモデルには、以下のような利点があります。

- 使用する .NET のバージョンを Functions ホストのバージョンに縛られず選択できる (本プロジェクトは .NET 10 を使用)
- 標準的な ASP.NET Core 系の DI (依存性注入) コンテナーやミドルウェアの仕組みがそのまま使える
- Functions ホストのプロセスから独立しているため、クラッシュ等の影響を受けにくい

## 1.4 エントリーポイントと DI 登録 (`Program.cs`)

分離ワーカーモデルのアプリケーションは、通常のコンソールアプリケーションのように
[`Program.cs`](../Program.cs) から起動します。

```csharp
var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Steam / Discord / IsThereAnyDeal などへの HTTP アクセスに使用する HttpClient を DI に登録する
builder.Services.AddHttpClient();

// (OpenTelemetry の設定 ... )

builder.Build().Run();
```

ポイントは `builder.Services` です。これは ASP.NET Core でもおなじみの **DI (Dependency Injection: 依存性注入) コンテナー**
への登録口です。ここに登録したサービスは、各 Function クラスのコンストラクターで受け取る (注入してもらう) ことができます。

### `IHttpClientFactory` を使う理由

本プロジェクトの各 Activity では、外部 API (Steam、Discord、IsThereAnyDeal など) を HTTP で呼び出します。
その際、`new HttpClient()` ではなく `IHttpClientFactory` 経由で取得する設計になっています。

```csharp
public class GetAppDetails(IHttpClientFactory httpClientFactory, ILogger<GetAppDetails> logger)
{
    // ...
    HttpClient client = httpClientFactory.CreateClient(nameof(GetAppDetails));
```

`HttpClient` を毎回 `new` すると、ソケットが再利用されずに枯渇する (ソケット枯渇問題) ことが知られています。
`IHttpClientFactory` はこの問題を解消するために .NET が提供している仕組みで、
`builder.Services.AddHttpClient()` のように DI に登録しておくことで、
あとはコンストラクターで受け取るだけで安全に `HttpClient` を取得できるようになります。

## 1.5 コンストラクターインジェクションと DI

C# 12 から導入された **プライマリコンストラクター** という構文を使うと、
クラス宣言の括弧内に書いたパラメーターを、そのままクラス全体で利用できるフィールドのように扱えます。
本プロジェクトの各 Function クラスは、ほぼすべてこの形式で依存サービスを受け取っています。

```csharp
public class Crawler(IConfiguration configuration, ILogger<Crawler> logger)
{
    [Function(FunctionNames.RunCrawler)]
    public async Task RunCrawler(/* ... */)
    {
        logger.LogInformation(/* ... */);
        string profileId = configuration["STEAM_PROFILE_ID"]
            ?? throw new InvalidOperationException("STEAM_PROFILE_ID is not configured");
```

- `IConfiguration`: `local.settings.json` (ローカル) や Azure の「アプリケーション設定」(本番) から
  設定値を読み取るためのインターフェース
- `ILogger<T>`: ログ出力用のインターフェース (詳しくは [05-best-practices.md](./05-best-practices.md) で解説)

これらは Azure Functions のホストが標準で DI コンテナーに登録してくれているため、
利用側は「コンストラクターで受け取るだけ」で済みます。

## まとめ

- Azure Functions は「トリガー」をきっかけに実行される、サーバーレスな関数の集まり
- バインディングを使うと、外部リソースとのやり取りを宣言的に書ける
- 本プロジェクトは「分離ワーカーモデル」を採用しており、`Program.cs` で DI を構成する
- `IHttpClientFactory` や `ILogger<T>` のようなサービスは、コンストラクターで受け取って利用する

次は [02-durable-functions-fundamentals.md](./02-durable-functions-fundamentals.md) で、
本プロジェクトの中核である **Durable Functions** について学びます。
