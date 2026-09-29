using System;
using System.IO;
using System.Linq;
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
        // Helper methods for UI creation
        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }
        
        private static GameObject CreateText(Transform parent, string text, int fontSize, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            return go;
        }
        
        private static Button CreateButton(Transform parent, string text, System.Action onClick, float width, float height)
        {
            var go = new GameObject("Btn_" + text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            
            var image = go.AddComponent<Image>();
            image.sprite = buttonSprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.2f, 0.3f, 0.4f, 0.95f);
            
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());
            
            // Add hover/pressed colors
            var colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.3f, 0.4f, 0.95f);
            colors.highlightedColor = new Color(0.3f, 0.4f, 0.5f, 1f);
            colors.pressedColor = new Color(0.15f, 0.25f, 0.35f, 1f);
            colors.selectedColor = new Color(0.25f, 0.35f, 0.45f, 1f);
            colors.disabledColor = new Color(0.1f, 0.15f, 0.2f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            
            var tmp = CreateText(go.transform, text, 20, FontStyles.Bold);
            tmp.GetComponent<RectTransform>().sizeDelta = new Vector2(width - 20, height - 10);
            
            return button;
        }
        
        // Reference to button sprite for UI creation
        private static Sprite buttonSprite;
        
        private void InitializeButtonSprite()
        {
            if (buttonSprite == null)
            {
                var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                Color[] colors = new Color[256];
                Color borderColor = new Color(0.3f, 0.4f, 0.5f, 1f);
                Color centerColor = new Color(0.2f, 0.3f, 0.4f, 0.95f);
                
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        bool isBorder = x == 0 || x == 15 || y == 0 || y == 15;
                        colors[y * 16 + x] = isBorder ? borderColor : centerColor;
                    }
                }
                
                tex.SetPixels(colors);
                tex.Apply();
                tex.filterMode = FilterMode.Bilinear;
                
                buttonSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
            }
        }
        
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

        [Header("Game Panel - Detail / Buildings")]
        public Transform detailContainer;
        
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
        
        private GameManager _gameManager;
        
        /// <summary>
        /// Resolved lazily: the GameManager can be created after this UI, so caching it once
        /// in Start() would leave every button dead for the rest of the session.
        /// </summary>
        private GameManager gameManager
        {
            get
            {
                if (_gameManager == null)
                {
                    _gameManager = GameManager.Instance;
                }
                return _gameManager;
            }
        }
        
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
            UnityEngine.Debug.Log("UIManager.Start() called");
            _gameManager = GameManager.Instance;
            UnityEngine.Debug.Log("GameManager.Instance: " + (gameManager != null ? "found" : "NULL"));
            
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

            // Rebind explicitly: the game panel must always be repainted from the live
            // GameManager state, never left showing whatever Start() happened to render.
            RefreshUI();
            UnityEngine.Debug.Log($"UIManager.ShowGame: resources={gameManager?.resourceSystem?.GetAllResources().Count ?? 0} " +
                                  $"characters={gameManager?.characterSystem?.GetAliveCharacters().Length ?? 0} " +
                                  $"buildings={gameManager?.buildingSystem?.GetAllBuildings().Length ?? 0}");
        }
        
        public void ShowSaveLoad(bool isSave)
        {
            mainMenuPanel.SetActive(false);
            gamePanel.SetActive(false);
            saveLoadPanel.SetActive(true);
            settingsPanel.SetActive(false);
            
            PopulateSaveList(isSave);
        }
        
        private void PopulateSaveList(bool isSave)
        {
            if (saveLoadPanel == null || gameManager == null) return;
            
            // Clear existing content except the back button
            foreach (Transform child in saveLoadPanel.transform)
            {
                if (child.GetComponent<Button>() != null) continue; // Keep back button
                Destroy(child.gameObject);
            }
            
            var layout = saveLoadPanel.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = saveLoadPanel.AddComponent<VerticalLayoutGroup>();
            }
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 15;
            layout.padding = new RectOffset(50, 50, 50, 50);
            
            var saves = gameManager.saveSystem.GetSaveFiles();
            
            if (saves.Length == 0)
            {
                CreateText(saveLoadPanel.transform, "暂无存档", 24);
                return;
            }
            
            Array.Sort(saves); // Sort by name (which includes timestamp)
            
            foreach (var savePath in saves)
            {
                var fileName = Path.GetFileName(savePath);
                var saveInfo = gameManager.saveSystem.GetSaveInfo(fileName);
                
                if (saveInfo == null) continue;
                
                var saveRow = CreateUIObject("Save_" + fileName, saveLoadPanel.transform);
                var rowLayout = saveRow.AddComponent<HorizontalLayoutGroup>();
                rowLayout.childAlignment = TextAnchor.MiddleLeft;
                rowLayout.spacing = 10;
                rowLayout.padding = new RectOffset(20, 20, 10, 10);
                
                var bg = saveRow.AddComponent<Image>();
                bg.color = new Color(0.2f, 0.2f, 0.25f, 0.8f);
                
                var infoText = CreateText(saveRow.transform, $"Day {saveInfo.day} {saveInfo.timeSlot}", 20);
                infoText.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 40);
                
                var popText = CreateText(saveRow.transform, $"人口: {saveInfo.population}", 16);
                popText.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 40);
                
                var timeText = CreateText(saveRow.transform, saveInfo.timestamp, 14);
                timeText.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 40);
                
                var actionBtn = CreateButton(saveRow.transform, "读取", () => 
                {
                    gameManager.LoadGame(fileName);
                    ShowGame();
                }, 100, 40);
            }
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
        
        /// <summary>
        /// Single entry point for "the game state changed, repaint everything".
        /// Called by GameManager-driven paths (new game, load, time advance) instead of
        /// relying on Start() having run at the right moment.
        /// UI -> GameManager -> GameState -> Systems: the UI never stores game data.
        /// </summary>
        public void RefreshUI()
        {
            RefreshAll();
        }

        public void RefreshAll()
        {
            RefreshDayTime();
            RefreshResources();
            RefreshCharacters();
            RefreshDetailPanel();
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
                item.SetActive(true); // Ensure instance is active
                
                var itemUI = item.GetComponent<ResourceItemUI>();
                if (itemUI != null)
                {
                    int amount = kvp.Value;
                    int capacity = gameManager.resourceSystem.GetCapacity(kvp.Key);
                    int netChange = gameManager.resourceSystem.GetNetDailyChange(kvp.Key);
                    int daysRemaining = gameManager.resourceSystem.GetEstimatedDaysRemaining(kvp.Key);
                    
                    string changeStr = netChange >= 0 ? $"+{netChange}" : netChange.ToString();
                    string daysStr = daysRemaining >= 0 ? (daysRemaining == -1 ? "∞" : $"{daysRemaining}天") : "N/A";
                    
                    itemUI.Setup(kvp.Key.GetDisplayName(), amount, capacity, netChange, daysRemaining);
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
                item.SetActive(true); // Ensure instance is active
                
                var itemUI = item.GetComponent<CharacterItemUI>();
                if (itemUI != null)
                {
                    itemUI.Setup(character, gameManager.characterSystem);
                    
                    // Set up work buttons
                    if (itemUI.workButtons != null && itemUI.workButtons.Length > 0)
                    {
                        var workTypes = new[] { WorkType.Idle, WorkType.Farming, WorkType.Gathering, WorkType.Engineering, WorkType.Researching, WorkType.Medical };
                        for (int i = 0; i < Mathf.Min(itemUI.workButtons.Length, workTypes.Length); i++)
                        {
                            var btn = itemUI.workButtons[i];
                            var workType = workTypes[i];
                            btn.onClick.RemoveAllListeners();
                            btn.onClick.AddListener(() => gameManager.AssignWork(character.characterId, workType));
                            btn.GetComponentInChildren<TextMeshProUGUI>().text = workType.GetDisplayName();
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// The right-hand "详情 / 建筑" column. Data comes from BuildingSystem only;
        /// the UI holds no building state of its own.
        /// </summary>
        private void RefreshDetailPanel()
        {
            if (detailContainer == null || gameManager == null) return;

            foreach (Transform child in detailContainer)
            {
                Destroy(child.gameObject);
            }

            var buildings = gameManager.buildingSystem.GetAllBuildings();
            if (buildings.Length == 0)
            {
                var emptyText = CreateText(detailContainer, "暂无建筑", 18);
                emptyText.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 30);
                return;
            }

            foreach (var building in buildings)
            {
                var def = gameManager.buildingSystem.GetBuildingDefinition(building.definitionId);
                string buildingName = def != null ? def.name : building.definitionId;
                string status = GetBuildingStatusText(building);
                int workers = building.assignedWorkers?.Length ?? 0;
                int slots = def?.workerSlots ?? 0;

                var row = CreateUIObject("Detail_" + building.buildingId, detailContainer);
                var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
                rowLayout.childAlignment = TextAnchor.MiddleLeft;
                rowLayout.spacing = 10;
                rowLayout.padding = new RectOffset(10, 5, 5, 5);
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = false;
                rowLayout.childForceExpandWidth = false;
                rowLayout.childForceExpandHeight = false;

                var nameText = CreateText(row.transform, $"{buildingName} (Lv.{building.level})", 18, FontStyles.Bold);
                nameText.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 30);

                var workerText = CreateText(row.transform, $"工人 {workers}/{slots}", 14);
                workerText.GetComponent<RectTransform>().sizeDelta = new Vector2(90, 30);

                var statusText = CreateText(row.transform, status, 14);
                statusText.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 30);
            }
        }

        private void RefreshPersonnelPanel()
        {
            // Detailed personnel management
            RefreshCharacters();
        }
        
        private void RefreshBuildingPanel()
        {
            if (buildingPanel == null || gameManager == null) return;
            
            // Clear existing
            foreach (Transform child in buildingPanel.transform)
            {
                Destroy(child.gameObject);
            }
            
            var layout = buildingPanel.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = buildingPanel.AddComponent<VerticalLayoutGroup>();
            }
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 10;
            layout.padding = new RectOffset(20, 20, 20, 20);
            
            // Title
            var title = CreateText(buildingPanel.transform, "建筑建造", 28, FontStyles.Bold);
            
            // Get available buildings
            var buildings = gameManager.buildingSystem.GetAvailableBuildings();
            foreach (var building in buildings)
            {
                var buildingRow = CreateUIObject("Building_" + building.id, buildingPanel.transform);
                var rowLayout = buildingRow.AddComponent<HorizontalLayoutGroup>();
                rowLayout.childAlignment = TextAnchor.MiddleLeft;
                rowLayout.spacing = 10;
                rowLayout.padding = new RectOffset(10, 10, 10, 10);
                
                var bg = buildingRow.AddComponent<Image>();
                bg.color = new Color(0.2f, 0.2f, 0.25f, 0.8f);
                
                    var infoText = CreateText(buildingRow.transform, $"{building.name} (Lv.1)", 20);
                    infoText.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 40);
                    
                    var costText = CreateText(buildingRow.transform, $"消耗: {building.GetCostString()}", 16);
                    costText.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 40);
                    
                    var countText = CreateText(buildingRow.transform,
                        building.maxCount > 0 ? $"数量上限: {gameManager.buildingSystem.GetBuildingsByDefinition(building.id).Length}/{building.maxCount}" : "可重复建造",
                        16);
                    countText.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 40);
                    
                    bool canBuild = gameManager.buildingSystem.CanBuild(building.id);
                    var buildBtn = CreateButton(buildingRow.transform, canBuild ? "建造" : "资源不足", () => 
                    {
                        if (canBuild)
                        {
                            gameManager.BuildBuilding(building.id);
                            RefreshBuildingPanel();
                            RefreshResources();
                        }
                    }, 100, 40);
                    buildBtn.interactable = canBuild;
            }
            
            // Existing buildings
            var existingBuildings = gameManager.buildingSystem.GetAllBuildings();
            if (existingBuildings.Length > 0)
            {
                var separator = CreateText(buildingPanel.transform, "已建建筑", 24, FontStyles.Bold);
                
                foreach (var building in existingBuildings)
                {
                    var def = gameManager.buildingSystem.GetBuildingDefinition(building.definitionId);
                    var buildingRow = CreateUIObject("Built_" + building.buildingId, buildingPanel.transform);
                    var rowLayout = buildingRow.AddComponent<HorizontalLayoutGroup>();
                    rowLayout.childAlignment = TextAnchor.MiddleLeft;
                    rowLayout.spacing = 10;
                    rowLayout.padding = new RectOffset(10, 10, 10, 10);
                    
                    var bg = buildingRow.AddComponent<Image>();
                    bg.color = new Color(0.15f, 0.2f, 0.25f, 0.8f);
                    
                    var infoText = CreateText(buildingRow.transform, $"{def?.name ?? building.definitionId} (Lv.{building.level})", 20);
                    infoText.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 40);
                    
                    var workerText = CreateText(buildingRow.transform,
                        $"工人: {building.assignedWorkers?.Length ?? 0}/{def?.workerSlots ?? 0}", 16);
                    workerText.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 40);
                    
                    var statusText = CreateText(buildingRow.transform, GetBuildingStatusText(building), 16);
                    statusText.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 40);
                    
                    var productionText = CreateText(buildingRow.transform, $"昨日产出: {building.currentProduction}", 16);
                    productionText.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 40);
                    
                    var upgradeBtn = CreateButton(buildingRow.transform, "升级", () => 
                    {
                        gameManager.UpgradeBuilding(building.buildingId);
                        RefreshBuildingPanel();
                        RefreshResources();
                    }, 100, 40);
                    upgradeBtn.interactable = gameManager.buildingSystem.CanUpgrade(building.buildingId);
                }
            }
        }
        
        /// <summary>
        /// Status is derived from BuildingSystem, the UI does not evaluate upkeep or power itself.
        /// </summary>
        private string GetBuildingStatusText(BuildingState building)
        {
            if (!building.enabled) return "已停用";
            if (building.durability <= 0) return "已损坏";
            
            if (!gameManager.buildingSystem.CanOperate(building))
            {
                var missing = new System.Collections.Generic.List<string>();
                var cost = gameManager.buildingSystem.CalculateOperationCost(building);
                foreach (var kvp in cost)
                {
                    if (!gameManager.resourceSystem.CanAfford(kvp.Key, kvp.Value))
                    {
                        missing.Add($"{kvp.Key.GetDisplayName()}({gameManager.resourceSystem.GetAmount(kvp.Key)}/{kvp.Value})");
                    }
                }
                
                return missing.Count > 0 ? $"维护不足: {string.Join(",", missing)}" : "维护不足";
            }
            
            return "运行中";
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
                // DayEnd is included: the only way out of the settlement state is to
                // roll over to the next Morning, so the button must stay usable there.
                nextTimeSlotButton.interactable = gameManager.GetCurrentGameplayState() == GameplayState.Morning ||
                                                  gameManager.GetCurrentGameplayState() == GameplayState.Planning ||
                                                  gameManager.GetCurrentGameplayState() == GameplayState.Action ||
                                                  gameManager.GetCurrentGameplayState() == GameplayState.Evening ||
                                                  gameManager.GetCurrentGameplayState() == GameplayState.Night ||
                                                  gameManager.GetCurrentGameplayState() == GameplayState.DayEnd;
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
            UnityEngine.Debug.Log("OnNewGameClicked called");
            if (gameManager == null)
            {
                UnityEngine.Debug.LogError("Failed to get GameManager.Instance");
                return;
            }
            gameManager.NewGame();
            ShowGame();
            // The state was just created: repaint from it rather than waiting for an event.
            RefreshUI();
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
            else
            {
                // Show message - no saves available
                UnityEngine.Debug.Log("No save files found");
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