# 6. ローカル環境での動かし方・デバッグ方法

最後に、本プロジェクトを実際に手元の環境で動かし、動作を確認する方法を見ていきます。
「ドキュメントを読んで理解した気になる」だけでなく、実際に動かしてみることで理解が深まります。

## 6.1 必要なもの

- [.NET SDK](https://dotnet.microsoft.com/) (本プロジェクトは .NET 10 を使用)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) (`func` コマンド)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (Azure Storage のローカルエミュレーター)

## 6.2 Azurite: ローカルの Azure Storage エミュレーター

Durable Functions は、オーケストレーションの実行履歴やエンティティの状態を
**Azure Storage** (Blob / Queue / Table) に永続化する仕組みになっています
([`host.json`](../host.json) の `"durableTask": { "storageProvider": { "type": "AzureStorage" } }` の部分)。

ローカル開発でわざわざ Azure 上に Storage アカウントを用意しなくても済むように、
**Azurite** という Storage の互換エミュレーターが提供されています。
これを起動しておくことで、ローカルマシン上だけで Durable Functions を完結させて動かせます。

```powershell
# npm 経由でインストールする場合
npm install -g azurite

# 起動 (デフォルトではカレントディレクトリにデータを保存する)
azurite
```

起動すると、Blob (既定で `10000` 番ポート) / Queue (`10001`) / Table (`10002`) の
3 つのエンドポイントがローカルで待ち受け状態になります。

> 💡 実行すると `__blobstorage__` や `__queuestorage__` といったディレクトリが生成されます。
> これらは Azurite が永続化に使う作業ディレクトリで、Git の管理対象には含めません。

## 6.3 `local.settings.json`: ローカル専用の設定ファイル

[`local.settings.json`](../local.settings.json) は、ローカル実行時にのみ読み込まれる設定ファイルです
(本番の Azure 環境では「アプリケーション設定」が代わりに使われます)。

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "STEAM_PROFILE_ID": "<Steam の SteamID64 (数値)>",
    "DISCORD_WEBHOOK_URL": "<Discord Webhook の URL>",
    "ITAD_API_KEY": "<IsThereAnyDeal の API キー>"
  }
}
```

各キーの意味:

| キー | 説明 |
|---|---|
| `AzureWebJobsStorage` | Durable Functions が状態の永続化に使う Storage への接続文字列。`"UseDevelopmentStorage=true"` と書くと「ローカルの Azurite を使う」という意味になる |
| `FUNCTIONS_WORKER_RUNTIME` | ワーカーの実行ランタイム。.NET の分離ワーカーモデルでは `"dotnet-isolated"` を指定する |
| `STEAM_PROFILE_ID` | 監視対象のウィッシュリストを持つアカウントの **SteamID64** (数値の ID)。[`Triggers/Crawler.cs`](../Triggers/Crawler.cs) で `configuration["STEAM_PROFILE_ID"]` として読み出される |
| `DISCORD_WEBHOOK_URL` | 通知を送信する Discord チャンネルの Webhook URL。[`SendDiscordNotification`](../Activities/SendDiscordNotification.cs) で使われる |
| `ITAD_API_KEY` | 過去最安値の取得に使う [IsThereAnyDeal](https://isthereanydeal.com/) の API キー。[`GetLowestPrice`](../Activities/GetLowestPrice.cs) で使われる |

> ⚠️ **`local.settings.json` は Git に含めない**
>
> この設定ファイルには Webhook URL や API キーといった機密情報を含めることになるため、
> [`.gitignore`](../.gitignore) で明示的に除外されています。誤ってコミットしないよう注意してください。
> リポジトリを clone した直後はこのファイルが存在しないため、上記の構造を参考に手動で作成する必要があります。

`STEAM_PROFILE_ID` には、ウィッシュリストページの URL に含まれるカスタム URL 名 (vanity name) ではなく、
**数値の SteamID64** を指定する必要がある点に注意してください
([`GetWishlistAppIds`](../Activities/GetWishlistAppIds.cs) が呼び出す
公式 API `IWishlistService/GetWishlist/v1` の制約によるものです)。
SteamID64 は [steamid.io](https://steamid.io/) のようなツールで調べられます。

## 6.4 `func start`: ローカルでの起動

設定が揃ったら、プロジェクトのルートディレクトリで以下を実行します。

```powershell
# Azurite を別ターミナルで起動しておく
azurite

