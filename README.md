# Events API - Azure Functions (C#)

An Azure Functions-based REST API for managing church events, with data stored in Azure Table Storage. Built with C# and .NET 8.

## Features

- **Multilingual Support**: Title, description, and location in English, Finnish, and Amharic
- **GET /api/events** - Retrieve all events
- **GET /api/events/{id}** - Retrieve a specific event by ID
- **GET /api/openinghours** - Retrieve all opening hours
- **GET /api/openinghours/{dayOfWeek}** - Retrieve opening hours for a specific day
- Query parameters for filtering and limiting results
- Event model with multilingual title, description, location, and event date
- Opening hours model with day of week, day name, open/close times

## Event Model

Each event contains the following fields with multilingual support:

- `title` - The title of the event (English, Finnish, Amharic)
- `description` - A description of the event (English, Finnish, Amharic)
- `location` - The location where the event takes place (English, Finnish, Amharic)
- `eventDate` - The date and time of the event (ISO 8601 format)
- `partitionKey` - Azure Table Storage partition key (default: "events")
- `rowKey` - Azure Table Storage row key (unique identifier)

## Prerequisites

- .NET 8 SDK or higher
- Azure Functions Core Tools (v4)
- Azure Storage Account
- Azure Storage Explorer (optional, for managing table data)
- Visual Studio 2022, VS Code, or Rider (optional, for development)

## Setup Instructions

### 1. Install Azure Functions Core Tools

```bash
# Windows (using npm)
npm install -g azure-functions-core-tools@4 --unsafe-perm true

# Or using Chocolatey
choco install azure-functions-core-tools-4

# macOS
brew tap azure/functions
brew install azure-functions-core-tools@4

# Linux
# See: https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local
```

### 2. Restore NuGet Packages

```bash
dotnet restore
```

### 3. Configure Azure Storage

#### Option A: Use Azure Storage Account (Production)

1. Create an Azure Storage Account in the Azure Portal
2. Navigate to "Access keys" and copy the connection string
3. Create a table named `events` in the storage account

#### Option B: Use Azurite (Local Development)

1. Install Azurite:
   ```bash
   npm install -g azurite
   ```

2. Start Azurite:
   ```bash
   azurite --silent --location c:\azurite --debug c:\azurite\debug.log
   ```

3. Use the default connection string for local development

### 4. Configure Local Settings

Update `local.settings.json` with your configuration:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AZURE_STORAGE_CONNECTION_STRING": "YOUR_CONNECTION_STRING_HERE",
    "TABLE_NAME": "events"
  }
}
```

**For local development with Azurite:**
```json
"AZURE_STORAGE_CONNECTION_STRING": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;"
```

### 5. Create the Events Table

You can create the table using Azure Storage Explorer or programmatically:

**Using C#:**
```csharp
using Azure.Data.Tables;

var connectionString = "YOUR_CONNECTION_STRING";
var serviceClient = new TableServiceClient(connectionString);
await serviceClient.CreateTableIfNotExistsAsync("events");
```

**Using Azure CLI:**
```bash
az storage table create --name events --connection-string "YOUR_CONNECTION_STRING"
```

### 6. Add Sample Data (Optional)

You can add sample events to test the API:

**Using C#:**
```csharp
using Azure.Data.Tables;

var connectionString = "YOUR_CONNECTION_STRING";
var serviceClient = new TableServiceClient(connectionString);
var tableClient = serviceClient.GetTableClient("events");

// Sample event
var entity = new TableEntity("EVENT", "20250201120000_Sunday_Service")
{
    { "Title", "Sunday Service" },
    { "Description", "Weekly Sunday worship service" },
    { "Location", "Main Sanctuary" },
    { "EventDate", DateTime.Now.AddDays(7) }
};

await tableClient.AddEntityAsync(entity);
```

**Using Azure Storage Explorer:**
1. Open Azure Storage Explorer
2. Navigate to your storage account → Tables → events
3. Click "Add" to create a new entity
4. Set PartitionKey to "EVENT" and RowKey to a unique identifier
5. Add properties: Title, Description, Location, EventDate

## Running Locally

### Option 1: Using Azure Functions Core Tools

```bash
func start
```

### Option 2: Using .NET CLI

```bash
dotnet build
dotnet run
```

### Option 3: Using Visual Studio

1. Open the project in Visual Studio 2022
2. Press F5 to start debugging

The API will be available at `http://localhost:7071/api/events`

## API Endpoints

### Events Endpoints

#### Get All Events

```http
GET /api/events
```

**Query Parameters:**
- `limit` (optional) - Maximum number of events to return
- `upcoming` (optional) - Set to `true` to only return upcoming events

**Example:**
```bash
curl http://localhost:7071/api/events?upcoming=true&limit=10
```

**Response:**
```json
[
  {
    "title": {
      "en": "Sunday Service",
      "fi": "Sunnuntain jumalanpalvelus",
      "am": "ሰንበት ሰዓት"
    },
    "description": {
      "en": "Weekly Sunday worship service",
      "fi": "Viikoittainen sunnuntain jumalanpalvelus",
      "am": "ሳምንታዊ ሰንበት ሰዓት"
    },
    "location": {
      "en": "Main Sanctuary",
      "fi": "Pääsankttuaario",
      "am": "ዋና ቅዱስ ቦታ"
    },
    "eventDate": "2025-02-01T12:00:00",
    "partitionKey": "events",
    "rowKey": "20250201120000_Sunday_Service"
  }
]
```

