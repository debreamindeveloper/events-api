using Azure;
using Azure.Data.Tables;
using System.Text.Json.Serialization;

namespace EventsAPI.Models;

/// <summary>
/// OpeningHours model representing church opening hours for a specific day with multilingual support.
/// </summary>
public class OpeningHours : ITableEntity
{
    /// <summary>
    /// Day of the week (0-6, where 0 is Sunday)
    /// </summary>
    [JsonPropertyName("dayOfWeek")]
    public int DayOfWeek { get; set; }

    /// <summary>
    /// Name of the day in multiple languages (e.g., "Monday", "Maanantai", "ሰኞ")
    /// </summary>
    [JsonPropertyName("dayName")]
    public MultilingualContent DayName { get; set; } = new();

    /// <summary>
    /// Opening time in HH:mm format (e.g., "09:00")
    /// </summary>
    [JsonPropertyName("openTime")]
    public string OpenTime { get; set; } = string.Empty;

    /// <summary>
    /// Closing time in HH:mm format (e.g., "17:00")
    /// </summary>
    [JsonPropertyName("closeTime")]
    public string CloseTime { get; set; } = string.Empty;

    /// <summary>
    /// Whether the location is closed on this day
    /// </summary>
    [JsonPropertyName("isClosed")]
    public bool IsClosed { get; set; }

    /// <summary>
    /// Azure Table Storage partition key
    /// </summary>
    [JsonPropertyName("partitionKey")]
    public string PartitionKey { get; set; } = "openinghours";

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
    public OpeningHours()
    {
    }

    /// <summary>
    /// Constructor with parameters (English day name only)
    /// </summary>
    public OpeningHours(int dayOfWeek, string dayName, string openTime, string closeTime, bool isClosed = false)
    {
        DayOfWeek = dayOfWeek;
        DayName = new MultilingualContent(dayName);
        OpenTime = openTime;
        CloseTime = closeTime;
        IsClosed = isClosed;
        PartitionKey = "openinghours";
        RowKey = dayOfWeek.ToString();
    }

    /// <summary>
    /// Constructor with multilingual day name
    /// </summary>
    public OpeningHours(int dayOfWeek, MultilingualContent dayName, string openTime, string closeTime, bool isClosed = false)
    {
        DayOfWeek = dayOfWeek;
        DayName = dayName;
        OpenTime = openTime;
        CloseTime = closeTime;
        IsClosed = isClosed;
        PartitionKey = "openinghours";
        RowKey = dayOfWeek.ToString();
    }

    /// <summary>
    /// Create an OpeningHours from a TableEntity
    /// </summary>
    public static OpeningHours FromTableEntity(TableEntity entity)
    {
        // Parse day of week
        var dayOfWeek = entity.GetInt32("DayOfWeek") ?? entity.GetInt32("day_of_week") ?? 0;

        // Parse multilingual day name
        var dayName = ParseMultilingualDayName(entity);

        return new OpeningHours
        {
            DayOfWeek = dayOfWeek,
            DayName = dayName,
            OpenTime = entity.GetString("OpenTime") ?? entity.GetString("open_time") ?? string.Empty,
            CloseTime = entity.GetString("CloseTime") ?? entity.GetString("close_time") ?? string.Empty,
            IsClosed = entity.GetBoolean("IsClosed") ?? entity.GetBoolean("is_closed") ?? false,
            PartitionKey = entity.PartitionKey,
            RowKey = entity.RowKey,
            ETag = entity.ETag,
            Timestamp = entity.Timestamp
        };
    }

    /// <summary>
    /// Parse multilingual day name from TableEntity
    /// </summary>
    private static MultilingualContent ParseMultilingualDayName(TableEntity entity)
    {
        // Try to get multilingual properties (en, fi, am suffixes)
        var english = entity.GetString("DayName_en") ?? entity.GetString("day_name_en") ?? string.Empty;
        var finnish = entity.GetString("DayName_fi") ?? entity.GetString("day_name_fi") ?? string.Empty;
        var amharic = entity.GetString("DayName_am") ?? entity.GetString("day_name_am") ?? string.Empty;

        // If no multilingual properties found, try to get the base property (for backward compatibility)
        if (string.IsNullOrEmpty(english))
        {
            english = entity.GetString("DayName") ?? entity.GetString("day_name") ?? string.Empty;
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
            { "DayOfWeek", DayOfWeek },
            { "DayName_en", DayName.English },
            { "DayName_fi", DayName.Finnish },
            { "DayName_am", DayName.Amharic },
            { "OpenTime", OpenTime },
            { "CloseTime", CloseTime },
            { "IsClosed", IsClosed }
        };
        return entity;
    }
}

