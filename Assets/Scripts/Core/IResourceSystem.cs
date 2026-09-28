using LastRefuge.Data;

namespace LastRefuge.Core
{
    public interface IResourceSystem
    {
        void Initialize(GameState state);
        int GetAmount(ResourceType type);
        int GetCapacity(ResourceType type);
        bool CanAfford(ResourceType type, int amount);
        bool CanAfford(System.Collections.Generic.Dictionary<ResourceType, int> costs);
        bool Add(ResourceType type, int amount, string source = "System");
        bool Remove(ResourceType type, int amount, string source = "System");
        bool TryRemove(ResourceType type, int amount, string source = "System");
        void SetCapacity(ResourceType type, int capacity);
        void AddCapacity(ResourceType type, int delta);
        System.Collections.Generic.Dictionary<ResourceType, int> GetAllResources();
        float GetRatio(ResourceType type);
        int GetDailyConsumption(ResourceType type);
        int GetDailyProduction(ResourceType type);
        int GetNetDailyChange(ResourceType type);
        int GetEstimatedDaysRemaining(ResourceType type);
        
        event System.Action<ResourceType, int, int> OnResourceChanged;
    }
}