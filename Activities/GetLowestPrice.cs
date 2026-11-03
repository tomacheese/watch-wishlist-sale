using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WatchWishlistSale.Common;
using WatchWishlistSale.Models.Pricing;

namespace WatchWishlistSale.Activities;

/// <summary>
/// Activity that gets the historical lowest price of a Steam app.
/// SteamDB cannot be scraped directly because of Cloudflare's Managed Challenge,
/// so the IsThereAnyDeal (ITAD) API is primary and the CheapShark API is the fallback when ITAD has no result.
///
/// Design trade-off: the ITAD and CheapShark calls are bundled into one activity, which bends the "one external I/O per activity" guideline.
/// The fallback (CheapShark only when ITAD has no result) cannot be decided in the orchestrator because it cannot branch deterministically on activity results.
/// Retries are configured per activity by the calling orchestrator.
/// </summary>
public class GetLowestPrice(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GetLowestPrice> logger)
{
    /// <summary>Internal ID of the Steam shop in ITAD.</summary>
    private const int SteamShopId = 61;

    /// <summary>
    /// Activity entry point. Gets and returns the historical lowest price from ITAD or CheapShark.
    /// </summary>
    /// <param name="appId">Steam app ID to look up.</param>
    /// <returns>The lowest price result, or <see langword="null"/> if it cannot be fetched.</returns>
    [Function(FunctionNames.GetLowestPriceActivity)]
    public async Task<LowestPriceResult?> GetLowestPriceActivity([ActivityTrigger] long appId)
    {
        logger.LogInformation("Getting lowest price for app id: {AppId}", appId);

        LowestPriceResult? result = await GetFromItadAsync(appId)
          ?? await GetFromCheapSharkAsync(appId);
        if (result is null)
        {
            logger.LogWarning("⚠️ Failed to get lowest price for app id {AppId}", appId);
        }

        return result;
    }

    /// <summary>
    /// Gets the historical lowest price (JPY) in the Steam store from the IsThereAnyDeal API.
    /// </summary>
    private async Task<LowestPriceResult?> GetFromItadAsync(long appId)
    {
        var apiKey = configuration["ITAD_API_KEY"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("⚠️ ITAD_API_KEY is not configured. Skipping ITAD lookup for app id {AppId}", appId);
            return null;
        }

        try
        {
            HttpClient client = httpClientFactory.CreateClient(nameof(GetLowestPrice));

            // Resolve the ITAD game ID from the Steam app ID.
            var lookupUrl = $"https://api.isthereanydeal.com/games/lookup/v1?key={apiKey}&appid={appId}";
            ItadLookupResponse? lookup = await client.GetFromJsonAsync<ItadLookupResponse>(lookupUrl);
            if (lookup is not { Found: true, Game: not null })
            {
                logger.LogInformation("ITAD does not have a game entry for app id {AppId}", appId);
                return null;
            }

            // Get the lowest price (JPY) in the Steam shop from the ITAD game ID.
            var storeLowUrl = $"https://api.isthereanydeal.com/games/storelow/v2?key={apiKey}&country=JP&shops={SteamShopId}";
            using StringContent body = new(JsonSerializer.Serialize(new[] { new { id = lookup.Game.Id } }), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client.PostAsync(new Uri(storeLowUrl), body);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("⚠️ ITAD storelow request failed for app id {AppId}: {StatusCode} {ReasonPhrase}", appId, (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }

            List<ItadStoreLowEntry>? entries = await response.Content.ReadFromJsonAsync<List<ItadStoreLowEntry>>();
            ItadLow? steamLow = entries?
              .SelectMany(entry => entry.Lows)
              .FirstOrDefault(low => low.Shop.Id == SteamShopId);
            if (steamLow is null)
            {
                logger.LogInformation("ITAD does not have a Steam price history for app id {AppId}", appId);
                return null;
            }

            return new LowestPriceResult(appId, steamLow.Price.Amount, steamLow.Price.Currency, "ITAD");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "⚠️ Failed to get lowest price from ITAD for app id {AppId}", appId);
            return null;
        }
    }

    /// <summary>
    /// Gets the historical lowest price from the CheapShark API (fallback).
    /// CheapShark prices are USD only, so the value is returned as is without currency conversion.
    /// </summary>
    private async Task<LowestPriceResult?> GetFromCheapSharkAsync(long appId)
    {
        try
        {
            HttpClient client = httpClientFactory.CreateClient(nameof(GetLowestPrice));

            List<CheapSharkGameLookup>? lookup = await client.GetFromJsonAsync<List<CheapSharkGameLookup>>(
              $"https://www.cheapshark.com/api/1.0/games?steamAppID={appId}");
            var gameId = lookup?.FirstOrDefault()?.GameId;
            if (string.IsNullOrEmpty(gameId))
            {
                logger.LogInformation("CheapShark does not have a game entry for app id {AppId}", appId);
                return null;
            }

            CheapSharkGameInfo? info = await client.GetFromJsonAsync<CheapSharkGameInfo>(
              $"https://www.cheapshark.com/api/1.0/games?id={gameId}");
            var priceText = info?.CheapestPriceEver?.Price;
            if (string.IsNullOrEmpty(priceText) || !decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            {
                logger.LogInformation("CheapShark does not have a price history for app id {AppId}", appId);
                return null;
            }

            return new LowestPriceResult(appId, price, "USD", "CheapShark");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "⚠️ Failed to get lowest price from CheapShark for app id {AppId}", appId);
            return null;
        }
    }
}
