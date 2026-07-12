# ローカル開発

## 必要なもの

- [.NET SDK](https://dotnet.microsoft.com/) (.NET 10)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) (`func` コマンド)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (Azure Storage のローカルエミュレーター)

## Azurite の起動

Durable Functions はオーケストレーションの実行履歴やエンティティの状態を Azure Storage (Blob / Queue / Table) に永続化します ([`host.json`](../host.json) の `durableTask.storageProvider.type: "AzureStorage"`)。ローカルでは Azurite を起動しておくことで、Azure 上に Storage アカウントを用意せずに完結させられます。

```bash
# npm 経由でインストールする場合
npm install -g azurite

# 起動 (デフォルトではカレントディレクトリにデータを保存する)
azurite
```

起動すると Blob (既定で `10000` 番ポート) / Queue (`10001`) / Table (`10002`) の 3 つのエンドポイントが待ち受け状態になります。`__blobstorage__` / `__queuestorage__` ディレクトリが生成されますが、これらは Git の管理対象外です。

## 設定ファイルの準備

[`local.settings.json`](../local.settings.json) を作成します。詳細は [configuration.md](configuration.md) を参照してください。

## `func start`: ローカルでの起動

```bash
# Azurite を別ターミナルで起動しておく
azurite

# Functions ホストを起動する
func start
```

起動に成功すると、`RunCrawler` / `CrawlerOrchestrator` / `GetWishlistAppIdsActivity` などの Function 一覧がコンソールに表示され、ホストが待ち受け状態になります。`RunCrawler` は `[TimerTrigger("0 0 * * * *")]` (毎時 0 分) で動作するため、動作確認のためには次項の方法で手動トリガーします。

## Function を手動でトリガーする

```bash
curl -X POST "http://localhost:7071/admin/functions/RunCrawler" \
  -H "Content-Type: application/json" \
  -d '{ "input": "" }'
```

これにより `RunCrawler` が起動し、`client.ScheduleNewOrchestrationInstanceAsync` が呼び出されてオーケストレーションが開始されます。

## オーケストレーションの実行状況を確認する

```bash
# instanceId は "{CrawlerOrchestrator}-{profileId}" の形式 (例: CrawlerOrchestrator-76561198072825180)
curl "http://localhost:7071/runtime/webhooks/durabletask/instances/CrawlerOrchestrator-<STEAM_PROFILE_ID の値>"
```

レスポンスの `runtimeStatus` フィールドで状態を確認できます。

| `runtimeStatus` | 意味 |
|---|---|
| `Pending` | 開始待ち |
| `Running` | 実行中 |
| `Completed` | 正常に完了した |
| `Failed` | 失敗して終了した |

## デバッグのヒント

- **初回実行時は Discord に通知が飛ばない**: `NotificationSnapshot.isFirstRun` が `true` の間は通知がスキップされ、状態の記録だけが行われます。状態をリセットして初回実行の挙動を再現したい場合は、Azurite のデータ (`__blobstorage__` / `__queuestorage__` ディレクトリ) を削除してから起動し直してください。
- **同じインスタンス ID のオーケストレーションは再利用される**: シングルトンパターンにより、`Running`/`Pending` 状態のインスタンスがあると新規実行はスキップされます。手動トリガーしても何も起きない場合は、まず現在のインスタンスの状態を確認してください。
- **外部 API のレート制限に注意する**: Steam・IsThereAnyDeal・CheapShark にはレート制限があります。動作確認のために何度も手動トリガーする際は間隔を空けてください。
