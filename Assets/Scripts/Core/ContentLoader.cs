using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using LastRefuge.Data;

namespace LastRefuge.Core
{
    public static class ContentLoader
    {
        public const string EventsPath = "GameData/Events";
        public const string LocationsPath = "GameData/Locations";

        private static readonly Regex ValidId = new Regex("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

        [Serializable]
        private class EventFile
        {
            public EventRaw @event;
        }

        [Serializable]
        private class EventRaw
        {
            public string id;
            public string name;
            public string type;
            public string phase;
            public float weight;
            public int cooldownDays;
            public int minDay;
            public int maxDay;
            public ConditionRaw[] conditions;
            public OptionRaw[] options;
            public FollowUpRaw[] followUpEvents;
            public string[] tags;
        }

        [Serializable]
        private class OptionRaw
        {
            public string id;
            public string text;
            public ConditionRaw[] conditions;
            public EffectRaw[] effects;
        }

        [Serializable]
        private class ConditionRaw
        {
            public string type;
            public string resource;
            public string targetId;
            public int amount;
            public int minDay;
            public int maxDay;
            public string stringValue;
        }

        [Serializable]
        private class EffectRaw
        {
            public string type;
            public string resourceType;
            public string targetId;
            public int intValue;
            public float floatValue;
            public string stringValue;
            public string[] stringArrayValue;
        }

        [Serializable]
        private class FollowUpRaw
        {
            public string id;
            public int delayDays;
        }

        [Serializable]
        private class LocationFile
        {
            public LocationRaw location;
        }

        [Serializable]
        private class LocationRaw
        {
            public string id;
            public string name;
            public int tier;
            public int unlockDay;
            public int distance;
            public int danger;
            public YieldRaw[] baseYield;
            public string[] eventPool;
            public string[] prerequisites;
            public string[] tags;
        }

        [Serializable]
        private class YieldRaw
        {
            public string resource;
            public int min;
            public int max;
        }

        public static ContentDatabase LoadAll()
        {
            var database = new ContentDatabase();

            foreach (var asset in Resources.LoadAll<TextAsset>(EventsPath))
            {
                var errors = new List<string>();
                if (TryParseEvent(asset.text, out var definition, errors))
                {
                    if (!database.AddEvent(definition))
                    {
                        database.AddDiagnostic($"{asset.name}: duplicate event id \"{definition.id}\"");
                    }
                }
                else
                {
                    database.AddDiagnostic($"{asset.name}: {string.Join("; ", errors)}");
                }
            }

            foreach (var asset in Resources.LoadAll<TextAsset>(LocationsPath))
            {
                var errors = new List<string>();
                if (TryParseLocation(asset.text, out var definition, errors))
                {
                    if (!database.AddLocation(definition))
                    {
                        database.AddDiagnostic($"{asset.name}: duplicate location id \"{definition.id}\"");
                    }
                }
                else
                {
                    database.AddDiagnostic($"{asset.name}: {string.Join("; ", errors)}");
                }
            }

            database.Validate();
            return database;
        }

        public static bool TryParseEvent(string json, out EventDefinition definition, List<string> errors)
        {
            definition = null;

            if (json == null || !json.Contains("\"event\""))
            {
                errors.Add("missing \"event\" wrapper object");
                return false;
            }

            EventFile file;
            try
            {
                file = JsonUtility.FromJson<EventFile>(json);
            }
            catch (Exception e)
            {
                errors.Add($"json parse failed: {e.Message}");
                return false;
            }

            if (file == null || file.@event == null)
            {
                errors.Add("missing \"event\" wrapper object");
                return false;
            }

            var raw = file.@event;
            int errorCountBefore = errors.Count;

            CheckId(raw.id, "event id", errors);
            if (string.IsNullOrEmpty(raw.name)) errors.Add("name is required");

            bool typeOk = TryParseEnum(raw.type, out EventType type);
            if (!typeOk) errors.Add($"unknown event type \"{raw.type}\"");

            bool phaseOk = TryParseEnum(raw.phase, out TimeSlot phase);
            if (!phaseOk)
            {
                errors.Add($"unknown phase \"{raw.phase}\"");
            }
            else if (phase != TimeSlot.Evening && phase != TimeSlot.Action)
            {
                errors.Add($"phase \"{raw.phase}\" is not implemented (only Evening and Action trigger events)");
            }

            if (raw.weight <= 0f) errors.Add("weight must be > 0");
            if (raw.cooldownDays < 0) errors.Add("cooldownDays must be >= 0");
            if (raw.maxDay > 0 && raw.minDay > raw.maxDay) errors.Add("minDay must be <= maxDay");

            var conditions = ParseConditions(raw.conditions, errors, "condition");
            var options = ParseOptions(raw.options, errors);
            var followUps = ParseFollowUps(raw.followUpEvents, errors);

            if (errors.Count > errorCountBefore) return false;

            definition = new EventDefinition
            {
                id = raw.id,
                name = raw.name,
                type = type,
                phase = phase,
                weight = raw.weight,
                cooldownDays = raw.cooldownDays,
                minDay = raw.minDay,
                maxDay = raw.maxDay,
                conditions = conditions,
                options = options,
                followUpEvents = followUps,
                tags = raw.tags ?? new string[0]
            };
            return true;
        }

        public static bool TryParseLocation(string json, out LocationDefinition definition, List<string> errors)
        {
            definition = null;

            if (json == null || !json.Contains("\"location\""))
            {
                errors.Add("missing \"location\" wrapper object");
                return false;
            }

            LocationFile file;
            try
            {
                file = JsonUtility.FromJson<LocationFile>(json);
            }
            catch (Exception e)
            {
                errors.Add($"json parse failed: {e.Message}");
                return false;
            }

            if (file == null || file.location == null)
            {
                errors.Add("missing \"location\" wrapper object");
                return false;
            }

            var raw = file.location;
            int errorCountBefore = errors.Count;

            CheckId(raw.id, "location id", errors);
            if (string.IsNullOrEmpty(raw.name)) errors.Add("name is required");
            if (raw.tier < 1) errors.Add("tier must be >= 1");
            if (raw.unlockDay < 1) errors.Add("unlockDay must be >= 1");
            if (raw.distance < 1) errors.Add("distance must be >= 1");
            if (raw.danger < 1 || raw.danger > 5) errors.Add("danger must be between 1 and 5");

            var yields = new List<ResourceYield>();
            if (raw.baseYield != null)
            {
                for (int i = 0; i < raw.baseYield.Length; i++)
                {
                    var entry = raw.baseYield[i];
                    if (entry == null)
                    {
                        errors.Add($"baseYield #{i}: empty entry");
                        continue;
                    }
                    if (!TryParseEnum(entry.resource, out ResourceType resource))
                    {
                        errors.Add($"baseYield #{i}: unknown resource \"{entry.resource}\"");
                        continue;
                    }
                    if (entry.min < 0) errors.Add($"baseYield #{i}: min must be >= 0");
                    if (entry.max < entry.min) errors.Add($"baseYield #{i}: max must be >= min");
                    yields.Add(new ResourceYield { resource = resource, min = entry.min, max = entry.max });
                }
            }

            if (errors.Count > errorCountBefore) return false;

            definition = new LocationDefinition
            {
                id = raw.id,
                name = raw.name,
                tier = raw.tier,
                unlockDay = raw.unlockDay,
                distance = raw.distance,
                danger = raw.danger,
                baseYield = yields.ToArray(),
                eventPool = raw.eventPool ?? new string[0],
                prerequisites = raw.prerequisites ?? new string[0],
                tags = raw.tags ?? new string[0]
            };
            return true;
        }

        private static EventCondition[] ParseConditions(ConditionRaw[] raws, List<string> errors, string context)
        {
            if (raws == null || raws.Length == 0) return new EventCondition[0];

            var parsed = new EventCondition[raws.Length];
            for (int i = 0; i < raws.Length; i++)
            {
                var raw = raws[i];
                string label = $"{context} #{i}";

                if (raw == null)
                {
                    errors.Add($"{label}: empty entry");
                    continue;
                }
                if (!TryParseEnum(raw.type, out ConditionType type))
                {
                    errors.Add($"{label}: unknown condition type \"{raw.type}\"");
                    continue;
                }

                var entry = new EventCondition
                {
                    type = type,
                    targetId = raw.targetId,
                    amount = raw.amount,
                    minDay = raw.minDay,
                    maxDay = raw.maxDay,
                    stringValue = raw.stringValue
                };

                bool needsResource = type == ConditionType.ResourceAtLeast || type == ConditionType.ResourceBelow;
                if (needsResource)
                {
                    if (!TryParseEnum(raw.resource, out ResourceType resource))
                    {
                        errors.Add($"{label}: unknown resource \"{raw.resource}\"");
                        continue;
                    }
                    entry.resource = resource;
                }

                bool needsTarget = type == ConditionType.FlagIs || type == ConditionType.FlagNot
                    || type == ConditionType.HasTrait || type == ConditionType.HasProfession
                    || type == ConditionType.LocationDiscovered || type == ConditionType.BuildingExists;
                if (needsTarget && string.IsNullOrEmpty(raw.targetId))
                {
                    errors.Add($"{label}: targetId is required for {type}");
                    continue;
                }

                if ((type == ConditionType.FlagIs || type == ConditionType.FlagNot) && string.IsNullOrEmpty(raw.stringValue))
                {
                    errors.Add($"{label}: stringValue is required for {type}");
                    continue;
                }

                if (type == ConditionType.DayBetween && raw.minDay > raw.maxDay)
                {
                    errors.Add($"{label}: minDay must be <= maxDay");
                    continue;
                }

                parsed[i] = entry;
            }
            return parsed;
        }

        private static EventOption[] ParseOptions(OptionRaw[] raws, List<string> errors)
        {
            if (raws == null || raws.Length == 0)
            {
                errors.Add("options must contain at least one entry");
                return new EventOption[0];
            }

            var parsed = new EventOption[raws.Length];
            for (int i = 0; i < raws.Length; i++)
            {
                var raw = raws[i];
                string label = $"option #{i}";

                if (raw == null)
                {
                    errors.Add($"{label}: empty entry");
                    continue;
                }

                CheckId(raw.id, $"{label} id", errors);
                if (string.IsNullOrEmpty(raw.text)) errors.Add($"{label}: text is required");

                var conditions = ParseConditions(raw.conditions, errors, label);
                var effects = ParseEffects(raw.effects, errors, label);

                if (raw.effects == null || raw.effects.Length == 0)
                {
                    errors.Add($"{label}: at least one effect is required");
                }

                parsed[i] = new EventOption
                {
                    id = raw.id,
                    text = raw.text,
                    conditions = conditions,
                    effects = effects
                };
            }
            return parsed;
        }

        private static Effect[] ParseEffects(EffectRaw[] raws, List<string> errors, string context)
        {
            if (raws == null || raws.Length == 0) return new Effect[0];

            var parsed = new Effect[raws.Length];
            for (int i = 0; i < raws.Length; i++)
            {
                var raw = raws[i];
                string label = $"{context} effect #{i}";

                if (raw == null)
                {
                    errors.Add($"{label}: empty entry");
                    continue;
                }
                if (!TryParseEnum(raw.type, out EffectType type))
                {
                    errors.Add($"{label}: unknown effect type \"{raw.type}\"");
                    continue;
                }

                var entry = new Effect
                {
                    type = type,
                    targetId = raw.targetId,
                    intValue = raw.intValue,
                    floatValue = raw.floatValue,
                    stringValue = raw.stringValue,
                    stringArrayValue = raw.stringArrayValue
                };

                bool needsResource = type == EffectType.AddResource || type == EffectType.RemoveResource;
                if (needsResource)
                {
                    if (!TryParseEnum(raw.resourceType, out ResourceType resource))
                    {
                        errors.Add($"{label}: unknown resource \"{raw.resourceType}\"");
                        continue;
                    }
                    entry.resourceType = resource;
                }

                parsed[i] = entry;
            }
            return parsed;
        }

        private static FollowUpEvent[] ParseFollowUps(FollowUpRaw[] raws, List<string> errors)
        {
            if (raws == null || raws.Length == 0) return new FollowUpEvent[0];

            var parsed = new FollowUpEvent[raws.Length];
            for (int i = 0; i < raws.Length; i++)
            {
                var raw = raws[i];
                string label = $"followUp #{i}";

                if (raw == null)
                {
                    errors.Add($"{label}: empty entry");
                    continue;
                }

                CheckId(raw.id, $"{label} id", errors);
                if (raw.delayDays < 0) errors.Add($"{label}: delayDays must be >= 0");

                parsed[i] = new FollowUpEvent { id = raw.id, delayDays = raw.delayDays };
            }
            return parsed;
        }

        private static void CheckId(string id, string label, List<string> errors)
        {
            if (string.IsNullOrEmpty(id))
            {
                errors.Add($"{label} is required");
                return;
            }
            if (!ValidId.IsMatch(id))
            {
                errors.Add($"{label} \"{id}\" must be lower_snake_case");
            }
        }

        private static bool TryParseEnum<T>(string raw, out T value) where T : struct
        {
            if (!string.IsNullOrEmpty(raw) && Enum.TryParse(raw, false, out T parsed) && Enum.IsDefined(typeof(T), parsed))
            {
                value = parsed;
                return true;
            }
            value = default;
            return false;
        }
    }
}