# Functions ホストを起動する
func start
```

起動に成功すると、コンソールに `[Function]` 属性で定義された各 Function の一覧
(`RunCrawler`、`CrawlerOrchestrator`、`GetWishlistAppIdsActivity` など) が表示され、
ホストが待ち受け状態になります。

`RunCrawler` は `[TimerTrigger("0 0 * * * *")]` (毎時 0 分) で動作するため、
何もしなければ次の「正時」になるまでオーケストレーションは開始されません。
動作確認のためにそこまで待つのは現実的ではないため、次節の方法で **手動でトリガー** します。

## 6.5 Function を手動でトリガーする

Azure Functions のローカルホストは、管理用の HTTP エンドポイントを提供しています。
これを使うと、タイマーの発火を待たずに任意のタイミングで Function を実行できます。

```powershell
# RunCrawler を即座に実行する
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:7071/admin/functions/RunCrawler" `
  -ContentType "application/json" `
  -Body '{ "input": "" }'
```

これを実行すると `RunCrawler` (タイマートリガー) が起動し、内部で
`client.ScheduleNewOrchestrationInstanceAsync` が呼び出されてオーケストレーションが開始されます。

## 6.6 オーケストレーションの実行状況を確認する

Durable Functions は、オーケストレーションインスタンスの状態を問い合わせるための
HTTP エンドポイントも提供しています。

```powershell
# instanceId は Crawler.cs で組み立てられる
# "{FunctionNames.CrawlerOrchestrator}-{profileId}" の形式 (例: CrawlerOrchestrator-76561198072825180)
$instanceId = "CrawlerOrchestrator-<STEAM_PROFILE_ID の値>"

Invoke-RestMethod -Method Get `
  -Uri "http://localhost:7071/runtime/webhooks/durabletask/instances/$instanceId"
```

レスポンスの `runtimeStatus` フィールドを見ることで、現在の状態が分かります。

| `runtimeStatus` | 意味 |
|---|---|
| `Pending` | 開始待ち |
| `Running` | 実行中 |
| `Completed` | 正常に完了した |
| `Failed` | 失敗して終了した |

`Completed` になっていれば、ウィッシュリストの取得からセールの抽出、過去最安値の取得、
Discord への通知 (初回実行時はスキップ)、状態の記録までの一連の流れが正常に完了したことを意味します。

途中で `Failed` になった場合は、`func start` を実行しているコンソールに出力される
ログ (各アクティビティの `ILogger` 出力) を確認することで、どの段階でどんなエラーが発生したかを
追跡できます。

## 6.7 デバッグのヒント

- **初回実行時は Discord に通知が飛ばない**: [03-architecture-and-flow.md](./03-architecture-and-flow.md#ステップ-7-discord-に通知-初回実行時を除く)
  で説明した通り、`snapshot.IsFirstRun` が `true` の間は通知がスキップされ、状態の記録だけが行われます。
  「2 回目以降の実行で初めて Discord 通知が送られる」という前提でデバッグを進めてください。
  状態をリセットして「初回実行」の挙動を再現したい場合は、Azurite のデータ
  (`__blobstorage__` / `__queuestorage__` ディレクトリ、もしくは `azurite` 起動時に指定した保存先) を削除して、
  まっさらな状態から起動し直します。
- **同じインスタンス ID のオーケストレーションは再利用される**: [05-best-practices.md](./05-best-practices.md#52-シングルトンオーケストレーターパターン)
  で説明したシングルトンパターンにより、`Running`/`Pending` 状態のインスタンスがあると新規実行はスキップされます。
  「手動トリガーしたのに何も起きない」と感じたら、まず現在のインスタンスの状態を確認してみてください。
- **外部 API のレート制限に注意する**: Steam・IsThereAnyDeal・CheapShark などの外部 API には、
  短時間に大量のリクエストを送るとレート制限 (HTTP 429 など) がかかることがあります。
  動作確認のために何度も手動トリガーを繰り返す際は、間隔を空けるなど配慮してください。
  (本プロジェクトに [5.1 で説明した再試行ポリシー](./05-best-practices.md#51-アクティビティのリトライポリシー)
  が実装されているのは、まさにこうした一時的なエラーに対応するためです。)

## まとめ

- Durable Functions のローカル実行には Azurite (Storage エミュレーター) が必要
- `local.settings.json` に各種シークレット・設定値を記述する (Git 管理対象外)
- `func start` でホストを起動し、`/admin/functions/{関数名}` への POST で手動トリガーできる
- `/runtime/webhooks/durabletask/instances/{instanceId}` でオーケストレーションの状態を確認できる
- 初回実行時は Discord 通知がスキップされる、シングルトンパターンにより多重起動が抑制される、
  といった「コードを読んだだけでは気づきにくい」挙動を踏まえてデバッグする

---

これで、本ドキュメントシリーズは完結です。
[README.md](./README.md) の冒頭に立ち返り、もう一度全体像を俯瞰してみると、
最初に読んだときよりも理解が深まっていることに気づくはずです。
