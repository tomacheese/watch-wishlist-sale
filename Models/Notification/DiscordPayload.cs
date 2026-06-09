using System.Text.Json.Serialization;

namespace WatchWishlistSale.Models.Notification;

internal class DiscordEmbedField
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("inline")]
    public bool Inline { get; set; }
}

internal class DiscordEmbed
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public List<DiscordEmbedField> Fields { get; set; } = [];

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public int Color { get; set; }
}

internal class DiscordWebhookPayload
{
    [JsonPropertyName("embeds")]
    public List<DiscordEmbed> Embeds { get; set; } = [];
}
