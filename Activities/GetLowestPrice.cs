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
/// Steam アプリの過去最安値を取得する Activity。
/// SteamDB は Cloudflare の Managed Challenge により直接スクレイピングできないため、
/// IsThereAnyDeal (ITAD) API を主軸とし、取得できない場合は CheapShark API にフォールバックする。
/// 参考: https://gist.github.com/akubiusa/4356652a26a66d6ac1a659d79600f989
///
/// 設計上のトレードオフ: ITAD と CheapShark への複数回の HTTP 呼び出しを 1 つの Activity にまとめている。
/// 「Activity は外部 I/O を 1 回だけ行うべき」という原則に厳密には反するが、
/// (1) どちらも「同じアプリの過去最安値を取得する」という単一の責務を構成する一連の処理であり、
/// (2) フォールバック判定 (ITAD で取得できない場合のみ CheapShark を呼ぶ) はオーケストレーター内では
///     決定論性の制約により行えず、Activity 側に委譲せざるを得ないため、
/// あえて 1 つの Activity にまとめている。再試行は呼び出し元のオーケストレーターで
/// Activity 単位のリトライポリシーとして設定済み。
/// </summary>
public class GetLowestPrice(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GetLowestPrice> logger)
{
    /// <summary>ITAD における Steam ショップの内部 ID</summary>
    private const int SteamShopId = 61;

    [Function(FunctionNames.GetLowestPriceActivity)]
    public async Task<LowestPriceResult?> GetLowestPriceActivity([ActivityTrigger] long appId)
    {
        logger.LogInformation("Getting lowest price for app id: {appId}", appId);

        LowestPriceResult? result = await GetFromItadAsync(appId)
          ?? await GetFromCheapSharkAsync(appId);
        if (result is null)
        {
            logger.LogWarning("⚠️ Failed to get lowest price for app id {appId}", appId);
        }

        return result;
    }

    /// <summary>
    /// IsThereAnyDeal API から、Steam ストアにおける過去最安値 (JPY) を取得する
    /// </summary>
    private async Task<LowestPriceResult?> GetFromItadAsync(long appId)
    {
        var apiKey = configuration["ITAD_API_KEY"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("⚠️ ITAD_API_KEY is not configured. Skipping ITAD lookup for app id {appId}", appId);
            return null;
        }

        try
        {
            HttpClient client = httpClientFactory.CreateClient(nameof(GetLowestPrice));

            // Steam アプリ ID から ITAD のゲーム ID を引く
            var lookupUrl = $"https://api.isthereanydeal.com/games/lookup/v1?key={apiKey}&appid={appId}";
            ItadLookupResponse? lookup = await client.GetFromJsonAsync<ItadLookupResponse>(lookupUrl);
            if (lookup is not { Found: true, Game: not null })
            {
                logger.LogInformation("ITAD does not have a game entry for app id {appId}", appId);
                return null;
            }

            // ITAD のゲーム ID から Steam ショップでの過去最安値 (日本円) を取得する
            var storeLowUrl = $"https://api.isthereanydeal.com/games/storelow/v2?key={apiKey}&country=JP&shops={SteamShopId}";
            using StringContent body = new(JsonSerializer.Serialize(new[] { new { id = lookup.Game.Id } }), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client.PostAsync(storeLowUrl, body);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("⚠️ ITAD storelow request failed for app id {appId}: {statusCode} {reasonPhrase}", appId, (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }

            List<ItadStoreLowEntry>? entries = await response.Content.ReadFromJsonAsync<List<ItadStoreLowEntry>>();
            ItadLow? steamLow = entries?
              .SelectMany(entry => entry.Lows)
              .FirstOrDefault(low => low.Shop.Id == SteamShopId);
            if (steamLow is null)
            {
                logger.LogInformation("ITAD does not have a Steam price history for app id {appId}", appId);
                return null;
            }

            return new LowestPriceResult(appId, steamLow.Price.Amount, steamLow.Price.Currency, "ITAD");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "⚠️ Failed to get lowest price from ITAD for app id {appId}", appId);
            return null;
        }
    }

    /// <summary>
    /// CheapShark API から過去最安値を取得する (フォールバック)。
    /// CheapShark は USD 建てのみのため、為替換算は行わずそのまま返す。
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
                logger.LogInformation("CheapShark does not have a game entry for app id {appId}", appId);
                return null;
            }

            CheapSharkGameInfo? info = await client.GetFromJsonAsync<CheapSharkGameInfo>(
              $"https://www.cheapshark.com/api/1.0/games?id={gameId}");
            var priceText = info?.CheapestPriceEver?.Price;
            if (string.IsNullOrEmpty(priceText) || !decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            {
                logger.LogInformation("CheapShark does not have a price history for app id {appId}", appId);
                return null;
            }

            return new LowestPriceResult(appId, price, "USD", "CheapShark");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "⚠️ Failed to get lowest price from CheapShark for app id {appId}", appId);
            return null;
        }
    }
}
