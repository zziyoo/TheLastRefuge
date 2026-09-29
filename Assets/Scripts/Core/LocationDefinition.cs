using LastRefuge.Data;

namespace LastRefuge.Core
{
    [System.Serializable]
    public class ResourceYield
    {
        public ResourceType resource;
        public int min;
        public int max;
    }

    [System.Serializable]
    public class LocationDefinition
    {
        public string id;
        public string name;
        public int tier;
        public int unlockDay;
        public int distance;
        public int danger;
        public ResourceYield[] baseYield;
        public string[] eventPool;
        public string[] prerequisites;
        public string[] tags;
    }
}
