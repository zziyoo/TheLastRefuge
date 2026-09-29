using System;
using System.Collections.Generic;
using UnityEngine;
using LastRefuge.Core;
using LastRefuge.Data;

namespace LastRefuge.Systems
{
    public class ExplorationSystem : IExplorationSystem
    {
        private const float BaseHurtChancePerDanger = 0.2f;
        private const float AbilityHurtReduction = 0.001f;

        private GameState gameState;
        private IRandomSystem randomSystem;
        private IContentDatabase contentDatabase;
        private IEventSystem eventSystem;
        private IResourceSystem resourceSystem;
        private ICharacterSystem characterSystem;
        private Action advanceOneSlot;

        private string pendingLocationId;
        private string[] pendingTeam;
        private int pendingAdvanceSlots;

        public void Initialize(GameState state, IRandomSystem randomSystem, IContentDatabase contentDatabase,
                               IEventSystem eventSystem, IResourceSystem resourceSystem,
                               ICharacterSystem characterSystem)
        {
            this.gameState = state;
            this.randomSystem = randomSystem;
            this.contentDatabase = contentDatabase;
            this.eventSystem = eventSystem;
            this.resourceSystem = resourceSystem;
            this.characterSystem = characterSystem;

            EventBus.Unsubscribe<EventFinishedEvent>(OnEventFinished);
            EventBus.Subscribe<EventFinishedEvent>(OnEventFinished);
        }

        public void SetTimeAdvance(Action advanceOneSlot)
        {
            this.advanceOneSlot = advanceOneSlot;
        }

        public bool CanExplore(string locationId, string[] teamMemberIds, out string reason)
        {
            return Validate(locationId, teamMemberIds, out reason);
        }

        public bool ExploreLocation(string locationId, string[] teamMemberIds, out string rejectReason)
        {
            if (!Validate(locationId, teamMemberIds, out rejectReason)) return false;

            var location = contentDatabase.TryGetLocation(locationId, out var definition)
                ? definition
                : null;
            if (location == null)
            {
                rejectReason = "unknown location";
                return false;
            }

            int rations = (teamMemberIds.Length * location.distance) / 2;
            if (rations > 0 && !resourceSystem.TryRemove(ResourceType.Food, rations, "Exploration"))
            {
                rejectReason = $"not enough food (need {rations})";
                return false;
            }

            pendingLocationId = locationId;
            pendingTeam = (string[])teamMemberIds.Clone();

            foreach (var memberId in teamMemberIds)
            {
                var member = characterSystem.GetCharacter(memberId);
                if (member != null) member.locationId = locationId;
            }

            EventBus.Publish(new ExplorationStartedEvent
            {
                locationId = locationId,
                teamSize = teamMemberIds.Length
            });

            if (!eventSystem.TryStartLocationEvent(locationId))
            {
                FinishPendingExploration();
            }

            return true;
        }

        public void Reset()
        {
            if (pendingTeam != null && gameState.characters != null)
            {
                foreach (var memberId in pendingTeam)
                {
                    foreach (var character in gameState.characters)
                    {
                        if (character != null && character.characterId == memberId)
                        {
                            character.locationId = null;
                        }
                    }
                }
            }

            pendingLocationId = null;
            pendingTeam = null;
            pendingAdvanceSlots = 0;
        }

        private bool Validate(string locationId, string[] teamMemberIds, out string reason)
        {
            if (pendingLocationId != null)
            {
                reason = "an exploration is already in progress";
                return false;
            }
            if (gameState.gameplayState != GameplayState.Action)
            {
                reason = "exploration is only available during the action phase";
                return false;
            }
            if (eventSystem.PendingEvent != null)
            {
                reason = "an event is waiting for a choice";
                return false;
            }
            if (!contentDatabase.TryGetLocation(locationId, out var location))
            {
                reason = "unknown location";
                return false;
            }
            if (gameState.currentDay < location.unlockDay)
            {
                reason = $"location unlocks on day {location.unlockDay}";
                return false;
            }
            if (teamMemberIds == null || teamMemberIds.Length < 1 || teamMemberIds.Length > 4)
            {
                reason = "team must contain 1 to 4 survivors";
                return false;
            }

            foreach (var memberId in teamMemberIds)
            {
                var member = characterSystem.GetCharacter(memberId);
                if (member == null || !member.alive)
                {
                    reason = "team member is not available";
                    return false;
                }
                if (!string.IsNullOrEmpty(member.locationId))
                {
                    reason = "team member is already away";
                    return false;
                }
            }

            int rations = (teamMemberIds.Length * location.distance) / 2;
            if (rations > 0 && !resourceSystem.CanAfford(ResourceType.Food, rations))
            {
                reason = $"not enough food (need {rations})";
                return false;
            }

            reason = null;
            return true;
        }

