using System;
using UnityEngine;
using LastRefuge.Data;
using LastRefuge.Core;
using LastRefuge.Systems;
using LastRefuge.Save;

namespace LastRefuge.Gameplay
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        
        [Header("Game State")]
        public GameState gameState;
        
        [Header("Systems")]
        public ITimeSystem timeSystem;
        public IRandomSystem randomSystem;
        public IResourceSystem resourceSystem;
        public ICharacterSystem characterSystem;
        public IBuildingSystem buildingSystem;
        public EffectResolver effectResolver;
        public ISaveSystem saveSystem;
        
        [Header("Settings")]
        public string gameVersion = "0.1.0";
        public bool autoSaveOnDayEnd = true;
        
        private bool isInitialized = false;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            InitializeCoreSystems();
        }
        
        private void InitializeCoreSystems()
        {
            gameState = new GameState();
            
            timeSystem = new TimeSystem();
            randomSystem = new RandomSystem();
            resourceSystem = new ResourceSystem();
            characterSystem = new CharacterSystem();
            buildingSystem = new BuildingSystem();
            effectResolver = new EffectResolver();
            saveSystem = new SaveSystem();
            
            timeSystem.Initialize(gameState, randomSystem);
            resourceSystem.Initialize(gameState, buildingSystem, characterSystem);
            characterSystem.Initialize(gameState, resourceSystem, randomSystem, buildingSystem);
            buildingSystem.Initialize(gameState, resourceSystem, characterSystem);
            effectResolver.Initialize(gameState, resourceSystem, characterSystem, buildingSystem);
            saveSystem.Initialize(gameState, gameVersion);
            
            timeSystem.OnDayEnd += OnDayEnd;
            
            isInitialized = true;
            
            UnityEngine.Debug.Log("GameManager initialized");
        }
        
        private void Start()
        {
            SetupMobilePortrait();
        }
        
        private void SetupMobilePortrait()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
        
        public void NewGame(string seed = null)
        {
            saveSystem.CreateNewGame(seed);
            
            randomSystem.Initialize(gameState.gameSeed);
            
            GenerateInitialState();
            
            timeSystem.SetTime(1, TimeSlot.Morning);
            timeSystem.SetGameplayState(GameplayState.Morning);
            
            UnityEngine.Debug.Log($"New game started with seed: {gameState.gameSeed}");
        }
        
        private void GenerateInitialState()
        {
            // Starting stock is sized so the first shelter plus one farm can be built
            // on day one: farm costs 30 Wood / 10 Stone, shelter costs 20 Wood / 10 Stone.
            resourceSystem.Add(ResourceType.Food, 30, "Initial");
            resourceSystem.Add(ResourceType.Water, 30, "Initial");
            resourceSystem.Add(ResourceType.Wood, 60, "Initial");
            resourceSystem.Add(ResourceType.Stone, 30, "Initial");
            resourceSystem.Add(ResourceType.Iron, 10, "Initial");
            
            characterSystem.GenerateInitialCharacters(4);
            
            buildingSystem.Build("shelter_temp");
            
            gameState.gameplayState = GameplayState.Morning;
        }
        
        public void LoadGame(string fileName)
        {
            if (saveSystem.LoadGame(fileName))
            {
                randomSystem.Initialize(gameState.gameSeed);
                timeSystem.Initialize(gameState, randomSystem);
                resourceSystem.Initialize(gameState, buildingSystem, characterSystem);
                characterSystem.Initialize(gameState, resourceSystem, randomSystem, buildingSystem);
                buildingSystem.Initialize(gameState, resourceSystem, characterSystem);
                effectResolver.Initialize(gameState, resourceSystem, characterSystem, buildingSystem);
                
                timeSystem.OnDayEnd -= OnDayEnd;
                timeSystem.OnDayEnd += OnDayEnd;
            }
        }
        
        public void AdvanceTimeSlot()
        {
            if (!isInitialized) return;
            
            TimeSlot currentSlot = timeSystem.CurrentTimeSlot;
            GameplayState currentState = timeSystem.GetGameplayState();
            
            ProcessTimeSlot(currentSlot);
            
            timeSystem.AdvanceTimeSlot();
            
            UpdateGameplayState();
        }
        
        private void ProcessTimeSlot(TimeSlot slot)
        {
            switch (slot)
            {
                case TimeSlot.Morning:
                    ProcessMorning();
                    break;
                case TimeSlot.Planning:
                    break;
                case TimeSlot.Action:
                    ProcessAction();
                    break;
                case TimeSlot.Evening:
                    ProcessEvening();
                    break;
                case TimeSlot.Night:
                    ProcessNight();
                    break;
                case TimeSlot.DayEnd:
                    ProcessDayEnd();
                    break;
            }
        }
        
        private void ProcessMorning()
        {
            UnityEngine.Debug.Log($"=== Day {gameState.currentDay} Morning ===");
            timeSystem.SetGameplayState(GameplayState.Planning);
        }
        
        private void ProcessAction()
        {
            UnityEngine.Debug.Log($"=== Day {gameState.currentDay} Action ===");
            
            // Action: buildings produce (and pay their running cost), the colony then eats
            // and drinks from what the day actually produced and what is in storage.
            buildingSystem.ProcessBuildingProduction();
            characterSystem.ProcessDailyConsumption();
            
            timeSystem.SetGameplayState(GameplayState.Action);
        }
        
        private void ProcessEvening()
        {
            UnityEngine.Debug.Log($"=== Day {gameState.currentDay} Evening ===");
            timeSystem.SetGameplayState(GameplayState.Evening);
        }
        
        private void ProcessNight()
        {
            UnityEngine.Debug.Log($"=== Day {gameState.currentDay} Night ===");
            // Night: characters rest, fatigue and stress recover, starvation takes its toll.
            characterSystem.ProcessNightRecovery();
            timeSystem.SetGameplayState(GameplayState.Night);
        }
        
        private void ProcessDayEnd()
        {
            UnityEngine.Debug.Log($"=== Day {gameState.currentDay} End ===");
            // DayEnd: the day is written to disk, TimeSystem then rolls over to the next morning.
            buildingSystem.RecalculateAllBuildingEffects();
            
            if (autoSaveOnDayEnd)
            {
                saveSystem.SaveGame(true);
            }
            
            GenerateDailyReport();
        }
        
        private void OnDayEnd()
        {
            timeSystem.SetGameplayState(GameplayState.Morning);
        }
        
        private void UpdateGameplayState()
        {
            TimeSlot slot = timeSystem.CurrentTimeSlot;
            
            switch (slot)
            {
                case TimeSlot.Morning:
                    timeSystem.SetGameplayState(GameplayState.Morning);
                    break;
                case TimeSlot.Planning:
                    timeSystem.SetGameplayState(GameplayState.Planning);
                    break;
                case TimeSlot.Action:
                    timeSystem.SetGameplayState(GameplayState.Action);
                    break;
                case TimeSlot.Evening:
                    timeSystem.SetGameplayState(GameplayState.Evening);
                    break;
                case TimeSlot.Night:
                    timeSystem.SetGameplayState(GameplayState.Night);
                    break;
                case TimeSlot.DayEnd:
                    timeSystem.SetGameplayState(GameplayState.DayEnd);
                    break;
            }
        }
        
        private void GenerateDailyReport()
        {
            int food = resourceSystem.GetAmount(ResourceType.Food);
            int water = resourceSystem.GetAmount(ResourceType.Water);
            int pop = buildingSystem.GetCurrentPopulation();
            
            UnityEngine.Debug.Log($"Daily Report - Day {gameState.currentDay}: Food={food}, Water={water}, Population={pop}");
        }
        
        public void AssignWork(string characterId, WorkType workType, string buildingId = null)
        {
            characterSystem.AssignWork(characterId, workType, buildingId);
        }
        
        public bool BuildBuilding(string definitionId)
        {
            return buildingSystem.Build(definitionId) != null;
        }
        
        public bool UpgradeBuilding(string buildingId)
        {
            return buildingSystem.UpgradeBuilding(buildingId);
        }
        
        public void SaveGame(bool autoSave = false)
        {
            saveSystem.SaveGame(autoSave);
        }
        
        public string GetGameSeed()
        {
            return gameState.gameSeed;
        }
        
        public int GetCurrentDay()
        {
            return gameState.currentDay;
        }
        
        public TimeSlot GetCurrentTimeSlot()
        {
            return gameState.currentTimeSlot;
        }
        
        public GameplayState GetCurrentGameplayState()
        {
            return gameState.gameplayState;
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && autoSaveOnDayEnd)
            {
                saveSystem.SaveGame(true);
            }
        }
        
        private void OnApplicationQuit()
        {
            if (autoSaveOnDayEnd)
            {
                saveSystem.SaveGame(true);
            }
        }
    }
}