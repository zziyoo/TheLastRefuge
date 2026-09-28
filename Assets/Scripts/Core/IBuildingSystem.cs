using LastRefuge.Data;
using System.Collections.Generic;

namespace LastRefuge.Core
{
    public interface IBuildingSystem
    {
        void Initialize(GameState state, IResourceSystem resSys, ICharacterSystem charSys);
        BuildingDefinition GetBuildingDefinition(string id);
        IEnumerable<BuildingDefinition> GetAvailableBuildings();
        BuildingState[] GetAllBuildings();
        BuildingState GetBuilding(string buildingId);
        BuildingState[] GetBuildingsByDefinition(string definitionId);
        bool CanBuild(string definitionId);
        bool CanBuildCount(string definitionId);
        bool HasBuilding(string definitionId);
        bool WorkTypeSupportedBy(string buildingId, WorkType workType);
        BuildingState Build(string definitionId);
        bool CanUpgrade(string buildingId);
        bool UpgradeBuilding(string buildingId);
        bool AssignWorker(string buildingId, string characterId);
        bool RemoveWorker(string buildingId, string characterId);
        bool CanOperate(BuildingState building);
        Dictionary<ResourceType, int> CalculateOperationCost(BuildingState building);
        Dictionary<ResourceType, int> CalculateProduction(BuildingState building);
        int GetBuildingDailyProduction(BuildingState building, ResourceType type);
        void RecalculateAllBuildingEffects();
        void ProcessBuildingProduction();
        int GetTotalHousingCapacity();
        int GetCurrentPopulation();
        bool IsOvercrowded();
        
        event System.Action<string> OnBuildingChanged;
        event System.Action<string> OnBuildingConstructed;
        event System.Action<string> OnBuildingDestroyed;
    }
}