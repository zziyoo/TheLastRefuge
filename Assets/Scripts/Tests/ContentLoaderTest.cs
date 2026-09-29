using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using LastRefuge.Core;
using LastRefuge.Data;

namespace LastRefuge.Tests
{
    public class ContentLoaderTest
    {
        private const string ValidEventJson = @"{
    ""event"": {
        ""id"": ""event_food_storage_01"",
        ""name"": ""粮仓里的老鼠"",
        ""type"": ""Normal"",
        ""phase"": ""Evening"",
        ""weight"": 10,
        ""cooldownDays"": 3,
        ""minDay"": 1,
        ""maxDay"": 15,
        ""conditions"": [ { ""type"": ""ResourceAtLeast"", ""resource"": ""Food"", ""amount"": 20 } ],
        ""options"": [
            {
                ""id"": ""trap"",
                ""text"": ""捕杀"",
                ""conditions"": [],
                ""effects"": [ { ""type"": ""RemoveResource"", ""resourceType"": ""Food"", ""intValue"": 3 } ]
            },
            {
                ""id"": ""ignore"",
                ""text"": ""不管"",
                ""conditions"": [ { ""type"": ""FlagNot"", ""targetId"": ""rat_war"", ""stringValue"": ""true"" } ],
                ""effects"": [ { ""type"": ""SetFlag"", ""targetId"": ""rat_war"", ""stringValue"": ""true"" } ]
            }
        ],
        ""followUpEvents"": [ { ""id"": ""event_food_storage_02"", ""delayDays"": 4 } ],
        ""tags"": [ ""resource"" ]
    }
}";

