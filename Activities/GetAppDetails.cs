using System.Globalization;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WatchWishlistSale.Common;
using WatchWishlistSale.Models.Wishlist;

namespace WatchWishlistSale.Activities;

/// <summary>
/// Activity that fetches the details (including price information) of a Steam app ID from the Steam store API.
/// </summary>
public class GetAppDetails(IHttpClientFactory httpClientFactory, ILogger<GetAppDetails> logger)
{
    /// <summary>
    /// Activity entry point. Fetches and returns the details of the given Steam app ID.
    /// </summary>
    /// <param name="appId">Steam app ID to fetch details for.</param>
    /// <returns>The app details, or <see langword="null"/> if they cannot be fetched.</returns>
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
