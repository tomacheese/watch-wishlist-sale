# GitHub Copilot コードレビュー向け指示

本リポジトリは Steam のウィッシュリストを巡回し値下がりを Discord に通知する、.NET 10 Azure Durable Functions アプリ(isolated worker)です。C# の変更は以下の観点でレビューしてください。

## Durable Functions の正しさ(最優先)

- オーケストレータ(`Orchestrations/`)内の非決定的なコードを指摘する: `DateTime.Now`/`DateTime.UtcNow`、`Guid.NewGuid()`、乱数、環境変数の読み取り、直接の HTTP/ファイル I/O、`Task.Delay` など。これらはアクティビティに置くか、Durable が提供するコンテキスト API を使う必要がある。
- オーケストレータはアクティビティの調整と durable なタイマー/コンテキストの利用のみを行うべき。ビジネスロジックや外部呼び出しは `Activities/` に置く。
- `Triggers/Crawler.cs` のシングルトンパターン(プロファイルごとの固定インスタンス ID、既に `Running`/`Pending` の場合はスキップ)を壊す変更に注意する。壊すと実行が重複しうる。
- 「初回実行は通知をスキップする」挙動を維持する: エンティティ(`Entities/NotificationStateEntity.cs`)が初回呼び出し時に `isFirstRun` を報告し、オーケストレータがそれが true の間 Discord 通知をスキップする。どちらか片方だけを壊す変更を指摘する。

## このリポジトリで強制される規約

- Function 名は `Common/FunctionNames.cs` の定数を使用し、文字列リテラルを使わない。
- HTTP アクセスは注入された `IHttpClientFactory` 経由で行う。`new HttpClient()` は指摘する。
- public な型・メンバーには XML doc コメント(`///`)が必要(無いとビルド警告になる)。
- コード内コメントと XML doc は日本語、ログ・例外メッセージは英語で書かれている。日本語コメントを問題として指摘しない。
- モデル/DTO は `record` 型を優先する。Nullable reference types が有効なため、抑制された・不誠実な null 許容性(根拠のない `!`)を指摘する。

## セキュリティ・設定

- ハードコードされたシークレットや環境固有の値(Discord Webhook URL、`ITAD_API_KEY`、`STEAM_PROFILE_ID`)を指摘する。これらは `IConfiguration` 経由で取得する必要がある。
- `local.settings.json` は絶対にリポジトリに追加しない。
- Steam/ITAD/CheapShark のレスポンスは信頼できないため、外部 HTTP レスポンスのステータスチェック(`IsSuccessStatusCode`)とデシリアライズ結果の null チェックが行われているか確認する。

## 指摘すべきでない事項

- CRLF 改行、4 スペースインデント、allman ブレース — `.editorconfig` で強制されている。
- 日本語のコメント・ドキュメント。
- `docs/` 配下の学習用ロングフォームドキュメント。
