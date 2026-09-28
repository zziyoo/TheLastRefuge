namespace LastRefuge.Data
{
    public enum BuildingCategory
    {
        Housing,
        Production,
        Storage,
        Utility,
        Medical,
        Research,
        Defense,
        Special
    }

    [System.Serializable]
    public class ResourceCost
    {
        public ResourceType type;
        public int amount;
    }

    [System.Serializable]
    public class ResourceProduction
    {
        public ResourceType type;
        public int baseAmount;
        public StatType primaryStat = StatType.Agriculture;
        public float efficiencyMultiplier = 1f;
    }

[System.Serializable]
    public class BuildingDefinition
    {
        public string id;
        public string name;
        public BuildingCategory category;
        public int maxLevel = 3;
        
        public ResourceCost[] constructionCost;
        public ResourceCost[] upkeepCost;
        
        public ResourceProduction[] production;
        public ResourceType[] storageCapacity;
        
        public int housingCapacity = 0;
        public int workerSlots = 1;
        
        /// <summary>Electricity drawn per production cycle. Merged with upkeepCost by BuildingSystem so Power is never paid twice.</summary>
        public int powerConsumption = 0;
        
        /// <summary>Declared output capability only. Actual electricity is produced through the single 'production' entry, never added on top of it.</summary>
        public int powerProduction = 0;
        
        public string[] prerequisites;
        public string[] tags;
        
        public string description;
        
        // maxCount: 0 = unlimited, 1 = unique, >1 = limited count
        public int maxCount = 0;
        
        public string GetCostString()
        {
            if (constructionCost == null || constructionCost.Length == 0) return "无";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var cost in constructionCost)
            {
                parts.Add($"{cost.type.GetDisplayName()}:{cost.amount}");
            }
            return string.Join(", ", parts);
        }
    }
}