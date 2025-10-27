using Azure;
using Azure.Data.Tables;
using System.Text.Json.Serialization;

namespace EventsAPI.Models;

/// <summary>
/// Event model representing a church event.
/// </summary>
public class Event : ITableEntity
{
    /// <summary>
    /// The title of the event
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// A description of the event
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The location where the event takes place
    /// </summary>
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

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
    /// Constructor with parameters
    /// </summary>
    public Event(string title, string description, string location, DateTime eventDate, string? rowKey = null)
    {
        Title = title;
        Description = description;
        Location = location;
        EventDate = eventDate;
        PartitionKey = "EVENT";
        RowKey = rowKey ?? GenerateRowKey(eventDate, title);
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
        return new Event
        {
            Title = entity.GetString("Title") ?? entity.GetString("title") ?? string.Empty,
            Description = entity.GetString("Description") ?? entity.GetString("description") ?? string.Empty,
            Location = entity.GetString("Location") ?? entity.GetString("location") ?? string.Empty,
            EventDate = entity.GetDateTime("EventDate") ?? entity.GetDateTime("event_date") ?? DateTime.MinValue,
            PartitionKey = entity.PartitionKey,
            RowKey = entity.RowKey,
            ETag = entity.ETag,
            Timestamp = entity.Timestamp
        };
    }

    /// <summary>
    /// Convert to TableEntity for storage
    /// </summary>
    public TableEntity ToTableEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { "Title", Title },
            { "Description", Description },
            { "Location", Location },
            { "EventDate", EventDate }
        };
        return entity;
    }
}

