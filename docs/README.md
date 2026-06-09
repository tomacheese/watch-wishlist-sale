# WatchWishlistSale ドキュメント

このディレクトリは、本プロジェクト (`WatchWishlistSale`) のコードを題材に、
**Azure Functions** と **Durable Functions** の実装の考え方を学ぶための学習用ドキュメントです。

## このプロジェクトは何をするものか

Steam のウィッシュリストを定期的に巡回し、セール中になった (かつ前回通知時から値下がりした) アプリを検出して
Discord に通知する、サーバーレスのバッチ処理アプリケーションです。

```
毎時 1 回タイマーで起動
   │
   ▼
ウィッシュリストの App ID 一覧を取得
   │
   ▼
各アプリの詳細情報 (価格など) を並列取得
   │
   ▼
セール中のアプリだけを抽出
   │
   ▼
前回通知時から値下がりした (or 新規セール) アプリだけに絞り込み
   │
   ▼
過去最安値を並列取得
   │
   ▼
Discord に通知
   │
   ▼
通知済み状態を記録
```

この一連の流れを、Azure Functions の **Durable Functions** という仕組みを使って実装しています。

## ドキュメント一覧

学習する順番に並んでいます。Azure Functions / Durable Functions に触れたことがない人は、
上から順に読み進めることを推奨します。

| # | ファイル | 内容 |
|---|---|---|
| 1 | [01-azure-functions-fundamentals.md](./01-azure-functions-fundamentals.md) | Azure Functions の基礎 (Function とは、トリガーとバインディング、分離ワーカーモデル、DI) |
| 2 | [02-durable-functions-fundamentals.md](./02-durable-functions-fundamentals.md) | Durable Functions の基礎 (オーケストレーター、アクティビティ、エンティティ、決定論性、Fan-out/Fan-in) |
| 3 | [03-architecture-and-flow.md](./03-architecture-and-flow.md) | 本プロジェクトのアーキテクチャと処理フロー、フォルダ構成・名前空間の設計 |
| 4 | [04-code-walkthrough.md](./04-code-walkthrough.md) | 実際のコードを 1 行ずつ追うウォークスルー (Trigger → Orchestrator → Activity → Entity) |
| 5 | [05-best-practices.md](./05-best-practices.md) | コード中に登場するベストプラクティスとその理由 (リトライポリシー、シングルトン化、DI など) |
| 6 | [06-local-development.md](./06-local-development.md) | ローカル環境での動かし方・デバッグ方法 (Azurite、func start、設定ファイル) |

## 前提知識

- C# の基本文法 (クラス、record、async/await、LINQ)
- HTTP / REST API の基本的な知識
- JSON の基本

Azure / Azure Functions / Durable Functions に関する知識は前提としません。
このドキュメントを読みながら、わからない用語が出てきたら都度調べる、というスタンスで進めてください。

## 実際のソースコードとの対応

このドキュメントは概念の説明を主目的としているため、コードの全文は載せていません。
実際のソースコードは以下のディレクトリにあります。あわせて読むことで理解が深まります。

```
WatchWishlistSale/
├── Program.cs                 … エントリーポイント (DI 登録など)
├── Common/                    … 各 Function 名の定数など、共通的な部品
├── Triggers/                  … 外部からの起動トリガー (タイマーなど)
├── Orchestrations/            … Durable Functions のオーケストレーター
├── Activities/                … オーケストレーターから呼び出される個々の処理
├── Entities/                  … 状態を保持する Durable Entity
└── Models/                    … 各種データ構造 (DTO)
    ├── Wishlist/              … Steam ウィッシュリスト・アプリ情報関連
    ├── Pricing/               … 過去最安値関連
    └── Notification/          … 通知関連
```
