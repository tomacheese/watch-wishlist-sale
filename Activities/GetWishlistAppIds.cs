using System.Net.Http.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WatchWishlistSale.Common;
using WatchWishlistSale.Models.Wishlist;

namespace WatchWishlistSale.Activities;

/// <summary>
/// 指定した Steam プロフィールのウィッシュリストから、登録されているアプリ ID 一覧を取得する Activity。
///
/// 旧実装ではウィッシュリストページの HTML に埋め込まれた g_rgWishlistData をスクレイピングしていたが、
/// Steam がウィッシュリストページを SPA 化したことで埋め込み JSON が廃止されたため、
/// 公式の Steam Web API (IWishlistService/GetWishlist/v1) を利用する方式に変更した。
/// このエンドポイントは API キー不要で呼び出せるが、引数には数値の SteamID64 を渡す必要がある
/// (カスタム URL 名 (vanity name) は使用できない)。
/// </summary>
public class GetWishlistAppIds(IHttpClientFactory httpClientFactory, ILogger<GetWishlistAppIds> logger)
{
    [Function(FunctionNames.GetWishlistAppIdsActivity)]
    public async Task<List<long>> GetWishlistAppIdsActivity([ActivityTrigger] string profileId)
    {
        logger.LogInformation("Getting wishlist app ids for profile id: {profileId}", profileId);

        string url = $"https://api.steampowered.com/IWishlistService/GetWishlist/v1/?steamid={profileId}";
        HttpClient client = httpClientFactory.CreateClient(nameof(GetWishlistAppIds));
        WishlistApiResponse? result = await client.GetFromJsonAsync<WishlistApiResponse>(url);
        List<WishlistItem>? items = result?.Response?.Items;
        if (items is null)
        {
            throw new InvalidOperationException($"Failed to get wishlist items for profile id {profileId} ({url})");
        }

        List<long> appIds = items.Select(item => item.AppId).ToList();
        logger.LogInformation("Got {appIdsCount} app ids for profile id: {profileId}", appIds.Count, profileId);
        return appIds;
    }
}
