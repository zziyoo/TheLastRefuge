using System;
using System.Collections.Generic;
using LastRefuge.Data;
using LastRefuge.Core;

namespace LastRefuge.Systems
{
    public class ResourceSystem : IResourceSystem
    {
        private GameState gameState;
        
        private Dictionary<ResourceType, ResourceState> resourceMap = new Dictionary<ResourceType, ResourceState>();
        
        public event Action<ResourceType, int, int> OnResourceChanged;
        
        public void Initialize(GameState state)
        {
            gameState = state;
            resourceMap.Clear();
            
            if (gameState.resources != null)
            {
                foreach (var res in gameState.resources)
                {
                    resourceMap[res.type] = res;
                }
            }
            
            EnsureAllResourcesExist();
        }
        
        private void EnsureAllResourcesExist()
        {
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                if (!resourceMap.ContainsKey(type))
                {
                    int defaultCapacity = GetDefaultCapacity(type);
                    resourceMap[type] = new ResourceState(type, 0, defaultCapacity);
                }
            }
            
            var list = new List<ResourceState>(resourceMap.Values);
            gameState.resources = list.ToArray();
        }
        
        private int GetDefaultCapacity(ResourceType type)
        {
            return type switch
            {
                ResourceType.Food => 100,
                ResourceType.Water => 100,
                ResourceType.Wood => 200,
                ResourceType.Stone => 200,
                ResourceType.Iron => 100,
                ResourceType.Metal => 100,
                ResourceType.Fuel => 100,
                ResourceType.Parts => 100,
                ResourceType.Medicine => 50,
                ResourceType.Power => 500,
                _ => 100
            };
        }
        
        public int GetAmount(ResourceType type)
        {
            return resourceMap.TryGetValue(type, out var res) ? res.currentAmount : 0;
        }
        
        public int GetCapacity(ResourceType type)
        {
            return resourceMap.TryGetValue(type, out var res) ? res.capacity : 0;
        }
        
        public bool CanAfford(ResourceType type, int amount)
        {
            return GetAmount(type) >= amount;
        }
        
        public bool CanAfford(Dictionary<ResourceType, int> costs)
        {
            foreach (var kvp in costs)
            {
                if (!CanAfford(kvp.Key, kvp.Value)) return false;
            }
            return true;
        }
        
        public bool Add(ResourceType type, int amount, string source = "System")
        {
            if (amount <= 0) return false;
            
            var res = resourceMap[type];
            int oldAmount = res.currentAmount;
            int newAmount = Math.Min(res.currentAmount + amount, res.capacity);
            int actualDelta = newAmount - oldAmount;
            
            if (actualDelta <= 0) return false;
            
            res.currentAmount = newAmount;
            
            OnResourceChanged?.Invoke(type, oldAmount, newAmount);
            EventBus.Publish(new ResourceChangedEvent
            {
                resourceType = type,
                oldAmount = oldAmount,
                newAmount = newAmount,
                delta = actualDelta,
                source = source
            });
            
            return true;
        }
        
        public bool Remove(ResourceType type, int amount, string source = "System")
        {
            if (amount <= 0) return false;
            
            var res = resourceMap[type];
            if (res.currentAmount < amount) return false;
            
            int oldAmount = res.currentAmount;
            int newAmount = res.currentAmount - amount;
            
            res.currentAmount = newAmount;
            
            OnResourceChanged?.Invoke(type, oldAmount, newAmount);
            EventBus.Publish(new ResourceChangedEvent
            {
                resourceType = type,
                oldAmount = oldAmount,
                newAmount = newAmount,
                delta = -amount,
                source = source
            });
            
            return true;
        }
        
        public bool TryRemove(ResourceType type, int amount, string source = "System")
        {
            return Remove(type, amount, source);
        }
        
        public void SetCapacity(ResourceType type, int capacity)
        {
            if (resourceMap.TryGetValue(type, out var res))
            {
                res.capacity = Math.Max(capacity, 0);
                if (res.currentAmount > res.capacity)
                {
                    int oldAmount = res.currentAmount;
                    res.currentAmount = res.capacity;
                    OnResourceChanged?.Invoke(type, oldAmount, res.currentAmount);
                    EventBus.Publish(new ResourceChangedEvent
                    {
                        resourceType = type,
                        oldAmount = oldAmount,
                        newAmount = res.currentAmount,
                        delta = res.currentAmount - oldAmount,
                        source = "CapacityChange"
                    });
                }
            }
        }
        
        public void AddCapacity(ResourceType type, int delta)
        {
            SetCapacity(type, GetCapacity(type) + delta);
        }
        
        public Dictionary<ResourceType, int> GetAllResources()
        {
            var dict = new Dictionary<ResourceType, int>();
            foreach (var kvp in resourceMap)
            {
                dict[kvp.Key] = kvp.Value.currentAmount;
            }
            return dict;
        }
        
        public float GetRatio(ResourceType type)
        {
            var res = resourceMap[type];
            if (res.capacity <= 0) return 0f;
            return (float)res.currentAmount / res.capacity;
        }
        
        public int GetDailyConsumption(ResourceType type)
        {
            if (type == ResourceType.Food || type == ResourceType.Water)
            {
                int population = gameState.characters?.Length ?? 0;
                int aliveCount = 0;
                if (gameState.characters != null)
                {
                    foreach (var c in gameState.characters)
                    {
                        if (c.alive) aliveCount++;
                    }
                }
                return aliveCount * 2;
            }
            return 0;
        }
        
        public int GetDailyProduction(ResourceType type)
        {
            return 0;
        }
        
        public int GetNetDailyChange(ResourceType type)
        {
            return GetDailyProduction(type) - GetDailyConsumption(type);
        }
        
        public int GetEstimatedDaysRemaining(ResourceType type)
        {
            int netChange = GetNetDailyChange(type);
            int current = GetAmount(type);
            
            if (netChange >= 0) return -1;
            if (current <= 0) return 0;
            
            return current / Math.Abs(netChange);
        }
    }
}