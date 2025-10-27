# Events API - Azure Functions

An Azure Functions-based REST API for managing church events, with data stored in Azure Table Storage.

## Features

- **GET /api/events** - Retrieve all events
- **GET /api/events/{id}** - Retrieve a specific event by ID
- Query parameters for filtering and limiting results
- Simple event model with title, description, location, and event date

## Event Model

Each event contains the following fields:

- `title` - The title of the event
- `description` - A description of the event
- `location` - The location where the event takes place
- `eventDate` - The date and time of the event (ISO 8601 format)
- `partitionKey` - Azure Table Storage partition key (default: "EVENT")
- `rowKey` - Azure Table Storage row key (unique identifier)

## Prerequisites

- Python 3.8 or higher
- Azure Functions Core Tools (v4)
- Azure Storage Account
- Azure Storage Explorer (optional, for managing table data)

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

### 2. Install Python Dependencies

```bash
pip install -r requirements.txt
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
    "FUNCTIONS_WORKER_RUNTIME": "python",
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

**Using Python:**
```python
from azure.data.tables import TableServiceClient

connection_string = "YOUR_CONNECTION_STRING"
service_client = TableServiceClient.from_connection_string(connection_string)
table_client = service_client.create_table_if_not_exists("events")
```

### 6. Add Sample Data (Optional)

You can add sample events to test the API:

```python
from azure.data.tables import TableServiceClient
from datetime import datetime, timedelta

connection_string = "YOUR_CONNECTION_STRING"
service_client = TableServiceClient.from_connection_string(connection_string)
table_client = service_client.get_table_client("events")

# Sample event
event = {
    'PartitionKey': 'EVENT',
    'RowKey': '20250201120000_Sunday_Service',
    'title': 'Sunday Service',
    'description': 'Weekly Sunday worship service',
    'location': 'Main Sanctuary',
    'eventDate': (datetime.now() + timedelta(days=7)).isoformat()
}

table_client.create_entity(event)
```

## Running Locally

1. Start the Azure Functions runtime:
   ```bash
   func start
   ```

2. The API will be available at `http://localhost:7071/api/events`

## API Endpoints

### Get All Events

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
    "title": "Sunday Service",
    "description": "Weekly Sunday worship service",
    "location": "Main Sanctuary",
    "eventDate": "2025-02-01T12:00:00",
    "partitionKey": "EVENT",
    "rowKey": "20250201120000_Sunday_Service"
  }
]
```

### Get Event by ID

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
  "title": "Sunday Service",
  "description": "Weekly Sunday worship service",
  "location": "Main Sanctuary",
  "eventDate": "2025-02-01T12:00:00",
  "partitionKey": "EVENT",
  "rowKey": "20250201120000_Sunday_Service"
}
```

## Deployment to Azure

### 1. Create Azure Resources

```bash
# Create resource group
az group create --name EventsAPIResourceGroup --location eastus

# Create storage account
az storage account create --name eventsstorageacct --resource-group EventsAPIResourceGroup --location eastus --sku Standard_LRS

# Create function app
az functionapp create --resource-group EventsAPIResourceGroup --consumption-plan-location eastus --runtime python --runtime-version 3.9 --functions-version 4 --name events-api-function --storage-account eventsstorageacct
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
├── function_app.py          # Main Azure Functions app with HTTP endpoints
├── models/
│   ├── __init__.py
│   └── event.py            # Event model class
├── host.json               # Azure Functions host configuration
├── local.settings.json     # Local development settings
├── requirements.txt        # Python dependencies
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

