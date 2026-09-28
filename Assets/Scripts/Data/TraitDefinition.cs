namespace LastRefuge.Data
{
    public enum TraitType
    {
        Positive,
        Neutral,
        Negative
    }

    [System.Serializable]
    public class TraitDefinition
    {
        public string id;
        public string name;
        public TraitType type;
        public string description;
        
        public float workEfficiencyModifier = 1f;
        public float stressGainModifier = 1f;
        public float hungerGainModifier = 1f;
        public float fatigueGainModifier = 1f;
        public float resourceConsumptionModifier = 1f;
        public float socialModifier = 0f;
        
        public StatType? bonusStat;
        public int bonusStatValue = 0;
        
        public string[] incompatibleTraits;
        public string[] requiredTraits;
    }
}