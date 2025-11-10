using Azure;
using Azure.Data.Tables;
using System.Text.Json.Serialization;

namespace EventsAPI.Models;

/// <summary>
/// Event model representing a church event with multilingual support.
/// </summary>
public class Event : ITableEntity
{
    /// <summary>
    /// The title of the event in multiple languages
    /// </summary>
    [JsonPropertyName("title")]
    public MultilingualContent Title { get; set; } = new();

    /// <summary>
    /// A description of the event in multiple languages
    /// </summary>
    [JsonPropertyName("description")]
    public MultilingualContent Description { get; set; } = new();

    /// <summary>
    /// The location where the event takes place in multiple languages
    /// </summary>
    [JsonPropertyName("location")]
    public MultilingualContent Location { get; set; } = new();

    /// <summary>
    /// The date and time of the event
    /// </summary>
    [JsonPropertyName("eventDate")]
    public DateTime EventDate { get; set; }

    /// <summary>
    /// Azure Table Storage partition key
    /// </summary>
    [JsonPropertyName("partitionKey")]
    public string PartitionKey { get; set; } = "events";

    /// <summary>
    /// Azure Table Storage row key (unique identifier)
    /// </summary>
    [JsonPropertyName("rowKey")]
    public string RowKey { get; set; } = string.Empty;

    /// <summary>
    /// ETag for optimistic concurrency
    /// </summary>
    [JsonIgnore]
    public ETag ETag { get; set; }

    /// <summary>
    /// Timestamp of the entity
    /// </summary>
    [JsonIgnore]
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// Default constructor
    /// </summary>
    public Event()
    {
    }

    /// <summary>
    /// Constructor with parameters (English only)
    /// </summary>
    public Event(string title, string description, string location, DateTime eventDate, string? rowKey = null)
    {
        Title = new MultilingualContent(title);
        Description = new MultilingualContent(description);
        Location = new MultilingualContent(location);
        EventDate = eventDate;
        PartitionKey = "events";
        RowKey = rowKey ?? GenerateRowKey(eventDate, title);
    }

    /// <summary>
    /// Constructor with multilingual parameters
    /// </summary>
    public Event(MultilingualContent title, MultilingualContent description, MultilingualContent location, DateTime eventDate, string? rowKey = null)
    {
        Title = title;
        Description = description;
        Location = location;
        EventDate = eventDate;
        PartitionKey = "events";
        RowKey = rowKey ?? GenerateRowKey(eventDate, title.English);
    }

    /// <summary>
    /// Generate a row key based on the event date and title
    /// </summary>
    private static string GenerateRowKey(DateTime eventDate, string title)
    {
        var dateStr = eventDate.ToString("yyyyMMddHHmmss");
        var titleSlug = title.Replace(" ", "_");
        if (titleSlug.Length > 20)
        {
            titleSlug = titleSlug.Substring(0, 20);
        }
        return $"{dateStr}_{titleSlug}";
    }

    /// <summary>
    /// Create an Event from a TableEntity
    /// </summary>
    public static Event FromTableEntity(TableEntity entity)
    {
        // Try to parse multilingual content, fallback to simple strings for backward compatibility
        var title = ParseMultilingualContent(entity, "Title", "title");
        var description = ParseMultilingualContent(entity, "Description", "description");
        var location = ParseMultilingualContent(entity, "Location", "location");

        return new Event
        {
            Title = title,
            Description = description,
            Location = location,
            EventDate = entity.GetDateTime("EventDate") ?? entity.GetDateTime("event_date") ?? DateTime.MinValue,
            PartitionKey = entity.PartitionKey,
            RowKey = entity.RowKey,
            ETag = entity.ETag,
            Timestamp = entity.Timestamp
        };
    }

    /// <summary>
    /// Parse multilingual content from TableEntity
    /// </summary>
    private static MultilingualContent ParseMultilingualContent(TableEntity entity, string pascalCaseKey, string snakeCaseKey)
    {
        // Try to get multilingual properties (en, fi, am suffixes)
        var english = entity.GetString($"{pascalCaseKey}_en") ?? entity.GetString($"{snakeCaseKey}_en") ?? string.Empty;
        var finnish = entity.GetString($"{pascalCaseKey}_fi") ?? entity.GetString($"{snakeCaseKey}_fi") ?? string.Empty;
        var amharic = entity.GetString($"{pascalCaseKey}_am") ?? entity.GetString($"{snakeCaseKey}_am") ?? string.Empty;

        // If no multilingual properties found, try to get the base property (for backward compatibility)
        if (string.IsNullOrEmpty(english))
        {
            english = entity.GetString(pascalCaseKey) ?? entity.GetString(snakeCaseKey) ?? string.Empty;
        }

        return new MultilingualContent(english, finnish, amharic);
    }

    /// <summary>
    /// Convert to TableEntity for storage
    /// </summary>
    public TableEntity ToTableEntity()
    { 
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { "Title_en", Title.English },
            { "Title_fi", Title.Finnish },
            { "Title_am", Title.Amharic },
            { "Description_en", Description.English },
            { "Description_fi", Description.Finnish },
            { "Description_am", Description.Amharic },
            { "Location_en", Location.English },
            { "Location_fi", Location.Finnish },
            { "Location_am", Location.Amharic },
            { "EventDate", EventDate }
        };
        return entity;
    }
}

