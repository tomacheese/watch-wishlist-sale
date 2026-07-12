# 設定リファレンス

## `local.settings.json`

ローカル実行時にのみ読み込まれる設定ファイルです (本番の Azure 環境では「アプリケーション設定」が代わりに使われ、GitHub Actions によるデプロイの対象外です)。`.gitignore` で除外されているため、リポジトリを clone した直後は存在しません。次の内容を参考に手動で作成してください。

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

## 環境変数一覧

| キー | 説明 | 参照箇所 |
|---|---|---|
| `AzureWebJobsStorage` | Durable Functions が状態の永続化に使う Storage への接続文字列。`"UseDevelopmentStorage=true"` はローカルの Azurite を使う指定 | Azure Functions ランタイム共通 |
| `FUNCTIONS_WORKER_RUNTIME` | ワーカーの実行ランタイム。.NET の分離ワーカーモデルでは `"dotnet-isolated"` を指定する | Azure Functions ランタイム共通 |
| `STEAM_PROFILE_ID` | 監視対象のウィッシュリストを持つアカウントの SteamID64 (数値)。カスタム URL 名 (vanity name) ではなく数値の ID が必要 | [`Triggers/Crawler.cs`](../Triggers/Crawler.cs) |
| `DISCORD_WEBHOOK_URL` | 通知を送信する Discord チャンネルの Webhook URL | [`Activities/SendDiscordNotification.cs`](../Activities/SendDiscordNotification.cs) |
| `ITAD_API_KEY` | 過去最安値の取得に使う [IsThereAnyDeal](https://isthereanydeal.com/) の API キー | [`Activities/GetLowestPrice.cs`](../Activities/GetLowestPrice.cs) |

`STEAM_PROFILE_ID` の SteamID64 は [steamid.io](https://steamid.io/) のようなツールで調べられます。

## 本番環境での設定

Azure 上ではこれらの値は Function App の「アプリケーション設定」として設定します。[`.github/workflows/azure-functions-deploy.yml`](../.github/workflows/azure-functions-deploy.yml) によるデプロイはコードのみを対象とし、アプリケーション設定の値自体はデプロイ対象に含まれません。
