using Azure;
using Azure.Data.Tables;
using EventsAPI.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Linq;

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
    /// Get table client for the opening hours table
    /// </summary>
    private TableClient GetOpeningHoursTableClient()
    {
        var serviceClient = new TableServiceClient(_connectionString);
        return serviceClient.GetTableClient("openinghours");
    }

    /// <summary>
    /// Serialize a payload to JSON and return it as an HTTP response with the given status code
    /// </summary>
    private static async Task<HttpResponseData> WriteJson(HttpRequestData req, HttpStatusCode statusCode, object payload)
    {
        var response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json");

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        await response.WriteStringAsync(JsonSerializer.Serialize(payload, jsonOptions));
        return response;
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

    /// <summary>
    /// HTTP POST endpoint to create a new event
    /// </summary>
    /// <param name="req">HTTP request with Event JSON body</param>
    /// <returns>JSON object representing the created event</returns>
    [Function("CreateEvent")]
    public async Task<HttpResponseData> CreateEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events")] HttpRequestData req)
    {
        _logger.LogInformation("Processing POST request to create event");

        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = "Request body is required" });
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            Event? evt;
            try
            {
                evt = JsonSerializer.Deserialize<Event>(body, jsonOptions);
            }
            catch (JsonException ex)
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = $"Invalid JSON: {ex.Message}" });
            }

            if (evt == null || evt.Title == null || string.IsNullOrWhiteSpace(evt.Title.English))
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = "Event title (en) is required" });
            }

            if (evt.EventDate == DateTime.MinValue)
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = "Event date is required" });
            }

            evt.PartitionKey = "events";
            if (string.IsNullOrWhiteSpace(evt.RowKey))
            {
                var dateStr = evt.EventDate.ToString("yyyyMMddHHmmss");
                var titleSlug = evt.Title.English.Replace(" ", "_");
                if (titleSlug.Length > 20) titleSlug = titleSlug.Substring(0, 20);
                evt.RowKey = $"{dateStr}_{titleSlug}";
            }

            var tableClient = GetTableClient();
            await tableClient.CreateIfNotExistsAsync();

            try
            {
                await tableClient.AddEntityAsync(evt.ToTableEntity());
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                return await WriteJson(req, HttpStatusCode.Conflict,
                    new { error = $"Event with ID '{evt.RowKey}' already exists" });
            }

            return await WriteJson(req, HttpStatusCode.Created, evt);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating event: {ex.Message}");
            return await WriteJson(req, HttpStatusCode.InternalServerError, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// HTTP PUT endpoint to update an existing event
    /// </summary>
    /// <param name="req">HTTP request with Event JSON body</param>
    /// <param name="id">Event row key</param>
    /// <returns>JSON object representing the updated event</returns>
    [Function("UpdateEvent")]
    public async Task<HttpResponseData> UpdateEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "events/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation($"Processing PUT request for event with ID: {id}");

        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = "Event ID is required" });
            }

            var body = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = "Request body is required" });
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            Event? evt;
            try
            {
                evt = JsonSerializer.Deserialize<Event>(body, jsonOptions);
            }
            catch (JsonException ex)
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = $"Invalid JSON: {ex.Message}" });
            }

            if (evt == null)
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = "Invalid event payload" });
            }

            evt.PartitionKey = "events";
            evt.RowKey = id;

            var tableClient = GetTableClient();

            try
            {
                await tableClient.GetEntityAsync<TableEntity>("events", id);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return await WriteJson(req, HttpStatusCode.NotFound, new { error = $"Event with ID '{id}' not found" });
            }

            await tableClient.UpdateEntityAsync(evt.ToTableEntity(), ETag.All, TableUpdateMode.Replace);

            return await WriteJson(req, HttpStatusCode.OK, evt);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating event: {ex.Message}");
            return await WriteJson(req, HttpStatusCode.InternalServerError, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// HTTP DELETE endpoint to delete an event by its row key
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="id">Event row key</param>
    /// <returns>204 No Content on success</returns>
    [Function("DeleteEvent")]
    public async Task<HttpResponseData> DeleteEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "events/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation($"Processing DELETE request for event with ID: {id}");

        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = "Event ID is required" });
            }

            var tableClient = GetTableClient();

            try
            {
                await tableClient.DeleteEntityAsync("events", id);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return await WriteJson(req, HttpStatusCode.NotFound, new { error = $"Event with ID '{id}' not found" });
            }

            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting event: {ex.Message}");
            return await WriteJson(req, HttpStatusCode.InternalServerError, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// HTTP GET endpoint to retrieve all opening hours from Azure Table Storage
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>JSON array of opening hours</returns>
    [Function("GetOpeningHours")]
    public async Task<HttpResponseData> GetOpeningHours(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "openinghours")] HttpRequestData req)
    {
        _logger.LogInformation("Processing GET request for opening hours");

        try
        {
            // Get table client for openinghours table
            var serviceClient = new TableServiceClient(_connectionString);
            var tableClient = serviceClient.GetTableClient("openinghours");

            // Query all entities
            var entities = tableClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq 'openinghours'");

            // Convert entities to OpeningHours objects
            var openingHours = new List<OpeningHours>();
            await foreach (var entity in entities)
            {
                try
                {
                    var hours = OpeningHours.FromTableEntity(entity);
                    openingHours.Add(hours);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to parse opening hours entity: {ex.Message}");
                    continue;
                }
            }

            // Sort by day of week (0-6)
            openingHours = openingHours.OrderBy(h => h.DayOfWeek).ToList();

            // Create response
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(openingHours, jsonOptions));
            return response;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogError("Opening hours table not found");
            var response = req.CreateResponse(HttpStatusCode.NotFound);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { error = "Opening hours table not found" }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving opening hours: {ex.Message}");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { error = "Internal server error" }));
            return response;
        }
    }

    /// <summary>
    /// HTTP GET endpoint to retrieve opening hours for a specific day
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="dayOfWeek">Day of week (0-6, where 0 is Sunday)</param>
    /// <returns>JSON object representing the opening hours for that day</returns>
    [Function("GetOpeningHoursByDay")]
    public async Task<HttpResponseData> GetOpeningHoursByDay(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "openinghours/{dayOfWeek}")] HttpRequestData req,
        string dayOfWeek)
    {
        _logger.LogInformation($"Processing GET request for opening hours on day: {dayOfWeek}");

        try
        {
            if (string.IsNullOrWhiteSpace(dayOfWeek))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                badResponse.Headers.Add("Content-Type", "application/json");
                await badResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Day of week is required" }));
                return badResponse;
            }

            // Get table client for openinghours table
            var serviceClient = new TableServiceClient(_connectionString);
            var tableClient = serviceClient.GetTableClient("openinghours");

            // Query for the specific entity
            var partitionKey = "openinghours";

            try
            {
                var entity = await tableClient.GetEntityAsync<TableEntity>(partitionKey, dayOfWeek);
                var hours = OpeningHours.FromTableEntity(entity.Value);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };

                await response.WriteStringAsync(JsonSerializer.Serialize(hours, jsonOptions));
                return response;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                notFoundResponse.Headers.Add("Content-Type", "application/json");
                await notFoundResponse.WriteStringAsync(
                    JsonSerializer.Serialize(new { error = $"Opening hours for day '{dayOfWeek}' not found" }));
                return notFoundResponse;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving opening hours: {ex.Message}");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { error = "Internal server error" }));
            return response;
        }
    }

    /// <summary>
    /// HTTP POST endpoint to create opening hours for a day
    /// </summary>
    /// <param name="req">HTTP request with OpeningHours JSON body</param>
    /// <returns>JSON object representing the created opening hours</returns>
    [Function("CreateOpeningHours")]
    public async Task<HttpResponseData> CreateOpeningHours(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "openinghours")] HttpRequestData req)
    {
        _logger.LogInformation("Processing POST request to create opening hours");

        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = "Request body is required" });
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            OpeningHours? hours;
            try
            {
                hours = JsonSerializer.Deserialize<OpeningHours>(body, jsonOptions);
            }
            catch (JsonException ex)
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = $"Invalid JSON: {ex.Message}" });
            }

            if (hours == null)
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = "Invalid opening hours payload" });
            }

            if (hours.DayOfWeek < 0 || hours.DayOfWeek > 6)
            {
                return await WriteJson(req, HttpStatusCode.BadRequest,
                    new { error = "dayOfWeek must be between 0 (Sunday) and 6 (Saturday)" });
            }

            hours.PartitionKey = "openinghours";
            hours.RowKey = hours.DayOfWeek.ToString();

            var tableClient = GetOpeningHoursTableClient();
            await tableClient.CreateIfNotExistsAsync();

            try
            {
                await tableClient.AddEntityAsync(hours.ToTableEntity());
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                return await WriteJson(req, HttpStatusCode.Conflict,
                    new { error = $"Opening hours for day '{hours.DayOfWeek}' already exist" });
            }

            return await WriteJson(req, HttpStatusCode.Created, hours);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating opening hours: {ex.Message}");
            return await WriteJson(req, HttpStatusCode.InternalServerError, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// HTTP PUT endpoint to update opening hours for a specific day
    /// </summary>
    /// <param name="req">HTTP request with OpeningHours JSON body</param>
    /// <param name="dayOfWeek">Day of week (0-6, where 0 is Sunday)</param>
    /// <returns>JSON object representing the updated opening hours</returns>
    [Function("UpdateOpeningHours")]
    public async Task<HttpResponseData> UpdateOpeningHours(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "openinghours/{dayOfWeek}")] HttpRequestData req,
        string dayOfWeek)
    {
        _logger.LogInformation($"Processing PUT request for opening hours on day: {dayOfWeek}");

        try
        {
            if (string.IsNullOrWhiteSpace(dayOfWeek))
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = "Day of week is required" });
            }

            if (!int.TryParse(dayOfWeek, out int day) || day < 0 || day > 6)
            {
                return await WriteJson(req, HttpStatusCode.BadRequest,
                    new { error = "dayOfWeek must be an integer between 0 (Sunday) and 6 (Saturday)" });
            }

            var body = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = "Request body is required" });
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            OpeningHours? hours;
            try
            {
                hours = JsonSerializer.Deserialize<OpeningHours>(body, jsonOptions);
            }
            catch (JsonException ex)
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = $"Invalid JSON: {ex.Message}" });
            }

            if (hours == null)
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = "Invalid opening hours payload" });
            }

            hours.DayOfWeek = day;
            hours.PartitionKey = "openinghours";
            hours.RowKey = dayOfWeek;

            var tableClient = GetOpeningHoursTableClient();

            try
            {
                await tableClient.GetEntityAsync<TableEntity>("openinghours", dayOfWeek);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return await WriteJson(req, HttpStatusCode.NotFound,
                    new { error = $"Opening hours for day '{dayOfWeek}' not found" });
            }

            await tableClient.UpdateEntityAsync(hours.ToTableEntity(), ETag.All, TableUpdateMode.Replace);

            return await WriteJson(req, HttpStatusCode.OK, hours);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating opening hours: {ex.Message}");
            return await WriteJson(req, HttpStatusCode.InternalServerError, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// HTTP DELETE endpoint to delete opening hours for a specific day
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="dayOfWeek">Day of week (0-6, where 0 is Sunday)</param>
    /// <returns>204 No Content on success</returns>
    [Function("DeleteOpeningHours")]
    public async Task<HttpResponseData> DeleteOpeningHours(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "openinghours/{dayOfWeek}")] HttpRequestData req,
        string dayOfWeek)
    {
        _logger.LogInformation($"Processing DELETE request for opening hours on day: {dayOfWeek}");

        try
        {
            if (string.IsNullOrWhiteSpace(dayOfWeek))
            {
                return await WriteJson(req, HttpStatusCode.BadRequest, new { error = "Day of week is required" });
            }

            var tableClient = GetOpeningHoursTableClient();

            try
            {
                await tableClient.DeleteEntityAsync("openinghours", dayOfWeek);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return await WriteJson(req, HttpStatusCode.NotFound,
                    new { error = $"Opening hours for day '{dayOfWeek}' not found" });
            }

            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting opening hours: {ex.Message}");
            return await WriteJson(req, HttpStatusCode.InternalServerError, new { error = "Internal server error" });
        }
    }
}

