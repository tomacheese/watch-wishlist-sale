// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// CA1812: 以下のクラスは System.Text.Json によるリフレクション経由でインスタンス化されるため、
//         「インスタンス化されていない内部クラス」という誤検知を抑制する。
[assembly: SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated via JSON deserialization (System.Text.Json reflection).",
    Scope = "type",
    Target = "~T:WatchWishlistSale.Models.Pricing.CheapSharkGameLookup")]
[assembly: SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated via JSON deserialization (System.Text.Json reflection).",
    Scope = "type",
    Target = "~T:WatchWishlistSale.Models.Pricing.CheapSharkGameInfo")]
[assembly: SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated via JSON deserialization (System.Text.Json reflection).",
    Scope = "type",
    Target = "~T:WatchWishlistSale.Models.Pricing.CheapSharkCheapestPrice")]
[assembly: SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated via JSON deserialization (System.Text.Json reflection).",
    Scope = "type",
    Target = "~T:WatchWishlistSale.Models.Pricing.ItadLookupResponse")]
[assembly: SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated via JSON deserialization (System.Text.Json reflection).",
    Scope = "type",
    Target = "~T:WatchWishlistSale.Models.Pricing.ItadGame")]
[assembly: SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated via JSON deserialization (System.Text.Json reflection).",
    Scope = "type",
    Target = "~T:WatchWishlistSale.Models.Pricing.ItadStoreLowEntry")]
[assembly: SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated via JSON deserialization (System.Text.Json reflection).",
    Scope = "type",
    Target = "~T:WatchWishlistSale.Models.Pricing.ItadLow")]

// CA2227: 以下のプロパティはシリアライズに setter が必要なため抑制する。
[assembly: SuppressMessage(
    "Usage",
    "CA2227:Collection properties should be read only",
    Justification = "Setter is required for Durable Entity state serialization.",
    Scope = "member",
    Target = "~P:WatchWishlistSale.Models.Notification.NotificationState.NotifiedPrices")]
[assembly: SuppressMessage(
    "Usage",
    "CA2227:Collection properties should be read only",
    Justification = "Setter is required for JSON deserialization (System.Text.Json).",
    Scope = "member",
    Target = "~P:WatchWishlistSale.Models.Wishlist.WishlistResponseBody.Items")]

// CA1002: Azure Functions の Activity トリガーは List<T> を直接 JSON シリアライズするため、
//         Collection<T> への変更はシリアライズ互換を損なう。
[assembly: SuppressMessage(
    "Design",
    "CA1002:Do not expose generic lists",
    Justification = "Azure Functions Durable Activity serializes/deserializes List<T> directly via JSON.",
    Scope = "member",
    Target = "~M:WatchWishlistSale.Activities.FilterSaleApps.FilterSaleAppsActivity(System.Collections.Generic.List{WatchWishlistSale.Models.Wishlist.AppDetails})~System.Collections.Generic.List{WatchWishlistSale.Models.Wishlist.AppDetails}")]
[assembly: SuppressMessage(
    "Design",
    "CA1002:Do not expose generic lists",
    Justification = "Azure Functions Durable Activity receives List<T> as parameter via JSON deserialization.",
    Scope = "member",
    Target = "~M:WatchWishlistSale.Activities.SendDiscordNotification.SendDiscordNotificationActivity(System.Collections.Generic.List{WatchWishlistSale.Models.Notification.SaleNotification})~System.Threading.Tasks.Task")]
[assembly: SuppressMessage(
    "Design",
    "CA1002:Do not expose generic lists",
    Justification = "Setter is required for JSON deserialization (System.Text.Json).",
    Scope = "member",
    Target = "~P:WatchWishlistSale.Models.Wishlist.WishlistResponseBody.Items")]
