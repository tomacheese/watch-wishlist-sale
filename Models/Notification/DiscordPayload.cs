using System.Text.Json.Serialization;

namespace WatchWishlistSale.Models.Notification;

/// <summary>One field of a Discord embed (an item that can be shown inline).</summary>
internal class DiscordEmbedField
{
    /// <summary>Field title.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Field body.</summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    /// <summary>Whether to show the field inline (side by side) with other fields.</summary>
    [JsonPropertyName("inline")]
    public bool Inline { get; set; }
}

/// <summary>Discord embed object.</summary>
internal class DiscordEmbed
{
    /// <summary>Embed title.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Embed fields.</summary>
    [JsonPropertyName("fields")]
    public List<DiscordEmbedField> Fields { get; set; } = [];

    /// <summary>Embed timestamp (ISO 8601).</summary>
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>Embed sidebar color (RGB integer).</summary>
    [JsonPropertyName("color")]
    public int Color { get; set; }
}

/// <summary>Payload POSTed to the Discord webhook.</summary>
internal class DiscordWebhookPayload
{
    /// <summary>Embeds to send.</summary>
    [JsonPropertyName("embeds")]
    public List<DiscordEmbed> Embeds { get; set; } = [];
}
