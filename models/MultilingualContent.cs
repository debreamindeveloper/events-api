using System.Text.Json.Serialization;

namespace EventsAPI.Models;

/// <summary>
/// Represents content in multiple languages (English, Finnish, Amharic)
/// </summary>
public class MultilingualContent
{
    /// <summary>
    /// English version of the content
    /// </summary>
    [JsonPropertyName("en")]
    public string English { get; set; } = string.Empty;

    /// <summary>
    /// Finnish version of the content
    /// </summary>
    [JsonPropertyName("fi")]
    public string Finnish { get; set; } = string.Empty;

    /// <summary>
    /// Amharic version of the content
    /// </summary>
    [JsonPropertyName("am")]
    public string Amharic { get; set; } = string.Empty;

    /// <summary>
    /// Default constructor
    /// </summary>
    public MultilingualContent()
    {
    }

    /// <summary>
    /// Constructor with all languages
    /// </summary>
    public MultilingualContent(string english, string finnish = "", string amharic = "")
    {
        English = english;
        Finnish = finnish;
        Amharic = amharic;
    }

    /// <summary>
    /// Get content in a specific language, with fallback to English
    /// </summary>
    public string GetContent(string languageCode)
    {
        return languageCode?.ToLower() switch
        {
            "fi" => !string.IsNullOrEmpty(Finnish) ? Finnish : English,
            "am" => !string.IsNullOrEmpty(Amharic) ? Amharic : English,
            _ => English
        };
    }

    /// <summary>
    /// Check if content is empty in all languages
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(English) && string.IsNullOrEmpty(Finnish) && string.IsNullOrEmpty(Amharic);
}

