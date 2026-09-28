using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LastRefuge.Core;
using LastRefuge.Data;
using LastRefuge.Systems;
using LastRefuge.Gameplay;

namespace LastRefuge.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }
        
        [Header("Main Panels")]
        public GameObject mainMenuPanel;
        public GameObject gamePanel;
        public GameObject saveLoadPanel;
        public GameObject settingsPanel;
        
        [Header("Game Panel - Top")]
        public TextMeshProUGUI dayTimeText;
        public TextMeshProUGUI weatherText;
        
        [Header("Game Panel - Resources")]
        public Transform resourceContainer;
        public GameObject resourceItemPrefab;
        
        [Header("Game Panel - Characters")]
        public Transform characterContainer;
        public GameObject characterItemPrefab;
        
        [Header("Game Panel - Bottom Buttons")]
        public Button nextTimeSlotButton;
        public Button personnelButton;
        public Button buildingButton;
        public Button resourceButton;
        public Button logButton;
        public Button saveButton;
        public Button menuButton;
        
        [Header("Panels")]
        public GameObject personnelPanel;
        public GameObject buildingPanel;
        public GameObject resourceDetailPanel;
        public GameObject logPanel;
        public GameObject saveLoadGamePanel;
        
        private GameManager gameManager;
        private bool isGamePanelActive = false;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        private void Start()
        {
            gameManager = GameManager.Instance;
            
            // Setup button listeners
            nextTimeSlotButton.onClick.AddListener(OnNextTimeSlotClicked);
            personnelButton.onClick.AddListener(() => TogglePanel(personnelPanel));
            buildingButton.onClick.AddListener(() => TogglePanel(buildingPanel));
            resourceButton.onClick.AddListener(() => TogglePanel(resourceDetailPanel));
            logButton.onClick.AddListener(() => TogglePanel(logPanel));
            saveButton.onClick.AddListener(() => TogglePanel(saveLoadGamePanel));
            menuButton.onClick.AddListener(OnMenuClicked);
            
            // Subscribe to events
            EventBus.Subscribe<ResourceChangedEvent>(OnResourceChanged);
            EventBus.Subscribe<CharacterChangedEvent>(OnCharacterChanged);
            EventBus.Subscribe<BuildingChangedEvent>(OnBuildingChanged);
            EventBus.Subscribe<TimeChangedEvent>(OnTimeChanged);
            EventBus.Subscribe<DayChangedEvent>(OnDayChanged);
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Subscribe<GameSavedEvent>(OnGameSaved);
            EventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);
            
            ShowMainMenu();
        }
        
        private void OnDestroy()
        {
            EventBus.Unsubscribe<ResourceChangedEvent>(OnResourceChanged);
            EventBus.Unsubscribe<CharacterChangedEvent>(OnCharacterChanged);
            EventBus.Unsubscribe<BuildingChangedEvent>(OnBuildingChanged);
            EventBus.Unsubscribe<TimeChangedEvent>(OnTimeChanged);
            EventBus.Unsubscribe<DayChangedEvent>(OnDayChanged);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Unsubscribe<GameSavedEvent>(OnGameSaved);
            EventBus.Unsubscribe<GameLoadedEvent>(OnGameLoaded);
        }
        
        public void ShowMainMenu()
        {
            mainMenuPanel.SetActive(true);
            gamePanel.SetActive(false);
            saveLoadPanel.SetActive(false);
            settingsPanel.SetActive(false);
            CloseAllSubPanels();
            isGamePanelActive = false;
        }
        
        public void ShowGame()
        {
            mainMenuPanel.SetActive(false);
            gamePanel.SetActive(true);
            saveLoadPanel.SetActive(false);
            settingsPanel.SetActive(false);
            CloseAllSubPanels();
            isGamePanelActive = true;
            
            RefreshAll();
        }
        
        public void ShowSaveLoad(bool isSave)
        {
            mainMenuPanel.SetActive(false);
            gamePanel.SetActive(false);
            saveLoadPanel.SetActive(true);
            settingsPanel.SetActive(false);
            
            // Populate save list
            // PopulateSaveList(isSave);
        }
        
        private void CloseAllSubPanels()
        {
            personnelPanel?.SetActive(false);
            buildingPanel?.SetActive(false);
            resourceDetailPanel?.SetActive(false);
            logPanel?.SetActive(false);
            saveLoadGamePanel?.SetActive(false);
        }
        
        public void TogglePanel(GameObject panel)
        {
            if (panel == null) return;
            
            bool willOpen = !panel.activeSelf;
            CloseAllSubPanels();
            
            if (willOpen)
            {
                panel.SetActive(true);
                RefreshPanel(panel);
            }
        }
        
        private void RefreshPanel(GameObject panel)
        {
            if (panel == personnelPanel) RefreshPersonnelPanel();
            else if (panel == buildingPanel) RefreshBuildingPanel();
            else if (panel == resourceDetailPanel) RefreshResourceDetailPanel();
            else if (panel == logPanel) RefreshLogPanel();
        }
        
        public void RefreshAll()
        {
            RefreshDayTime();
            RefreshResources();
            RefreshCharacters();
            RefreshButtons();
        }
        
        private void RefreshDayTime()
        {
            if (dayTimeText != null && gameManager != null)
            {
                dayTimeText.text = gameManager.timeSystem.GetTimeDisplay();
            }
        }
        
        private void RefreshResources()
        {
            if (resourceContainer == null || resourceItemPrefab == null || gameManager == null) return;
            
            // Clear existing
            foreach (Transform child in resourceContainer)
            {
                Destroy(child.gameObject);
            }
            
            // Add resources
            var resources = gameManager.resourceSystem.GetAllResources();
            foreach (var kvp in resources)
            {
                var item = Instantiate(resourceItemPrefab, resourceContainer);
                var text = item.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    int amount = kvp.Value;
                    int capacity = gameManager.resourceSystem.GetCapacity(kvp.Key);
                    int netChange = gameManager.resourceSystem.GetNetDailyChange(kvp.Key);
                    int daysRemaining = gameManager.resourceSystem.GetEstimatedDaysRemaining(kvp.Key);
                    
                    string changeStr = netChange >= 0 ? $"+{netChange}" : netChange.ToString();
                    string daysStr = daysRemaining >= 0 ? (daysRemaining == -1 ? "∞" : $"{daysRemaining}天") : "N/A";
                    
                    text.text = $"{kvp.Key.GetDisplayName()}: {amount}/{capacity} ({changeStr}/天, {daysStr})";
                }
            }
        }
        
        private void RefreshCharacters()
        {
            if (characterContainer == null || characterItemPrefab == null || gameManager == null) return;
            
            foreach (Transform child in characterContainer)
            {
                Destroy(child.gameObject);
            }
            
            var characters = gameManager.characterSystem.GetAliveCharacters();
            foreach (var character in characters)
            {
                var item = Instantiate(characterItemPrefab, characterContainer);
                var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
                
                if (texts.Length >= 2)
                {
                    texts[0].text = $"{character.name} ({character.profession})";
                    texts[1].text = $"{character.currentWork.GetDisplayName()} | HP:{character.health} 饥饿:{character.hunger:F0} 压力:{character.stress:F0} 疲劳:{character.fatigue:F0}";
                }
                
                // Add work selection buttons
                var workButtons = item.GetComponentsInChildren<Button>();
                foreach (var btn in workButtons)
                {
                    var workType = (WorkType)Enum.Parse(typeof(WorkType), btn.name.Replace("Btn_", ""));
                    btn.onClick.AddListener(() => gameManager.AssignWork(character.characterId, workType));
                }
            }
        }
        
        private void RefreshPersonnelPanel()
        {
            // Detailed personnel management
            RefreshCharacters();
        }
        
        private void RefreshBuildingPanel()
        {
            // Building construction and management
        }
        
        private void RefreshResourceDetailPanel()
        {
            // Detailed resource view
        }
        
        private void RefreshLogPanel()
        {
            // Event log
        }
        
        private void RefreshButtons()
        {
            if (nextTimeSlotButton != null)
            {
                nextTimeSlotButton.interactable = gameManager.GetCurrentGameplayState() == GameplayState.Planning ||
                                                  gameManager.GetCurrentGameplayState() == GameplayState.Action ||
                                                  gameManager.GetCurrentGameplayState() == GameplayState.Evening ||
                                                  gameManager.GetCurrentGameplayState() == GameplayState.Night;
            }
        }
        
        // Event Handlers
        private void OnResourceChanged(ResourceChangedEvent e)
        {
            if (isGamePanelActive) RefreshResources();
        }
        
        private void OnCharacterChanged(CharacterChangedEvent e)
        {
            if (isGamePanelActive) RefreshCharacters();
        }
        
        private void OnBuildingChanged(BuildingChangedEvent e)
        {
            if (isGamePanelActive) RefreshResources(); // Capacity changes affect resources
        }
        
        private void OnTimeChanged(TimeChangedEvent e)
        {
            RefreshDayTime();
            RefreshButtons();
        }
        
        private void OnDayChanged(DayChangedEvent e)
        {
            RefreshDayTime();
            RefreshResources();
        }
        
        private void OnGameStateChanged(GameStateChangedEvent e)
        {
            RefreshButtons();
        }
        
        private void OnGameSaved(GameSavedEvent e)
        {
            UnityEngine.Debug.Log($"Game saved: {e.savePath}");
        }
        
        private void OnGameLoaded(GameLoadedEvent e)
        {
            ShowGame();
        }
        
        // Button Handlers
        public void OnNextTimeSlotClicked()
        {
            gameManager.AdvanceTimeSlot();
        }
        
        public void OnMenuClicked()
        {
            ShowMainMenu();
        }
        
        // Main Menu Buttons
        public void OnNewGameClicked()
        {
            gameManager.NewGame();
            ShowGame();
        }
        
        public void OnContinueClicked()
        {
            var saves = gameManager.saveSystem.GetSaveFiles();
            if (saves.Length > 0)
            {
                // Load most recent
                Array.Sort(saves);
                string latest = saves[saves.Length - 1];
                string fileName = Path.GetFileName(latest);
                gameManager.LoadGame(fileName);
                ShowGame();
            }
        }
        
        public void OnLoadGameClicked()
        {
            ShowSaveLoad(false);
        }
        
        public void OnSettingsClicked()
        {
            settingsPanel.SetActive(true);
        }
        
        public void OnQuitClicked()
        {
            Application.Quit();
        }
    }
}