# アーキテクチャと処理フロー

WatchWishlistSale は、Azure Functions の Durable Functions を使って実装されたサーバーレスバッチアプリケーションです。このドキュメントでは、システム全体の構成と処理フローを説明します。

## 全体構成図

```text
┌─────────────────┐       毎時 0 分に起動
│  TimerTrigger   │ ───────────────────────┐
│   (Crawler)     │                         │
└─────────────────┘                         ▼
                                ┌─────────────────────────┐
                                │   オーケストレーター       │
                                │ (WatchWishlistOrchestrator) │
                                └─────────────────────────┘
                                             │
        ┌───────────────┬────────────────────┼────────────────────┬──────────────────┐
        ▼               ▼                    ▼                    ▼                  ▼
┌───────────────┐ ┌───────────────┐  ┌───────────────┐   ┌───────────────┐  ┌───────────────────┐
│GetWishlist    │ │GetAppDetails  │  │FilterSaleApps │   │GetLowestPrice │  │SendDiscord        │
│AppIds         │ │ (Fan-out)     │  │               │   │ (Fan-out)     │  │Notification       │
│ [Activity]    │ │ [Activity]    │  │ [Activity]    │   │ [Activity]    │  │ [Activity]        │
└───────────────┘ └───────────────┘  └───────────────┘   └───────────────┘  └───────────────────┘
                                             │                                          ▲
                                             ▼                                          │
                                  ┌───────────────────────┐                            │
                                  │ NotificationStateEntity│ ───────────────────────────┘
                                  │     [Entity]           │   (前回の通知状態を取得・更新)
                                  └───────────────────────┘
```

## 処理フロー

[`Orchestrations/WatchWishlistOrchestrator.cs`](../Orchestrations/WatchWishlistOrchestrator.cs) が実行する処理は、次の流れになっています。

1. **ウィッシュリストの App ID 一覧を取得**: [`GetWishlistAppIds`](../Activities/GetWishlistAppIds.cs) が Steam の Web API (`IWishlistService/GetWishlist/v1`) を呼び出す。
2. **各アプリの詳細情報を並列取得**: [`GetAppDetails`](../Activities/GetAppDetails.cs) を Fan-out/Fan-in で並列実行する。Steam ストア API のレート制限に配慮し、一定件数ずつチャンク分割してチャンク間に待機を挟む (具体的なチャンクサイズ・待機時間は [`WatchWishlistOrchestrator.cs`](../Orchestrations/WatchWishlistOrchestrator.cs) を参照)。
3. **セール中のアプリを抽出**: [`FilterSaleApps`](../Activities/FilterSaleApps.cs) が、価格情報があり割引率が 0 でないアプリだけを残す。
4. **前回までの通知状態を取得**: [`NotificationStateEntity`](../Entities/NotificationStateEntity.cs) から、前回どのアプリをいくらで通知したかのスナップショットを取得する。
5. **通知対象を絞り込む**: まだ通知していない、または前回通知時から価格が変わったアプリだけに絞り込み、重複通知を防ぐ。
6. **過去最安値を並列取得**: [`GetLowestPrice`](../Activities/GetLowestPrice.cs) が IsThereAnyDeal / CheapShark から過去最安値を取得する。ステップ 2 と同様のチャンク分割 + 待機を用いる。
7. **Discord に通知 (初回実行時を除く)**: [`SendDiscordNotification`](../Activities/SendDiscordNotification.cs) が Discord Webhook に通知を送る。初回実行時はウィッシュリスト全体が「未通知」として検出されるため、大量通知を避けるために送信をスキップし状態の記録のみ行う。
8. **通知状態を更新**: 今回通知した内容をエンティティに記録し、セールが終了して対象から外れたアプリの記録を削除する。次回実行時の差分計算に使われる。

## フォルダー構成と名前空間

役割ベース (`Triggers` / `Orchestrations` / `Activities` / `Entities` / `Models`) を基本とし、複数の役割から横断的に参照される部品を `Common/` にまとめ、`Models/` 配下のみドメインベース (`Wishlist` / `Pricing` / `Notification`) で分割しています。

```text
WatchWishlistSale/
├── Program.cs
├── Common/
│   └── FunctionNames.cs                        … namespace WatchWishlistSale.Common
├── Triggers/
│   └── Crawler.cs                              … namespace WatchWishlistSale.Triggers
├── Orchestrations/
│   └── WatchWishlistOrchestrator.cs            … namespace WatchWishlistSale.Orchestrations
├── Activities/
│   ├── GetWishlistAppIds.cs                    … namespace WatchWishlistSale.Activities
│   ├── GetAppDetails.cs
│   ├── FilterSaleApps.cs
│   ├── GetLowestPrice.cs
│   └── SendDiscordNotification.cs
├── Entities/
│   └── NotificationStateEntity.cs              … namespace WatchWishlistSale.Entities
└── Models/
    ├── Wishlist/                               … namespace WatchWishlistSale.Models.Wishlist
    ├── Pricing/                                … namespace WatchWishlistSale.Models.Pricing
    └── Notification/                           … namespace WatchWishlistSale.Models.Notification
```

名前空間はフォルダー構成と一致させており (C# の慣習および Roslyn アナライザーの IDE0130 ルールに従う)、どのファイルがどこにあるかを名前空間から推測できます。

## 参考

Azure Functions / Durable Functions 自体の基礎的な概念については、[Microsoft Learn の公式ドキュメント](https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-overview) を参照してください。
