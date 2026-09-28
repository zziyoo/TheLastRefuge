namespace LastRefuge.Data
{
    public enum Profession
    {
        Farmer,
        Miner,
        Engineer,
        Researcher,
        Doctor,
        Hunter,
        Soldier,
        Merchant
    }

    public enum StatType
    {
        Agriculture,
        Gathering,
        Engineering,
        Research,
        Medical,
        Exploration,
        Combat
    }

    public enum NeedType
    {
        Food,
        Sleep,
        Safety,
        Entertainment,
        Social
    }

    [System.Serializable]
    public class CharacterStats
    {
        public int agriculture = 1;
        public int gathering = 1;
        public int engineering = 1;
        public int research = 1;
        public int medical = 1;
        public int exploration = 1;
        public int combat = 1;

        public int GetStat(StatType type)
        {
            return type switch
            {
                StatType.Agriculture => agriculture,
                StatType.Gathering => gathering,
                StatType.Engineering => engineering,
                StatType.Research => research,
                StatType.Medical => medical,
                StatType.Exploration => exploration,
                StatType.Combat => combat,
                _ => 1
            };
        }

        public void SetStat(StatType type, int value)
        {
            switch (type)
            {
                case StatType.Agriculture: agriculture = value; break;
                case StatType.Gathering: gathering = value; break;
                case StatType.Engineering: engineering = value; break;
                case StatType.Research: research = value; break;
                case StatType.Medical: medical = value; break;
                case StatType.Exploration: exploration = value; break;
                case StatType.Combat: combat = value; break;
            }
        }

        public void AddStat(StatType type, int delta)
        {
            SetStat(type, GetStat(type) + delta);
        }

        public float GetWorkEfficiency(StatType primaryStat)
        {
            int stat = GetStat(primaryStat);
            return 0.5f + (stat * 0.1f);
        }
    }

    [System.Serializable]
    public class CharacterNeeds
    {
        public float food = 100f;
        public float sleep = 100f;
        public float safety = 100f;
        public float entertainment = 100f;
        public float social = 100f;

        public float GetNeed(NeedType type)
        {
            return type switch
            {
                NeedType.Food => food,
                NeedType.Sleep => sleep,
                NeedType.Safety => safety,
                NeedType.Entertainment => entertainment,
                NeedType.Social => social,
                _ => 100f
            };
        }

        public void SetNeed(NeedType type, float value)
        {
            switch (type)
            {
                case NeedType.Food: food = value; break;
                case NeedType.Sleep: sleep = value; break;
                case NeedType.Safety: safety = value; break;
                case NeedType.Entertainment: entertainment = value; break;
                case NeedType.Social: social = value; break;
            }
        }
    }
}