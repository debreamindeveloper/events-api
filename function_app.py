"""Azure Functions app for Events API."""
import logging
import json
import os
from datetime import datetime
from typing import List

import azure.functions as func
from azure.data.tables import TableServiceClient
from azure.core.exceptions import ResourceNotFoundError

from models.event import Event

app = func.FunctionApp()


def get_table_client():
    """
    Get Azure Table Storage client.
    
    Returns:
        TableClient instance
    """
    connection_string = os.environ.get('AZURE_STORAGE_CONNECTION_STRING')
    table_name = os.environ.get('TABLE_NAME', 'events')
    
    if not connection_string:
        raise ValueError("AZURE_STORAGE_CONNECTION_STRING environment variable is not set")
    
    service_client = TableServiceClient.from_connection_string(connection_string)
    table_client = service_client.get_table_client(table_name)
    
    return table_client


@app.route(route="events", methods=["GET"], auth_level=func.AuthLevel.ANONYMOUS)
def get_events(req: func.HttpRequest) -> func.HttpResponse:
    """
    HTTP GET endpoint to retrieve all events from Azure Table Storage.
    
    Query Parameters:
        - limit: Maximum number of events to return (optional)
        - upcoming: If 'true', only return events with date >= today (optional)
    
    Returns:
        JSON array of events
    """
    logging.info('Processing GET request for events')
    
    try:
        # Get query parameters
        limit = req.params.get('limit')
        upcoming_only = req.params.get('upcoming', '').lower() == 'true'
        
        # Get table client
        table_client = get_table_client()
        
        # Query all entities
        entities = table_client.list_entities()
        
        # Convert entities to Event objects
        events: List[Event] = []
        for entity in entities:
            try:
                event = Event.from_table_entity(entity)
                
                # Filter upcoming events if requested
                if upcoming_only:
                    if isinstance(event.event_date, datetime):
                        if event.event_date >= datetime.now():
                            events.append(event)
                    else:
                        # If event_date is a string, try to parse it
                        try:
                            event_dt = datetime.fromisoformat(event.event_date.replace('Z', '+00:00'))
                            if event_dt >= datetime.now():
                                events.append(event)
                        except:
                            events.append(event)  # Include if we can't parse the date
                else:
                    events.append(event)
                    
            except Exception as e:
                logging.warning(f"Failed to parse entity: {e}")
                continue
        
        # Sort events by date (ascending)
        events.sort(key=lambda x: x.event_date if isinstance(x.event_date, datetime) 
                    else datetime.fromisoformat(x.event_date.replace('Z', '+00:00')))
        
        # Apply limit if specified
        if limit:
            try:
                limit_int = int(limit)
                events = events[:limit_int]
            except ValueError:
                logging.warning(f"Invalid limit parameter: {limit}")
        
        # Convert to dictionaries for JSON serialization
        events_data = [event.to_dict() for event in events]
        
        return func.HttpResponse(
            body=json.dumps(events_data, indent=2),
            mimetype="application/json",
            status_code=200
        )
        
    except ResourceNotFoundError:
        logging.error("Table not found")
        return func.HttpResponse(
            body=json.dumps({"error": "Events table not found"}),
            mimetype="application/json",
            status_code=404
        )
        
    except ValueError as e:
        logging.error(f"Configuration error: {e}")
        return func.HttpResponse(
            body=json.dumps({"error": str(e)}),
            mimetype="application/json",
            status_code=500
        )
        
    except Exception as e:
        logging.error(f"Error retrieving events: {e}")
        return func.HttpResponse(
            body=json.dumps({"error": "Internal server error"}),
            mimetype="application/json",
            status_code=500
        )


@app.route(route="events/{id}", methods=["GET"], auth_level=func.AuthLevel.ANONYMOUS)
def get_event_by_id(req: func.HttpRequest) -> func.HttpResponse:
    """
    HTTP GET endpoint to retrieve a single event by its row key.
    
    Path Parameters:
        - id: The row key of the event
    
    Returns:
        JSON object representing the event
    """
    logging.info('Processing GET request for single event')
    
    try:
        # Get the event ID from the route
        event_id = req.route_params.get('id')
        
        if not event_id:
            return func.HttpResponse(
                body=json.dumps({"error": "Event ID is required"}),
                mimetype="application/json",
                status_code=400
            )
        
        # Get table client
        table_client = get_table_client()
        
        # Query for the specific entity
        # Using partition key "EVENT" as default
        partition_key = "EVENT"
        
        try:
            entity = table_client.get_entity(partition_key=partition_key, row_key=event_id)
            event = Event.from_table_entity(entity)
            
            return func.HttpResponse(
                body=json.dumps(event.to_dict(), indent=2),
                mimetype="application/json",
                status_code=200
            )
            
        except ResourceNotFoundError:
            return func.HttpResponse(
                body=json.dumps({"error": f"Event with ID '{event_id}' not found"}),
                mimetype="application/json",
                status_code=404
            )
        
    except ValueError as e:
        logging.error(f"Configuration error: {e}")
        return func.HttpResponse(
            body=json.dumps({"error": str(e)}),
            mimetype="application/json",
            status_code=500
        )
        
    except Exception as e:
        logging.error(f"Error retrieving event: {e}")
        return func.HttpResponse(
            body=json.dumps({"error": "Internal server error"}),
            mimetype="application/json",
            status_code=500
        )

