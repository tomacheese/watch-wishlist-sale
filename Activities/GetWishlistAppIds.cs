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
    /// <summary>
    /// アクティビティのエントリーポイント。指定した Steam プロフィール ID のウィッシュリストから App ID 一覧を取得して返す。
    /// </summary>
    /// <param name="profileId">ウィッシュリストを取得する Steam プロフィールの SteamID64</param>
    /// <returns>ウィッシュリストに登録されている Steam アプリ ID の一覧</returns>
    [Function(FunctionNames.GetWishlistAppIdsActivity)]
    public async Task<List<long>> GetWishlistAppIdsActivity([ActivityTrigger] string profileId)
    {
        logger.LogInformation("Getting wishlist app ids.");

        var url = $"https://api.steampowered.com/IWishlistService/GetWishlist/v1/?steamid={profileId}";
        HttpClient client = httpClientFactory.CreateClient(nameof(GetWishlistAppIds));
        WishlistApiResponse? result = await client.GetFromJsonAsync<WishlistApiResponse>(url);
        List<WishlistItem>? items = result?.Response?.Items ?? throw new InvalidOperationException("Failed to get wishlist items.");
        List<long> appIds = [.. items.Select(item => item.AppId)];
        logger.LogInformation("Got {AppIdsCount} app ids.", appIds.Count);
        return appIds;
    }
}
