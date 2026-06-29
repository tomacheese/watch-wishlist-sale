using System.Text.Json.Serialization;

namespace WatchWishlistSale.Models.Notification;

/// <summary>Discord embed の 1 フィールド (インライン表示される項目)</summary>
internal class DiscordEmbedField
{
    /// <summary>フィールドのタイトル</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>フィールドの本文</summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    /// <summary>他フィールドとインライン (横並び) 表示するかどうか</summary>
    [JsonPropertyName("inline")]
    public bool Inline { get; set; }
}

/// <summary>Discord の embed オブジェクト</summary>
internal class DiscordEmbed
{
    /// <summary>embed のタイトル</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>embed のフィールド一覧</summary>
    [JsonPropertyName("fields")]
    public List<DiscordEmbedField> Fields { get; set; } = [];

    /// <summary>embed のタイムスタンプ (ISO 8601 形式)</summary>
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>embed のサイドバーカラー (RGB 整数値)</summary>
    [JsonPropertyName("color")]
    public int Color { get; set; }
}

/// <summary>Discord Webhook に POST するペイロード</summary>
internal class DiscordWebhookPayload
{
    /// <summary>送信する embed 一覧</summary>
    [JsonPropertyName("embeds")]
    public List<DiscordEmbed> Embeds { get; set; } = [];
}
