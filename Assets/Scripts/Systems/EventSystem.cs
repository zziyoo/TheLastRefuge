using System;
using System.Collections.Generic;
using LastRefuge.Core;
using LastRefuge.Data;

namespace LastRefuge.Systems
{
    public class EventSystem : IEventSystem
    {
        private const string RollDayKey = "event_roll_day";
        private const string CooldownPrefix = "event_cd_";
        private const string DuePrefix = "event_due_";
        private const string DailyRollContext = "DailyEventRoll";
        private const string LocationRollContext = "LocationEventRoll";
        private const string EventTargetContext = "EventTarget";

        private static readonly EffectType[] CharacterScopedEffects =
        {
            EffectType.DamageCharacter, EffectType.HealCharacter,
            EffectType.AddStress, EffectType.RemoveStress,
            EffectType.AddFatigue, EffectType.RemoveFatigue,
            EffectType.AddHunger, EffectType.RemoveHunger,
            EffectType.SetHealth, EffectType.AddTrait, EffectType.RemoveTrait,
            EffectType.KillCharacter, EffectType.ChangeWork
        };

        private static readonly EffectType[] BuildingScopedEffects =
        {
            EffectType.DamageBuilding, EffectType.RepairBuilding, EffectType.SetBuildingEnabled
        };

        private GameState gameState;
        private ITimeSystem timeSystem;
        private IRandomSystem randomSystem;
        private IContentDatabase contentDatabase;
        private EffectResolver effectResolver;
        private IResourceSystem resourceSystem;
        private ICharacterSystem characterSystem;

        public EventDefinition PendingEvent { get; private set; }

        public void Initialize(GameState state, ITimeSystem timeSystem, IRandomSystem randomSystem,
                               IContentDatabase contentDatabase, EffectResolver effectResolver,
                               IResourceSystem resourceSystem, ICharacterSystem characterSystem)
        {
            this.gameState = state;
            this.timeSystem = timeSystem;
            this.randomSystem = randomSystem;
            this.contentDatabase = contentDatabase;
            this.effectResolver = effectResolver;
            this.resourceSystem = resourceSystem;
            this.characterSystem = characterSystem;

            if (PendingEvent != null && gameState.gameplayState != GameplayState.Event)
            {
                PendingEvent = null;
            }
        }

        public bool TryRollDailyEvent()
        {
            if (gameState.gameplayState == GameplayState.Event ||
                gameState.gameplayState == GameplayState.Resolution)
            {
                return false;
            }

            int day = gameState.currentDay;
            int lastRollDay;
            if (gameState.gameFlags.intFlags.TryGetValue(RollDayKey, out lastRollDay) && lastRollDay == day)
            {
                return false;
            }
            gameState.gameFlags.intFlags[RollDayKey] = day;

            foreach (var dueId in CollectDueEventIds(day))
            {
                if (TryStartEvent(dueId)) return true;
                gameState.gameFlags.intFlags.Remove(DuePrefix + dueId);
            }

            var candidates = new List<EventDefinition>();
            var weights = new List<float>();
            foreach (var evt in contentDatabase.Events)
            {
                if (!IsDailyRollable(evt, day)) continue;
                candidates.Add(evt);
                weights.Add(evt.weight);
            }
            if (candidates.Count == 0) return false;

            int pickedIndex = PickWeightedIndex(candidates, weights, DailyRollContext);
            return TryStartEvent(candidates[pickedIndex].id);
        }

        public bool TryStartLocationEvent(string locationId)
        {
            if (gameState.gameplayState == GameplayState.Event ||
                gameState.gameplayState == GameplayState.Resolution)
            {
                return false;
            }

            int day = gameState.currentDay;
            var candidates = new List<EventDefinition>();
            var weights = new List<float>();
            foreach (var evt in contentDatabase.GetEventPool(locationId))
            {
                if (!IsEligible(evt, day)) continue;
                candidates.Add(evt);
                weights.Add(evt.weight);
            }
            if (candidates.Count == 0) return false;

            int pickedIndex = PickWeightedIndex(candidates, weights, LocationRollContext);
            return TryStartEvent(candidates[pickedIndex].id);
        }

        private int PickWeightedIndex(List<EventDefinition> candidates, List<float> weights, string context)
        {
            float total = 0f;
            foreach (var w in weights) total += w;

            float roll = randomSystem.NextFloat(context) * total;
            float accumulated = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                accumulated += weights[i];
                if (roll <= accumulated) return i;
            }
            return candidates.Count - 1;
        }

