using System.Globalization;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WatchWishlistSale.Common;
using WatchWishlistSale.Models.Wishlist;

namespace WatchWishlistSale.Activities;

/// <summary>
/// 指定した Steam アプリ ID の詳細情報 (価格情報含む) を Steam ストア API から取得する Activity
/// </summary>
public class GetAppDetails(IHttpClientFactory httpClientFactory, ILogger<GetAppDetails> logger)
{
    /// <summary>
    /// アクティビティのエントリーポイント。指定した Steam アプリ ID の詳細情報を取得して返す。
    /// </summary>
    /// <param name="appId">詳細情報を取得する Steam アプリ ID</param>
    /// <returns>アプリ詳細情報。取得できない場合は <see langword="null"/></returns>
    [Function(FunctionNames.GetAppDetailsActivity)]
    public async Task<AppDetails?> GetAppDetailsActivity([ActivityTrigger] long appId)
    {
        logger.LogInformation("Getting app details for app id: {AppId}", appId);

        var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=JP";
        HttpClient client = httpClientFactory.CreateClient(nameof(GetAppDetails));
        using HttpResponseMessage response = await client.GetAsync(new Uri(url));
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("⚠️ HTTP error: {StatusCode} {ReasonPhrase} ({Url})", (int)response.StatusCode, response.ReasonPhrase, url);
            return null;
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync();
        Dictionary<string, AppDetailsResult>? results = await JsonSerializer.DeserializeAsync<Dictionary<string, AppDetailsResult>>(stream);
        if (results is null
          || !results.TryGetValue(appId.ToString(CultureInfo.InvariantCulture), out AppDetailsResult? result)
          || !result.Success
          || result.Data is null)
        {
            logger.LogWarning("⚠️ Failed to get app data for app id {AppId}", appId);
            return null;
        }

        return result.Data;
    }
}