        private void OnEventFinished(EventFinishedEvent finished)
        {
            if (pendingLocationId != null && eventSystem.PendingEvent == null)
            {
                FinishPendingExploration();
            }
            DrainAdvances();
        }

        private void FinishPendingExploration()
        {
            if (pendingLocationId == null) return;

            string locationId = pendingLocationId;
            var team = pendingTeam;
            pendingLocationId = null;
            pendingTeam = null;

            if (!contentDatabase.TryGetLocation(locationId, out var location))
            {
                ClearTeamLocationIds(team);
                return;
            }

            var summary = new List<string>();
            if (location.baseYield != null)
            {
                foreach (var yield in location.baseYield)
                {
                    if (yield == null) continue;
                    int amount = randomSystem.NextInt($"ExplorationYield_{locationId}", yield.min, yield.max + 1);
                    if (amount <= 0) continue;
                    if (resourceSystem.Add(yield.resource, amount, "Exploration"))
                    {
                        summary.Add($"{yield.resource}+{amount}");
                    }
                }
            }

            float averageAbility = AverageAbility(team);
            float hurtChance = Mathf.Clamp01(
                location.danger * BaseHurtChancePerDanger - averageAbility * AbilityHurtReduction);

            int survivors = 0;
            int casualties = 0;

            foreach (var memberId in team)
            {
                var member = characterSystem.GetCharacter(memberId);
                if (member == null) continue;
                if (!member.alive)
                {
                    casualties++;
                    continue;
                }

                if (randomSystem.NextBool($"ExplorationHurt_{locationId}", hurtChance))
                {
                    int damage = randomSystem.NextInt($"ExplorationDamage_{locationId}", 3, 3 + location.danger * 5);
                    member.health = Math.Max(0, member.health - damage);
                    if (member.health <= 0)
                    {
                        characterSystem.KillCharacter(memberId, "Exploration");
                        casualties++;
                        continue;
                    }
                }
                survivors++;
            }

            var discovered = gameState.worldState != null
                ? gameState.worldState.discoveredLocations
                : null;
            if (discovered == null || Array.IndexOf(discovered, locationId) < 0)
            {
                if (gameState.worldState == null) gameState.worldState = new WorldState();
                var next = new string[(discovered?.Length ?? 0) + 1];
                if (discovered != null) Array.Copy(discovered, next, discovered.Length);
                next[discovered?.Length ?? 0] = locationId;
                gameState.worldState.discoveredLocations = next;
            }

            ClearTeamLocationIds(team);

            EventBus.Publish(new ExplorationFinishedEvent
            {
                locationId = locationId,
                teamSize = team?.Length ?? 0,
                survivors = survivors,
                casualties = casualties,
                resourceSummary = string.Join(", ", summary)
            });

            pendingAdvanceSlots = location.distance;
            DrainAdvances();
        }

        private void ClearTeamLocationIds(string[] team)
        {
            if (team == null) return;
            foreach (var memberId in team)
            {
                var member = characterSystem.GetCharacter(memberId);
                if (member != null) member.locationId = null;
            }
        }

        private float AverageAbility(string[] team)
        {
            if (team == null || team.Length == 0) return 0f;

            float total = 0f;
            int counted = 0;
            foreach (var memberId in team)
            {
                var member = characterSystem.GetCharacter(memberId);
                if (member == null || member.stats == null) continue;
                total += (member.stats.exploration + member.stats.combat) / 2f;
                counted++;
            }
            return counted > 0 ? total / counted : 0f;
        }

        private void DrainAdvances()
        {
            while (pendingAdvanceSlots > 0)
            {
                if (gameState.gameplayState == GameplayState.Event ||
                    gameState.gameplayState == GameplayState.Resolution)
                {
                    return;
                }
                if (advanceOneSlot == null)
                {
                    pendingAdvanceSlots = 0;
                    return;
                }

                advanceOneSlot();
                pendingAdvanceSlots--;
            }
        }
    }
}
