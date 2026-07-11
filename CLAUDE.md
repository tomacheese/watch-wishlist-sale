# CLAUDE.md

## 概要

`WatchWishlistSale` は **Azure Durable Functions**(.NET 10、isolated worker モデル)で構築されたサーバーレスのバッチアプリです。毎時実行され、Steam のウィッシュリストを巡回し、セール中かつ前回通知時から値下がりしたアプリを検出して Discord に通知します。歴代最安値は IsThereAnyDeal / CheapShark から取得します。

## 開発コマンド

- `dotnet restore` — 依存関係の復元。
- `dotnet build --configuration Release` — ビルド。コミット前に実行すること。CI も同一コマンドを実行する。ビルド警告はゼロに保つ(StyleCop.Analyzers と `GenerateDocumentationFile` の警告はレビューブロッカーとして扱う)。
- `func start` — ローカル実行。別ターミナルで **Azurite**(ストレージエミュレータ)の起動と `local.settings.json` が必要(詳細は下記)。
- 毎時の実行を待たずにタイマーを手動発火する場合:
  `POST http://localhost:7071/admin/functions/RunCrawler` にボディ `{ "input": "" }` を送信。

テストプロジェクトは存在しない。変更の検証はビルドと、Azurite を使ったローカルでのフロー実行で行う(`docs/06-local-development.md` 参照)。

## アーキテクチャ

Durable Functions は処理を 3 つの役割に分割する。データは Trigger → Orchestrator → Activities と流れ、Entity が実行をまたいだ状態を保持する。

- `Triggers/` — エントリポイント。`Crawler.cs` は毎時実行される `TimerTrigger` で、オーケストレーションを開始する。
- `Orchestrations/` — `WatchWishlistOrchestrator.cs`。アクティビティを調整する(fan-out/fan-in)。**決定論的でなければならない**: `DateTime.Now`、乱数、直接の I/O、durable でない非同期処理は禁止。非決定的な処理はすべてアクティビティ経由で呼び出す。
- `Activities/` — 実際の処理(HTTP 呼び出し、フィルタリング、Discord 通知)。1 アクティビティ 1 クラス。
- `Entities/` — `NotificationStateEntity.cs`。通知済みセール状態を実行をまたいで永続化する Durable Entity。
- `Models/` — ドメインごとに分類された DTO: `Wishlist/`、`Pricing/`、`Notification/`。
- `Common/FunctionNames.cs` — 全 Function 名の定数を集約。
- `Program.cs` — DI/ホストのセットアップ(`IHttpClientFactory`、OpenTelemetry を登録)。

より詳細な設計とコードウォークスルーは `docs/` 配下にある(学習用ドキュメント)。

## コーディング規約

- **言語**: コード内コメントおよび XML doc コメントは **日本語**(既存ファイルに合わせる)、ログ・例外メッセージは **英語**。
- **XML doc 必須**: `GenerateDocumentationFile` が有効なため、public な型・メンバーには `///` summary が必要(無いとビルド警告になる)。
- **Function 名**: `[Function(...)]` 属性やオーケストレーション/アクティビティ/エンティティの呼び出しでは、文字列リテラルではなく `FunctionNames.*` 定数を参照する。
- **HTTP**: クライアントは注入された `IHttpClientFactory` から名前付きクライアント(`CreateClient(nameof(TheClass))`)として取得する。`new HttpClient()` は禁止。
- **モデル**: `record` 型を優先する。`Nullable` と `ImplicitUsings` が有効なため、null 許容性を正直に注釈し、implicit usings に依存する。
- **フォーマット**(`.editorconfig` で強制): 4 スペースインデント、**CRLF** 改行、UTF-8、ファイル末尾改行、allman ブレース。JSON/YAML/XML は 2 スペースインデント。

## シークレット・設定

- ローカル設定はリポジトリルートの `local.settings.json`。**gitignore 対象であり、絶対にコミットしない**。`STEAM_PROFILE_ID`(数値の SteamID64。vanity name ではない)、`DISCORD_WEBHOOK_URL`、`ITAD_API_KEY` を保持する。Azure 上ではアプリケーション設定から取得する。
- Webhook URL・API キー・プロファイル ID を直接コードに記述しない。`IConfiguration` 経由で取得する。
- デプロイは OIDC(`azure/login`)を使用する。リポジトリに長期的な Azure 認証情報は保存しない。

## ドキュメント更新ルール

- Function の追加・改名時 → `Common/FunctionNames.cs` と関連する `docs/` のウォークスルーを更新する。
- ローカルセットアップ、必要な設定キー、実行/デバッグ手順の変更時 → `docs/06-local-development.md` を更新する。
- オーケストレーションフローやアーキテクチャの変更時 → `docs/03-architecture-and-flow.md` を更新する。

## コミット

Conventional Commits に従う。説明は **日本語**(履歴に合わせる)。例: `feat: セール判定に最安値比較を追加`。
