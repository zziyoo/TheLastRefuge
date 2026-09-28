using LastRefuge.Data;

namespace LastRefuge.Core
{
    public interface ICharacterSystem
    {
        void Initialize(GameState state, IResourceSystem resSys, IRandomSystem randSys, IBuildingSystem buildSys);
        CharacterState[] GetAllCharacters();
        CharacterState[] GetAliveCharacters();
        CharacterState GetCharacter(string id);
        CharacterState CreateCharacter(string id, string name, Profession profession, CharacterStats baseStats = null);
        void GenerateInitialCharacters(int count = 4);
        bool AssignWork(string characterId, WorkType workType, string buildingId = null);
        void ProcessDailyConsumption();
        void ProcessNightRecovery();
        float GetWorkEfficiency(CharacterState character);
        void KillCharacter(string characterId, string cause);
        
        event System.Action<string> OnCharacterChanged;
        event System.Action<string> OnCharacterDied;
        event System.Action<string, WorkType> OnWorkAssigned;
    }
}