        public bool TryStartEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return false;
            if (gameState.gameplayState == GameplayState.Event ||
                gameState.gameplayState == GameplayState.Resolution)
            {
                return false;
            }

            EventDefinition definition;
            if (!contentDatabase.TryGetEvent(eventId, out definition))
            {
                UnityEngine.Debug.LogWarning($"EventSystem: event \"{eventId}\" does not exist");
                return false;
            }

            gameState.gameFlags.intFlags.Remove(DuePrefix + eventId);

            PendingEvent = definition;
            timeSystem.SetGameplayState(GameplayState.Event);
            EventBus.Publish(new EventStartedEvent { eventId = eventId });
            return true;
        }

        public void ScheduleNow(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            gameState.gameFlags.intFlags[DuePrefix + eventId] = gameState.currentDay;
        }

        public bool ResolveChoice(string optionId)
        {
            if (PendingEvent == null) return false;
            if (gameState.gameplayState != GameplayState.Event) return false;

            var option = FindOption(PendingEvent, optionId);
            if (option == null) return false;
            if (!EvaluateConditions(option.conditions)) return false;

            var resolvedEvent = PendingEvent;
            timeSystem.SetGameplayState(GameplayState.Resolution);

            var effects = MaterializeTargets(option.effects ?? new Effect[0]);
            effectResolver.ResolveAll(effects);

            var effectLog = new string[effects.Length];
            for (int i = 0; i < effects.Length; i++)
            {
                effectLog[i] = DescribeEffectLog(effects[i]);
            }
            AppendHistory(new EventHistoryEntry
            {
                eventId = resolvedEvent.id,
                day = gameState.currentDay,
                timeSlot = timeSystem.CurrentTimeSlot,
                choiceId = option.id,
                effects = effectLog
            });

            if (resolvedEvent.cooldownDays > 0)
            {
                gameState.gameFlags.intFlags[CooldownPrefix + resolvedEvent.id] =
                    gameState.currentDay + resolvedEvent.cooldownDays;
            }
            else
            {
                gameState.gameFlags.intFlags.Remove(CooldownPrefix + resolvedEvent.id);
            }

            if (resolvedEvent.followUpEvents != null)
            {
                foreach (var followUp in resolvedEvent.followUpEvents)
                {
                    if (string.IsNullOrEmpty(followUp.id)) continue;
                    gameState.gameFlags.intFlags[DuePrefix + followUp.id] =
                        gameState.currentDay + Math.Max(0, followUp.delayDays);
                }
            }

            PendingEvent = null;
            RestoreSlotState();
            StartDueChain();
            EventBus.Publish(new EventFinishedEvent { eventId = resolvedEvent.id, choiceId = option.id });
            return true;
        }

        public bool IsOptionAvailable(string optionId)
        {
            if (PendingEvent == null) return false;
            var option = FindOption(PendingEvent, optionId);
            return option != null && EvaluateConditions(option.conditions);
        }

        public bool CanResolveChoice(string optionId, out string reason)
        {
            if (PendingEvent == null)
            {
                reason = "没有待处理的事件";
                return false;
            }
            if (gameState.gameplayState != GameplayState.Event)
            {
                reason = "事件尚未开始";
                return false;
            }
            var option = FindOption(PendingEvent, optionId);
            if (option == null)
            {
                reason = "未知选项";
                return false;
            }
            if (option.conditions != null)
            {
                foreach (var condition in option.conditions)
                {
                    if (condition == null) continue;
                    if (!Evaluate(condition))
                    {
                        reason = DescribeCondition(condition);
                        return false;
                    }
                }
            }
            reason = null;
            return true;
        }

        private static string DescribeCondition(EventCondition condition)
        {
            switch (condition.type)
            {
                case ConditionType.ResourceAtLeast:
                    return $"需要{condition.resource.GetDisplayName()} ≥ {condition.amount}";
                case ConditionType.ResourceBelow:
                    return $"{condition.resource.GetDisplayName()}需低于 {condition.amount}";
                case ConditionType.FlagIs:
                case ConditionType.FlagNot:
                    return "前置条件未满足";
                case ConditionType.HasTrait:
                    return $"需要特质 {condition.targetId}";
                case ConditionType.HasProfession:
                    return $"需要{condition.targetId}职业的角色";
                case ConditionType.DayBetween:
                    return $"需要第 {condition.minDay} 天起";
                case ConditionType.LocationDiscovered:
                    return $"需要先发现 {condition.targetId}";
                case ConditionType.BuildingExists:
                    return $"需要建有 {condition.targetId}";
                default:
                    return "前置条件未满足";
            }
        }

        public bool EvaluateConditions(EventCondition[] conditions)
        {
            if (conditions == null) return true;
            foreach (var condition in conditions)
            {
                if (condition != null && !Evaluate(condition)) return false;
            }
            return true;
        }

        public void ClearPending()
        {
            PendingEvent = null;
        }

        private bool Evaluate(EventCondition condition)
        {
            switch (condition.type)
            {
                case ConditionType.ResourceAtLeast:
                    return resourceSystem.GetAmount(condition.resource) >= condition.amount;

                case ConditionType.ResourceBelow:
                    return resourceSystem.GetAmount(condition.resource) < condition.amount;

                case ConditionType.FlagIs:
                    return GetFlagValue(condition.targetId) == condition.stringValue;

                case ConditionType.FlagNot:
                    return GetFlagValue(condition.targetId) != condition.stringValue;

                case ConditionType.HasTrait:
                    foreach (var character in characterSystem.GetAliveCharacters())
                    {
                        if (character.traits != null &&
                            Array.IndexOf(character.traits, condition.targetId) >= 0)
                        {
                            return true;
                        }
                    }
                    return false;

                case ConditionType.HasProfession:
                {
                    Profession profession;
                    if (!Enum.TryParse(condition.targetId, true, out profession)) return false;
                    foreach (var character in characterSystem.GetAliveCharacters())
                    {
                        if (character.profession == profession) return true;
                    }
                    return false;
                }

                case ConditionType.DayBetween:
                    return gameState.currentDay >= condition.minDay &&
                           (condition.maxDay <= 0 || gameState.currentDay <= condition.maxDay);

                case ConditionType.LocationDiscovered:
                {
                    var discovered = gameState.worldState != null
                        ? gameState.worldState.discoveredLocations
                        : null;
                    return discovered != null && Array.IndexOf(discovered, condition.targetId) >= 0;
                }

                case ConditionType.BuildingExists:
                    if (gameState.buildings == null) return false;
                    foreach (var building in gameState.buildings)
                    {
                        if (building != null && building.definitionId == condition.targetId) return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        private bool IsDailyRollable(EventDefinition definition, int day)
        {
            if (definition != null && definition.type == EventType.Exploration) return false;
            return IsEligible(definition, day);
        }

        private bool IsEligible(EventDefinition definition, int day)
        {
            if (definition == null || string.IsNullOrEmpty(definition.id)) return false;
            if (definition.weight <= 0f) return false;
            if (definition.minDay > 0 && day < definition.minDay) return false;
            if (definition.maxDay > 0 && day > definition.maxDay) return false;

            int cooldownUntil;
            if (gameState.gameFlags.intFlags.TryGetValue(CooldownPrefix + definition.id, out cooldownUntil) &&
                cooldownUntil > day)
            {
                return false;
            }

            return EvaluateConditions(definition.conditions);
        }

        private List<string> CollectDueEventIds(int day)
        {
            var due = new List<KeyValuePair<string, int>>();
            foreach (var kvp in gameState.gameFlags.intFlags)
            {
                if (kvp.Key.StartsWith(DuePrefix, StringComparison.Ordinal) && kvp.Value <= day)
                {
                    due.Add(kvp);
                }
            }
            due.Sort((a, b) => a.Value != b.Value
                ? a.Value.CompareTo(b.Value)
                : string.CompareOrdinal(a.Key, b.Key));

            var ids = new List<string>(due.Count);
            foreach (var kvp in due)
            {
                ids.Add(kvp.Key.Substring(DuePrefix.Length));
            }
            return ids;
        }

        private void StartDueChain()
        {
            foreach (var dueId in CollectDueEventIds(gameState.currentDay))
            {
                if (TryStartEvent(dueId)) return;
                gameState.gameFlags.intFlags.Remove(DuePrefix + dueId);
            }
        }

        private void RestoreSlotState()
        {
            switch (timeSystem.CurrentTimeSlot)
            {
                case TimeSlot.Morning:
                    timeSystem.SetGameplayState(GameplayState.Morning);
                    break;
                case TimeSlot.Planning:
                    timeSystem.SetGameplayState(GameplayState.Planning);
                    break;
                case TimeSlot.Action:
                    timeSystem.SetGameplayState(GameplayState.Action);
                    break;
                case TimeSlot.Evening:
                    timeSystem.SetGameplayState(GameplayState.Evening);
                    break;
                case TimeSlot.Night:
                    timeSystem.SetGameplayState(GameplayState.Night);
                    break;
                case TimeSlot.DayEnd:
                    timeSystem.SetGameplayState(GameplayState.DayEnd);
                    break;
            }
        }

        private static EventOption FindOption(EventDefinition definition, string optionId)
        {
            if (definition.options == null || string.IsNullOrEmpty(optionId)) return null;
            foreach (var option in definition.options)
            {
                if (option != null && option.id == optionId) return option;
            }
            return null;
        }

        private static string DescribeEffectLog(Effect effect)
        {
            if (effect == null) return "null";
            switch (effect.type)
            {
                case EffectType.AddResource:
                    return $"AddResource {effect.resourceType} +{effect.intValue}";
                case EffectType.RemoveResource:
                    return $"RemoveResource {effect.resourceType} -{effect.intValue}";
                case EffectType.DamageCharacter:
                case EffectType.HealCharacter:
                case EffectType.SetHealth:
                    return $"{effect.type} {effect.targetId} {effect.intValue}";
                case EffectType.AddStress:
                case EffectType.RemoveStress:
                case EffectType.AddFatigue:
                case EffectType.RemoveFatigue:
                case EffectType.AddHunger:
                case EffectType.RemoveHunger:
                    return $"{effect.type} {effect.targetId} {effect.floatValue}";
                case EffectType.SetFlag:
                case EffectType.ClearFlag:
                    return $"{effect.type} {effect.targetId}={effect.stringValue}";
                case EffectType.DamageBuilding:
                case EffectType.RepairBuilding:
                    return $"{effect.type} {effect.targetId} {effect.intValue}";
                default:
                    return effect.targetId != null
                        ? $"{effect.type} {effect.targetId}"
                        : effect.type.ToString();
            }
        }

        private string GetFlagValue(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var flags = gameState.gameFlags;

            string stringValue;
            if (flags.stringFlags.TryGetValue(key, out stringValue)) return stringValue;

            bool boolValue;
            if (flags.boolFlags.TryGetValue(key, out boolValue)) return boolValue ? "true" : "false";

            return null;
        }

        private Effect[] MaterializeTargets(Effect[] source)
        {
            var materialized = new Effect[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var effect = source[i];
                if (effect == null) continue;

                var clone = new Effect
                {
                    type = effect.type,
                    targetId = effect.targetId,
                    resourceType = effect.resourceType,
                    intValue = effect.intValue,
                    floatValue = effect.floatValue,
                    stringValue = effect.stringValue,
                    stringArrayValue = effect.stringArrayValue
                };

                if (clone.targetId == "@random" &&
                    Array.IndexOf(CharacterScopedEffects, clone.type) >= 0)
                {
                    var alive = characterSystem.GetAliveCharacters();
                    if (alive != null && alive.Length > 0)
                    {
                        clone.targetId = randomSystem.NextElement(EventTargetContext, alive).characterId;
                    }
                }
                else if (clone.targetId != null &&
                         clone.targetId.StartsWith("@building:", StringComparison.Ordinal) &&
                         Array.IndexOf(BuildingScopedEffects, clone.type) >= 0)
                {
                    string definitionId = clone.targetId.Substring("@building:".Length);
                    if (gameState.buildings != null)
                    {
                        foreach (var building in gameState.buildings)
                        {
                            if (building != null && building.definitionId == definitionId)
                            {
                                clone.targetId = building.buildingId;
                                break;
                            }
                        }
                    }
                }

                materialized[i] = clone;
            }
            return materialized;
        }

        private void AppendHistory(EventHistoryEntry entry)
        {
            var old = gameState.eventHistory ?? new EventHistoryEntry[0];
            var next = new EventHistoryEntry[old.Length + 1];
            Array.Copy(old, next, old.Length);
            next[old.Length] = entry;
            gameState.eventHistory = next;
        }
    }
}
