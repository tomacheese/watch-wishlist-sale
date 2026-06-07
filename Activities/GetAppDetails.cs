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
    [Function(FunctionNames.GetAppDetailsActivity)]
    public async Task<AppDetails?> GetAppDetailsActivity([ActivityTrigger] long appId)
    {
        logger.LogInformation("Getting app details for app id: {appId}", appId);

        string url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=JP";
        HttpClient client = httpClientFactory.CreateClient(nameof(GetAppDetails));
        using HttpResponseMessage response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("⚠️ HTTP error: {statusCode} {reasonPhrase} ({url})", (int)response.StatusCode, response.ReasonPhrase, url);
            return null;
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync();
        Dictionary<string, AppDetailsResult>? results = await JsonSerializer.DeserializeAsync<Dictionary<string, AppDetailsResult>>(stream);
        if (results is null
          || !results.TryGetValue(appId.ToString(), out AppDetailsResult? result)
          || !result.Success
          || result.Data is null)
        {
            logger.LogWarning("⚠️ Failed to get app data for app id {appId}", appId);
            return null;
        }

        return result.Data;
    }
}
