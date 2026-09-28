using LastRefuge.Data;

namespace LastRefuge.Core
{
    public interface ISaveSystem
    {
        void Initialize(GameState state, string version);
        void CreateNewGame(string seed = null);
        bool SaveGame(bool isAutoSave = false, string customName = null);
        bool LoadGame(string fileName);
        string[] GetSaveFiles();
        SaveInfo GetSaveInfo(string fileName);
        bool DeleteSave(string fileName);
        
        event System.Action<bool, string> OnSaveComplete;
        event System.Action<bool, string> OnLoadComplete;
    }
    
    public class SaveInfo
    {
        public string fileName;
        public string fullPath;
        public string gameSeed;
        public int day;
        public TimeSlot timeSlot;
        public string timestamp;
        public bool isAutoSave;
        public int population;
        public int aliveCount;
        
        public string GetDisplayName()
        {
            string type = isAutoSave ? "[自动]" : "[手动]";
            System.DateTime dt = System.DateTime.Parse(timestamp);
            return $"{type} 第{day}天 {timeSlot.GetDisplayName()} {dt:MM/dd HH:mm}";
        }
    }
}