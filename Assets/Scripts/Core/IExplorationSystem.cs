using LastRefuge.Data;

namespace LastRefuge.Core
{
    public interface IExplorationSystem
    {
        void Initialize(GameState state, IRandomSystem randomSystem, IContentDatabase contentDatabase,
                        IEventSystem eventSystem, IResourceSystem resourceSystem,
                        ICharacterSystem characterSystem);
        void SetTimeAdvance(System.Action advanceOneSlot);
        bool ExploreLocation(string locationId, string[] teamMemberIds, out string rejectReason);
        bool CanExplore(string locationId, string[] teamMemberIds, out string reason);
        void Reset();
    }
}
