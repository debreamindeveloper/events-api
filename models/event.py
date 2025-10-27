"""Event model for Azure Table Storage."""
from datetime import datetime
from typing import Optional


class Event:
    """
    Event model representing a church event.
    
    Attributes:
        title: The title of the event
        description: A description of the event
        location: The location where the event takes place
        event_date: The date and time of the event
        partition_key: Azure Table Storage partition key
        row_key: Azure Table Storage row key
    """
    
    def __init__(
        self,
        title: str,
        description: str,
        location: str,
        event_date: datetime,
        partition_key: str = "EVENT",
        row_key: Optional[str] = None
    ):
        self.title = title
        self.description = description
        self.location = location
        self.event_date = event_date
        self.partition_key = partition_key
        self.row_key = row_key or self._generate_row_key()
    
    def _generate_row_key(self) -> str:
        """Generate a row key based on the event date and title."""
        date_str = self.event_date.strftime("%Y%m%d%H%M%S")
        title_slug = self.title.replace(" ", "_")[:20]
        return f"{date_str}_{title_slug}"
    
    def to_dict(self) -> dict:
        """Convert the event to a dictionary for JSON serialization."""
        return {
            "title": self.title,
            "description": self.description,
            "location": self.location,
            "eventDate": self.event_date.isoformat() if isinstance(self.event_date, datetime) else self.event_date,
            "partitionKey": self.partition_key,
            "rowKey": self.row_key
        }
    
    @staticmethod
    def from_table_entity(entity: dict) -> 'Event':
        """
        Create an Event instance from an Azure Table Storage entity.
        
        Args:
            entity: Dictionary representing a table entity
            
        Returns:
            Event instance
        """
        # Handle both datetime objects and ISO format strings
        event_date = entity.get('eventDate') or entity.get('EventDate')
        if isinstance(event_date, str):
            event_date = datetime.fromisoformat(event_date.replace('Z', '+00:00'))
        
        return Event(
            title=entity.get('title') or entity.get('Title', ''),
            description=entity.get('description') or entity.get('Description', ''),
            location=entity.get('location') or entity.get('Location', ''),
            event_date=event_date,
            partition_key=entity.get('PartitionKey', 'EVENT'),
            row_key=entity.get('RowKey', '')
        )
    
    def to_table_entity(self) -> dict:
        """
        Convert the event to an Azure Table Storage entity.
        
        Returns:
            Dictionary representing a table entity
        """
        return {
            'PartitionKey': self.partition_key,
            'RowKey': self.row_key,
            'title': self.title,
            'description': self.description,
            'location': self.location,
            'eventDate': self.event_date.isoformat() if isinstance(self.event_date, datetime) else self.event_date
        }

