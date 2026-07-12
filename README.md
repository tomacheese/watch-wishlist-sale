# watch-wishlist-sale

Steam のウィッシュリストを監視し、セール中かつ値下がりしたアプリを Discord に通知するサーバーレスアプリケーションです。

## 機能

- Steam ウィッシュリストを毎時 1 回巡回
- セール中 (かつ前回通知時から値下がりした) アプリを検出
- 過去最安値 ([IsThereAnyDeal](https://isthereanydeal.com/) 経由) と比較した情報を含めて Discord に通知
- 通知済み状態を Durable Entity で永続化し、重複通知を防止

## 必要要件

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) (ローカル実行時)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (ローカル実行時、Azure Storage エミュレーター)
- Discord Webhook URL / Steam SteamID64 / IsThereAnyDeal API キー

## セットアップ

```bash
git clone https://github.com/tomacheese/watch-wishlist-sale.git
cd watch-wishlist-sale
dotnet restore
```

`local.settings.json` の作成が必要です。詳細は [docs/local-development.md](docs/local-development.md) を参照してください。

## 設定

主な環境変数は次の通りです。詳細は [docs/configuration.md](docs/configuration.md) を参照してください。

| キー | 説明 |
|---|---|
| `AzureWebJobsStorage` | Durable Functions の状態永続化に使う Storage への接続文字列 |
| `FUNCTIONS_WORKER_RUNTIME` | ワーカーの実行ランタイム (`dotnet-isolated`) |
| `STEAM_PROFILE_ID` | 監視対象のウィッシュリストを持つアカウントの SteamID64 |
| `DISCORD_WEBHOOK_URL` | 通知を送信する Discord チャンネルの Webhook URL |
| `ITAD_API_KEY` | IsThereAnyDeal の API キー |

## アーキテクチャ

処理フローやフォルダー構成については [docs/architecture.md](docs/architecture.md) を参照してください。

## デプロイ

`main`/`master` ブランチへの push をトリガーに、GitHub Actions が Azure Functions へ自動デプロイします。詳細は [docs/deployment.md](docs/deployment.md) を参照してください。

## ライセンス

このプロジェクトは [MIT](LICENSE) ライセンスの下で公開されています。
