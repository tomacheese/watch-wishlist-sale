using System.Net.Http.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WatchWishlistSale.Common;
using WatchWishlistSale.Models.Wishlist;

namespace WatchWishlistSale.Activities;

/// <summary>
/// Activity that gets the app IDs on the wishlist of a Steam profile.
///
/// It uses the official Steam Web API (IWishlistService/GetWishlist/v1) because the wishlist page became an SPA
/// and no longer embeds the wishlist JSON, so scraping the page is not possible.
/// The endpoint needs no API key, but requires a numeric SteamID64
/// (a custom URL name (vanity name) cannot be used).
/// </summary>
public class GetWishlistAppIds(IHttpClientFactory httpClientFactory, ILogger<GetWishlistAppIds> logger)
{
    /// <summary>
    /// Activity entry point. Gets and returns the app IDs on the wishlist of the given Steam profile.
    /// </summary>
    /// <param name="profileId">SteamID64 of the Steam profile whose wishlist is fetched.</param>
    /// <returns>The Steam app IDs on the wishlist.</returns>
    [Function(FunctionNames.GetWishlistAppIdsActivity)]
    public async Task<List<long>> GetWishlistAppIdsActivity([ActivityTrigger] string profileId)
    {
        logger.LogInformation("Getting wishlist app ids.");

        var url = $"https://api.steampowered.com/IWishlistService/GetWishlist/v1/?steamid={profileId}";
        HttpClient client = httpClientFactory.CreateClient(nameof(GetWishlistAppIds));
        WishlistApiResponse? result = await client.GetFromJsonAsync<WishlistApiResponse>(url);
        IList<WishlistItem>? items = result?.Response?.Items ?? throw new InvalidOperationException("Failed to get wishlist items.");
        List<long> appIds = [.. items.Select(item => item.AppId)];
        logger.LogInformation("Got {AppIdsCount} app ids.", appIds.Count);
        return appIds;
    }
}
