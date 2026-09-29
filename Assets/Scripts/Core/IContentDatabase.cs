using System.Collections.Generic;

namespace LastRefuge.Core
{
    public interface IContentDatabase
    {
        IReadOnlyCollection<EventDefinition> Events { get; }
        IReadOnlyCollection<LocationDefinition> Locations { get; }
        IReadOnlyList<string> Diagnostics { get; }

        bool TryGetEvent(string id, out EventDefinition definition);
        bool TryGetLocation(string id, out LocationDefinition definition);
        bool AddEvent(EventDefinition definition);
        bool AddLocation(LocationDefinition definition);
        EventDefinition[] GetEventPool(string locationId);
        List<string> Validate();
    }
}