        private const string ValidLocationJson = @"{
    ""location"": {
        ""id"": ""location_forest"",
        ""name"": ""森林"",
        ""tier"": 1,
        ""unlockDay"": 1,
        ""distance"": 2,
        ""danger"": 1,
        ""baseYield"": [ { ""resource"": ""Wood"", ""min"": 8, ""max"": 15 } ],
        ""eventPool"": [ ""event_food_storage_01"" ],
        ""prerequisites"": [],
        ""tags"": [ ""scavenge"" ]
    }
}";

        [Test]
        public void TryParseEvent_ValidJson_ReturnsDefinition()
        {
            var errors = new List<string>();

            bool ok = ContentLoader.TryParseEvent(ValidEventJson, out var definition, errors);

            Assert.IsTrue(ok, string.Join("; ", errors));
            Assert.AreEqual("event_food_storage_01", definition.id);
            Assert.AreEqual(EventType.Normal, definition.type);
            Assert.AreEqual(TimeSlot.Evening, definition.phase);
            Assert.AreEqual(10f, definition.weight);
            Assert.AreEqual(3, definition.cooldownDays);
            Assert.AreEqual(1, definition.minDay);
            Assert.AreEqual(15, definition.maxDay);
            Assert.AreEqual(1, definition.conditions.Length);
            Assert.AreEqual(ConditionType.ResourceAtLeast, definition.conditions[0].type);
            Assert.AreEqual(ResourceType.Food, definition.conditions[0].resource);
            Assert.AreEqual(20, definition.conditions[0].amount);
            Assert.AreEqual(2, definition.options.Length);
            Assert.AreEqual("trap", definition.options[0].id);
            Assert.AreEqual(EffectType.RemoveResource, definition.options[0].effects[0].type);
            Assert.AreEqual(ResourceType.Food, definition.options[0].effects[0].resourceType);
            Assert.AreEqual(3, definition.options[0].effects[0].intValue);
            Assert.AreEqual(ConditionType.FlagNot, definition.options[1].conditions[0].type);
            Assert.AreEqual(1, definition.followUpEvents.Length);
            Assert.AreEqual("event_food_storage_02", definition.followUpEvents[0].id);
            Assert.AreEqual(4, definition.followUpEvents[0].delayDays);
        }

        [Test]
        public void TryParseEvent_MissingWrapper_ReportsError()
        {
            var errors = new List<string>();

            bool ok = ContentLoader.TryParseEvent("{\"other\": {}}", out var definition, errors);

            Assert.IsFalse(ok);
            Assert.IsNull(definition);
            Assert.IsTrue(errors.Any(e => e.Contains("wrapper")), "errors: " + string.Join(" || ", errors));
        }

        [Test]
        public void TryParseEvent_UnknownEventType_ReportsError()
        {
            string json = ValidEventJson.Replace("\"type\": \"Normal\"", "\"type\": \"Bogus\"");
            var errors = new List<string>();

            Assert.IsFalse(ContentLoader.TryParseEvent(json, out _, errors));
            Assert.IsTrue(errors.Any(e => e.Contains("Bogus")));
        }

        [Test]
        public void TryParseEvent_ZeroWeight_ReportsError()
        {
            string json = ValidEventJson.Replace("\"weight\": 10", "\"weight\": 0");
            var errors = new List<string>();

            Assert.IsFalse(ContentLoader.TryParseEvent(json, out _, errors));
            Assert.IsTrue(errors.Any(e => e.Contains("weight")));
        }

        [Test]
        public void TryParseEvent_UnimplementedPhase_ReportsError()
        {
            string json = ValidEventJson.Replace("\"phase\": \"Evening\"", "\"phase\": \"Night\"");
            var errors = new List<string>();

            Assert.IsFalse(ContentLoader.TryParseEvent(json, out _, errors));
            Assert.IsTrue(errors.Any(e => e.Contains("not implemented")));
        }

        [Test]
        public void TryParseEvent_UnknownEffectType_ReportsError()
        {
            string json = ValidEventJson.Replace("\"type\": \"RemoveResource\"", "\"type\": \"Explode\"");
            var errors = new List<string>();

            Assert.IsFalse(ContentLoader.TryParseEvent(json, out _, errors));
            Assert.IsTrue(errors.Any(e => e.Contains("Explode")));
        }

        [Test]
        public void TryParseEvent_NonSnakeCaseId_ReportsError()
        {
            string json = ValidEventJson.Replace("\"event_food_storage_01\"", "\"Event Food One\"");
            var errors = new List<string>();

            Assert.IsFalse(ContentLoader.TryParseEvent(json, out _, errors));
            Assert.IsTrue(errors.Any(e => e.Contains("lower_snake_case")));
        }

        [Test]
        public void TryParseLocation_ValidJson_ReturnsDefinition()
        {
            var errors = new List<string>();

            bool ok = ContentLoader.TryParseLocation(ValidLocationJson, out var definition, errors);

            Assert.IsTrue(ok, string.Join("; ", errors));
            Assert.AreEqual("location_forest", definition.id);
            Assert.AreEqual(1, definition.tier);
            Assert.AreEqual(2, definition.distance);
            Assert.AreEqual(1, definition.danger);
            Assert.AreEqual(ResourceType.Wood, definition.baseYield[0].resource);
            Assert.AreEqual(8, definition.baseYield[0].min);
            Assert.AreEqual(15, definition.baseYield[0].max);
            Assert.AreEqual(1, definition.eventPool.Length);
        }

        [Test]
        public void TryParseLocation_DangerOutOfRange_ReportsError()
        {
            string json = ValidLocationJson.Replace("\"danger\": 1", "\"danger\": 6");
            var errors = new List<string>();

            Assert.IsFalse(ContentLoader.TryParseLocation(json, out _, errors));
            Assert.IsTrue(errors.Any(e => e.Contains("danger")));
        }

        [Test]
        public void ContentDatabase_AddEvent_RejectsDuplicates()
        {
            var database = new ContentDatabase();
            var first = new EventDefinition { id = "event_dup" };
            var second = new EventDefinition { id = "event_dup" };

            Assert.IsTrue(database.AddEvent(first));
            Assert.IsFalse(database.AddEvent(second));
            Assert.AreEqual(1, database.Events.Count);
        }

        [Test]
        public void ContentDatabase_Validate_ReportsDanglingReferences()
        {
            var database = new ContentDatabase();
            database.AddEvent(new EventDefinition
            {
                id = "event_a",
                followUpEvents = new[] { new FollowUpEvent { id = "event_missing", delayDays = 2 } },
                options = new[]
                {
                    new EventOption
                    {
                        id = "opt",
                        effects = new[] { new Effect { type = EffectType.StartEvent, targetId = "event_also_missing" } }
                    }
                }
            });
            database.AddLocation(new LocationDefinition
            {
                id = "loc_x",
                eventPool = new[] { "event_missing_from_pool" }
            });

            List<string> errors = database.Validate();

            Assert.IsTrue(errors.Exists(e => e.Contains("event_missing")), "dangling followUp must be reported");
            Assert.IsTrue(errors.Exists(e => e.Contains("event_also_missing")), "dangling StartEvent must be reported");
            Assert.IsTrue(errors.Exists(e => e.Contains("event_missing_from_pool")), "dangling eventPool entry must be reported");
        }

        [Test]
        public void ContentDatabase_Validate_ReportsFollowUpCycle()
        {
            var database = new ContentDatabase();
            database.AddEvent(new EventDefinition
            {
                id = "event_cycle_a",
                followUpEvents = new[] { new FollowUpEvent { id = "event_cycle_b", delayDays = 1 } }
            });
            database.AddEvent(new EventDefinition
            {
                id = "event_cycle_b",
                followUpEvents = new[] { new FollowUpEvent { id = "event_cycle_a", delayDays = 1 } }
            });

            List<string> errors = database.Validate();

            Assert.IsTrue(errors.Exists(e => e.Contains("cycle")), "a followUp loop must be reported");
        }

        [Test]
        public void Content_SliceAllFilesParseAndValidate()
        {
            var database = ContentLoader.LoadAll();

            Assert.AreEqual(0, database.Diagnostics.Count, string.Join("\n", database.Diagnostics));
            Assert.AreEqual(9, database.Events.Count);
            Assert.AreEqual(3, database.Locations.Count);

            var diagnostics = database.Validate();
            Assert.AreEqual(0, diagnostics.Count, string.Join("\n", diagnostics));

            Assert.IsTrue(database.TryGetEvent("event_food_storage_01", out var chain));
            Assert.AreEqual(1, chain.followUpEvents.Length);
            Assert.AreEqual("event_food_storage_02", chain.followUpEvents[0].id);
            Assert.AreEqual(21, chain.conditions[0].amount);

            Assert.IsTrue(database.TryGetEvent("event_crisis_01", out var crisis));
            Assert.AreEqual(3, crisis.minDay);
            Assert.GreaterOrEqual(crisis.options.Length, 3);

            Assert.IsTrue(database.TryGetLocation("location_mine", out var mine));
            Assert.AreEqual(4, mine.distance);
            Assert.AreEqual(2, mine.danger);
            Assert.AreEqual("location_forest", mine.prerequisites[0]);
        }
    }
}
