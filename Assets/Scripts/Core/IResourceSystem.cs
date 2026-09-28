using LastRefuge.Data;
using System.Collections.Generic;
using System;

namespace LastRefuge.Core
{
    public interface IResourceSystem
    {
        void Initialize(GameState state);
        void Initialize(GameState state, IBuildingSystem buildSys);
        int GetAmount(ResourceType type);
        int GetCapacity(ResourceType type);
        bool CanAfford(ResourceType type, int amount);
        bool CanAfford(Dictionary<ResourceType, int> costs);
        bool Add(ResourceType type, int amount, string source = "System");
        bool Remove(ResourceType type, int amount, string source = "System");
        bool TryRemove(ResourceType type, int amount, string source = "System");
        void SetCapacity(ResourceType type, int capacity);
        void AddCapacity(ResourceType type, int delta);
        Dictionary<ResourceType, int> GetAllResources();
        float GetRatio(ResourceType type);
        int GetDailyConsumption(ResourceType type);
        int GetDailyProduction(ResourceType type);
        int GetNetDailyChange(ResourceType type);
        int GetEstimatedDaysRemaining(ResourceType type);
        
        event Action<ResourceType, int, int> OnResourceChanged;
    }
}