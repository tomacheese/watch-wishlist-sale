# Architecture and Processing Flow

WatchWishlistSale is a serverless batch application implemented using Durable Functions, an extension of Azure Functions. This document describes the overall system architecture and processing flow.

## Overall Architecture Diagram

```text
┌─────────────────┐       Triggered at minute 0 every hour
│  TimerTrigger   │ ───────────────────────┐
│   (Crawler)     │                         │
└─────────────────┘                         ▼
                                ┌─────────────────────────┐
                                │   Orchestrator          │
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
                                  │     [Entity]           │   (Retrieve and update previous notification state)
                                  └───────────────────────┘
```

## Processing Flow

The processing executed by [`Orchestrations/WatchWishlistOrchestrator.cs`](../Orchestrations/WatchWishlistOrchestrator.cs) follows this sequence:

1. **Retrieve list of wishlist app IDs**: [`GetWishlistAppIds`](../Activities/GetWishlistAppIds.cs) calls Steam's Web API (`IWishlistService/GetWishlist/v1`).
2. **Retrieve app details in parallel**: [`GetAppDetails`](../Activities/GetAppDetails.cs) runs in parallel using the Fan-out/Fan-in pattern. Taking Steam Store API rate limits into account, the requests are chunked into batches with delays between chunks (see [`WatchWishlistOrchestrator.cs`](../Orchestrations/WatchWishlistOrchestrator.cs) for specific chunk sizes and wait times).
3. **Extract apps on sale**: [`FilterSaleApps`](../Activities/FilterSaleApps.cs) keeps only apps with price information and non-zero discount rates.
4. **Retrieve previous notification state**: Gets a snapshot from [`NotificationStateEntity`](../Entities/NotificationStateEntity.cs) showing which apps were previously notified at what prices.
5. **Filter notification targets**: Narrows down to apps that haven't been notified yet or whose price has changed since the last notification, preventing duplicate notifications.
6. **Retrieve historical lowest prices in parallel**: [`GetLowestPrice`](../Activities/GetLowestPrice.cs) retrieves historical lowest prices from IsThereAnyDeal / CheapShark. Uses chunking and delays similar to step 2.
7. **Send Discord notification (except on first run)**: [`SendDiscordNotification`](../Activities/SendDiscordNotification.cs) sends notifications to Discord Webhook. On first run, the entire wishlist is detected as "not notified", so to avoid a flood of notifications, sending is skipped and only the state is recorded.
8. **Update notification state**: Records the notification content in the entity and deletes records for apps that are no longer on sale. Used for calculating differences in the next run.

## Folder Structure and Namespace

Based on role-based structure (`Triggers` / `Orchestrations` / `Activities` / `Entities` / `Models`), with cross-cutting concerns grouped in `Common/`, and only the `Models/` directory organized by domain (`Wishlist` / `Pricing` / `Notification`).

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

Namespaces match folder structure (following C# conventions and Roslyn analyzer IDE0130 rule), making it possible to infer file locations from their namespaces.

## References

For fundamental concepts of Azure Functions / Durable Functions themselves, see the [official Microsoft Learn documentation](https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-overview).
