// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

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
