using System.Collections.Generic;

namespace LastRefuge.Core
{
    public class ContentDatabase : IContentDatabase
    {
        private readonly Dictionary<string, EventDefinition> events = new Dictionary<string, EventDefinition>();
        private readonly Dictionary<string, LocationDefinition> locations = new Dictionary<string, LocationDefinition>();
        private readonly List<string> diagnostics = new List<string>();

        public IReadOnlyCollection<EventDefinition> Events => events.Values;
        public IReadOnlyCollection<LocationDefinition> Locations => locations.Values;
        public IReadOnlyList<string> Diagnostics => diagnostics;

        public void AddDiagnostic(string message)
        {
            diagnostics.Add(message);
        }

        public bool AddEvent(EventDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.id)) return false;
            if (events.ContainsKey(definition.id)) return false;
            events[definition.id] = definition;
            return true;
        }

        public bool AddLocation(LocationDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.id)) return false;
            if (locations.ContainsKey(definition.id)) return false;
            locations[definition.id] = definition;
            return true;
        }

        public bool TryGetEvent(string id, out EventDefinition definition)
        {
            if (string.IsNullOrEmpty(id)) { definition = null; return false; }
            return events.TryGetValue(id, out definition);
        }

        public bool TryGetLocation(string id, out LocationDefinition definition)
        {
            if (string.IsNullOrEmpty(id)) { definition = null; return false; }
            return locations.TryGetValue(id, out definition);
        }

        public EventDefinition[] GetEventPool(string locationId)
        {
            if (!TryGetLocation(locationId, out var location)) return new EventDefinition[0];

            var pool = new List<EventDefinition>();
            if (location.eventPool == null) return pool.ToArray();

            foreach (var eventId in location.eventPool)
            {
                if (TryGetEvent(eventId, out var evt)) pool.Add(evt);
            }
            return pool.ToArray();
        }

        public List<string> Validate()
        {
            var found = new List<string>();

            foreach (var evt in events.Values)
            {
                if (evt.followUpEvents != null)
                {
                    foreach (var followUp in evt.followUpEvents)
                    {
                        if (!events.ContainsKey(followUp.id))
                        {
                            found.Add($"event {evt.id}: followUp \"{followUp.id}\" does not exist");
                        }
                    }
                }

                if (evt.options == null) continue;
                foreach (var option in evt.options)
                {
                    if (option?.effects == null) continue;
                    foreach (var effect in option.effects)
                    {
                        if (effect == null) continue;
                        if (effect.type == EffectType.StartEvent && !events.ContainsKey(effect.targetId))
                        {
                            found.Add($"event {evt.id}, option {option.id}: StartEvent \"{effect.targetId}\" does not exist");
                        }
                        if (effect.type == EffectType.UnlockLocation && !locations.ContainsKey(effect.targetId))
                        {
                            found.Add($"event {evt.id}, option {option.id}: UnlockLocation \"{effect.targetId}\" does not exist");
                        }
                    }
                }
            }

            foreach (var location in locations.Values)
            {
                if (location.eventPool != null)
                {
                    foreach (var eventId in location.eventPool)
                    {
                        if (!events.ContainsKey(eventId))
                        {
                            found.Add($"location {location.id}: eventPool \"{eventId}\" does not exist");
                        }
                    }
                }
                if (location.prerequisites != null)
                {
                    foreach (var prerequisite in location.prerequisites)
                    {
                        if (!locations.ContainsKey(prerequisite))
                        {
                            found.Add($"location {location.id}: prerequisite \"{prerequisite}\" does not exist");
                        }
                    }
                }
            }

            found.AddRange(FindFollowUpCycles());

            diagnostics.AddRange(found);
            return found;
        }

        private List<string> FindFollowUpCycles()
        {
            var found = new List<string>();
            var visiting = new HashSet<string>();
            var visited = new HashSet<string>();

            foreach (var start in events.Keys)
            {
                if (VisitFollowUps(start, visiting, visited, found)) break;
            }
            return found;
        }

        private bool VisitFollowUps(string eventId, HashSet<string> visiting, HashSet<string> visited, List<string> found)
        {
            if (visited.Contains(eventId)) return false;
            if (!visiting.Add(eventId))
            {
                found.Add($"followUp cycle detected at \"{eventId}\"");
                return true;
            }

            if (events.TryGetValue(eventId, out var evt) && evt.followUpEvents != null)
            {
                foreach (var followUp in evt.followUpEvents)
                {
                    if (events.ContainsKey(followUp.id) && VisitFollowUps(followUp.id, visiting, visited, found))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(eventId);
            visited.Add(eventId);
            return false;
        }
    }
}