**Multilingual Format:**
Each text field (title, description, location) contains translations in three languages:
- `en` - English
- `fi` - Finnish
- `am` - Amharic

#### Get Event by ID

```http
GET /api/events/{rowKey}
```

**Example:**
```bash
curl http://localhost:7071/api/events/20250201120000_Sunday_Service
```

**Response:**
```json
{
  "title": {
    "en": "Sunday Service",
    "fi": "Sunnuntain jumalanpalvelus",
    "am": "ሰንበት ሰዓት"
  },
  "description": {
    "en": "Weekly Sunday worship service",
    "fi": "Viikoittainen sunnuntain jumalanpalvelus",
    "am": "ሳምንታዊ ሰንበት ሰዓት"
  },
  "location": {
    "en": "Main Sanctuary",
    "fi": "Pääsankttuaario",
    "am": "ዋና ቅዱስ ቦታ"
  },
  "eventDate": "2025-02-01T12:00:00",
  "partitionKey": "events",
  "rowKey": "20250201120000_Sunday_Service"
}
```

### Opening Hours Endpoints

#### Get All Opening Hours

```http
GET /api/openinghours
```

**Example:**
```bash
curl http://localhost:7071/api/openinghours
```

**Response:**
```json
[
  {
    "dayOfWeek": 0,
    "dayName": "Sunday",
    "openTime": "10:00",
    "closeTime": "18:00",
    "isClosed": false,
    "partitionKey": "openinghours",
    "rowKey": "0"
  },
  {
    "dayOfWeek": 1,
    "dayName": "Monday",
    "openTime": "09:00",
    "closeTime": "17:00",
    "isClosed": false,
    "partitionKey": "openinghours",
    "rowKey": "1"
  }
]
```

#### Get Opening Hours by Day

```http
GET /api/openinghours/{dayOfWeek}
```

**Path Parameters:**
- `dayOfWeek` - Day of week (0-6, where 0 is Sunday)

**Example:**
```bash
curl http://localhost:7071/api/openinghours/0
```

**Response:**
```json
{
  "dayOfWeek": 0,
  "dayName": "Sunday",
  "openTime": "10:00",
  "closeTime": "18:00",
  "isClosed": false,
  "partitionKey": "openinghours",
  "rowKey": "0"
}
```

## Creating Events with Multilingual Content

When adding events to the Azure Table Storage, you can provide content in multiple languages. The table should have the following columns for each language:

**For Title:**
- `Title_en` - English title
- `Title_fi` - Finnish title
- `Title_am` - Amharic title

**For Description:**
- `Description_en` - English description
- `Description_fi` - Finnish description
- `Description_am` - Amharic description

**For Location:**
- `Location_en` - English location
- `Location_fi` - Finnish location
- `Location_am` - Amharic location

**Example Table Row:**
```
PartitionKey: events
RowKey: 20250201120000_Sunday_Service
Title_en: Sunday Service
Title_fi: Sunnuntain jumalanpalvelus
Title_am: ሰንበት ሰዓት
Description_en: Weekly Sunday worship service
Description_fi: Viikoittainen sunnuntain jumalanpalvelus
Description_am: ሳምንታዊ ሰንበት ሰዓት
Location_en: Main Sanctuary
Location_fi: Pääsankttuaario
Location_am: ዋና ቅዱስ ቦታ
EventDate: 2025-02-01T12:00:00
```

**Backward Compatibility:**
If you have existing events with simple string fields (Title, Description, Location), the API will automatically convert them to multilingual format with the English content as the primary language.

## Deployment to Azure

### 1. Create Azure Resources

```bash
# Create resource group
az group create --name EventsAPIResourceGroup --location eastus

# Create storage account
az storage account create --name eventsstorageacct --resource-group EventsAPIResourceGroup --location eastus --sku Standard_LRS

# Create function app
az functionapp create --resource-group EventsAPIResourceGroup --consumption-plan-location eastus --runtime dotnet-isolated --runtime-version 8 --functions-version 4 --name events-api-function --storage-account eventsstorageacct
```

### 2. Configure Application Settings

```bash
# Get storage connection string
CONNECTION_STRING=$(az storage account show-connection-string --name eventsstorageacct --resource-group EventsAPIResourceGroup --query connectionString --output tsv)

# Set application settings
az functionapp config appsettings set --name events-api-function --resource-group EventsAPIResourceGroup --settings "AZURE_STORAGE_CONNECTION_STRING=$CONNECTION_STRING" "TABLE_NAME=events"
```

### 3. Deploy the Function

```bash
func azure functionapp publish events-api-function
```

## Project Structure

```
Events-API/
├── Functions/
│   └── EventsFunctions.cs       # Azure Functions HTTP endpoints
├── Models/
│   ├── Event.cs                 # Event model class
│   └── OpeningHours.cs          # OpeningHours model class
├── Program.cs                   # Application entry point
├── EventsAPI.csproj             # Project file with dependencies
├── host.json                    # Azure Functions host configuration
├── local.settings.json          # Local development settings
├── .gitignore
└── README.md
```

## Error Handling

The API includes comprehensive error handling:

- **404** - Table or event not found
- **400** - Invalid request parameters
- **500** - Internal server error or configuration issues

All errors return JSON responses with an `error` field describing the issue.

## Development Tips

1. **Testing locally**: Use Azurite for local development to avoid Azure costs
2. **Debugging**: Check the Azure Functions runtime logs for detailed error messages
3. **Table Storage**: Use Azure Storage Explorer to view and manage table data
4. **CORS**: Configure CORS in `host.json` if accessing from a web application

## License

This project is for church communications committee use.

