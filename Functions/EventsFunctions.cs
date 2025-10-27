using Azure;
using Azure.Data.Tables;
using EventsAPI.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace EventsAPI.Functions;

/// <summary>
/// Azure Functions for Events API
/// </summary>
public class EventsFunctions
{
    private readonly ILogger<EventsFunctions> _logger;
    private readonly string _connectionString;
    private readonly string _tableName;

    public EventsFunctions(ILogger<EventsFunctions> logger)
    {
        _logger = logger;
        _connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
            ?? throw new InvalidOperationException("AZURE_STORAGE_CONNECTION_STRING environment variable is not set");
        _tableName = Environment.GetEnvironmentVariable("TABLE_NAME") ?? "events";
    }

    /// <summary>
    /// Get table client for Azure Table Storage
    /// </summary>
    private TableClient GetTableClient()
    {
        var serviceClient = new TableServiceClient(_connectionString);
        return serviceClient.GetTableClient(_tableName);
    }

    /// <summary>
    /// HTTP GET endpoint to retrieve all events from Azure Table Storage
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>JSON array of events</returns>
    [Function("GetEvents")]
    public async Task<HttpResponseData> GetEvents(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events")] HttpRequestData req)
    {
        _logger.LogInformation("Processing GET request for events");

        try
        {
            // Get query parameters
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var limitStr = query["limit"];
            var upcomingStr = query["upcoming"];

            bool upcomingOnly = upcomingStr?.ToLower() == "true";
            int? limit = null;
            if (int.TryParse(limitStr, out int parsedLimit))
            {
                limit = parsedLimit;
            }

            // Get table client
            var tableClient = GetTableClient();

            // Query all entities
            var entities = tableClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq 'events'");

            // Convert entities to Event objects
            var events = new List<Event>();
            await foreach (var entity in entities)
            {
                try
                {
                    var evt = Event.FromTableEntity(entity);

                    // Filter upcoming events if requested
                    if (upcomingOnly)
                    {
                        if (evt.EventDate >= DateTime.Now)
                        {
                            events.Add(evt);
                        }
                    }
                    else
                    {
                        events.Add(evt);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to parse entity: {ex.Message}");
                    continue;
                }
            }

            // Sort events by date (ascending)
            events = events.OrderBy(e => e.EventDate).ToList();

            // Apply limit if specified
            if (limit.HasValue)
            {
                events = events.Take(limit.Value).ToList();
            }

            // Create response
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(events, jsonOptions));
            return response;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogError("Table not found");
            var response = req.CreateResponse(HttpStatusCode.NotFound);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { error = "Events table not found" }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving events: {ex.Message}");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { error = "Internal server error" }));
            return response;
        }
    }

    /// <summary>
    /// HTTP GET endpoint to retrieve a single event by its row key
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="id">Event row key</param>
    /// <returns>JSON object representing the event</returns>
    [Function("GetEventById")]
    public async Task<HttpResponseData> GetEventById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation($"Processing GET request for event with ID: {id}");

        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                badResponse.Headers.Add("Content-Type", "application/json");
                await badResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Event ID is required" }));
                return badResponse;
            }

            // Get table client
            var tableClient = GetTableClient();

            // Query for the specific entity
            var partitionKey = "events";

            try
            {
                var entity = await tableClient.GetEntityAsync<TableEntity>(partitionKey, id);
                var evt = Event.FromTableEntity(entity.Value);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };

                await response.WriteStringAsync(JsonSerializer.Serialize(evt, jsonOptions));
                return response;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                notFoundResponse.Headers.Add("Content-Type", "application/json");
                await notFoundResponse.WriteStringAsync(
                    JsonSerializer.Serialize(new { error = $"Event with ID '{id}' not found" }));
                return notFoundResponse;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving event: {ex.Message}");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { error = "Internal server error" }));
            return response;
        }
    }
}

