namespace LastRefuge.Data
{
    public enum ResourceType
    {
        Food,
        Water,
        Wood,
        Stone,
        Iron,
        Metal,
        Fuel,
        Parts,
        Medicine,
        Power
    }

    public static class ResourceTypeExtensions
    {
        public static string GetDisplayName(this ResourceType type)
        {
            return type switch
            {
                ResourceType.Food => "食物",
                ResourceType.Water => "水",
                ResourceType.Wood => "木材",
                ResourceType.Stone => "石材",
                ResourceType.Iron => "铁",
                ResourceType.Metal => "金属",
                ResourceType.Fuel => "燃料",
                ResourceType.Parts => "零件",
                ResourceType.Medicine => "药品",
                ResourceType.Power => "电力",
                _ => type.ToString()
            };
        }
    }
}