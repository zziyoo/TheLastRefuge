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
        public int powerConsumption = 0;
        public int powerProduction = 0;
        
        public string[] prerequisites;
        public string[] tags;
        
        public string description;
    }
}