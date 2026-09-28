namespace LastRefuge.Data
{
    public enum WorkType
    {
        Idle,
        Farming,
        Gathering,
        Engineering,
        Researching,
        Medical,
        Exploring,
        Combat,
        Construction,
        Maintenance
    }

    public static class WorkTypeExtensions
    {
        public static string GetDisplayName(this WorkType type)
        {
            return type switch
            {
                WorkType.Idle => "待命",
                WorkType.Farming => "农业",
                WorkType.Gathering => "采集",
                WorkType.Engineering => "工程",
                WorkType.Researching => "研究",
                WorkType.Medical => "医疗",
                WorkType.Exploring => "探索",
                WorkType.Combat => "战斗",
                WorkType.Construction => "建造",
                WorkType.Maintenance => "维护",
                _ => type.ToString()
            };
        }

        public static StatType GetPrimaryStat(this WorkType type)
        {
            return type switch
            {
                WorkType.Farming => StatType.Agriculture,
                WorkType.Gathering => StatType.Gathering,
                WorkType.Engineering => StatType.Engineering,
                WorkType.Researching => StatType.Research,
                WorkType.Medical => StatType.Medical,
                WorkType.Exploring => StatType.Exploration,
                WorkType.Combat => StatType.Combat,
                WorkType.Construction => StatType.Engineering,
                WorkType.Maintenance => StatType.Engineering,
                _ => StatType.Agriculture
            };
        }

        public static ResourceType[] GetProducedResources(this WorkType type)
        {
            return type switch
            {
                WorkType.Farming => new ResourceType[] { ResourceType.Food },
                WorkType.Gathering => new ResourceType[] { ResourceType.Wood, ResourceType.Stone, ResourceType.Iron },
                WorkType.Engineering => new ResourceType[] { ResourceType.Parts },
                WorkType.Researching => new ResourceType[] { },
                WorkType.Medical => new ResourceType[] { },
                WorkType.Exploring => new ResourceType[] { },
                _ => new ResourceType[0]
            };
        }
    }
}