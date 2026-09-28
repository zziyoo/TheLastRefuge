using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LastRefuge.UI
{
    public class UISetup : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SetupUI()
        {
            if (FindObjectOfType<UIManager>() != null) return;
            
            // Create Canvas
            var canvasGO = new GameObject("MainCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();
            
            // Create EventSystem if needed
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            
            // Create UIManager
            var uiManagerGO = new GameObject("UIManager");
            uiManagerGO.transform.SetParent(canvasGO.transform);
            var uiManager = uiManagerGO.AddComponent<UIManager>();
            
            // Create Main Menu Panel
            var mainMenuPanel = CreatePanel("MainMenuPanel", canvasGO.transform);
            uiManager.mainMenuPanel = mainMenuPanel;
            SetupMainMenu(mainMenuPanel, uiManager);
            
            // Create Game Panel
            var gamePanel = CreatePanel("GamePanel", canvasGO.transform);
            gamePanel.SetActive(false);
            uiManager.gamePanel = gamePanel;
            SetupGamePanel(gamePanel, uiManager);
            
            // Create sub-panels
            uiManager.personnelPanel = CreateSubPanel("PersonnelPanel", gamePanel.transform);
            uiManager.buildingPanel = CreateSubPanel("BuildingPanel", gamePanel.transform);
            uiManager.resourceDetailPanel = CreateSubPanel("ResourceDetailPanel", gamePanel.transform);
            uiManager.logPanel = CreateSubPanel("LogPanel", gamePanel.transform);
            uiManager.saveLoadGamePanel = CreateSubPanel("SaveLoadGamePanel", gamePanel.transform);
            
            // Add DebugCommands
            canvasGO.AddComponent<LastRefuge.Debug.DebugCommands>();
            canvasGO.AddComponent<LastRefuge.Core.GameBootstrap>();
        }
        
        private static GameObject CreatePanel(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.95f);
            return go;
        }
        
        private static GameObject CreateSubPanel(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.9f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 0.98f);
            go.SetActive(false);
            return go;
        }
        
        private static void SetupMainMenu(GameObject panel, UIManager uiManager)
        {
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 20;
            layout.padding = new RectOffset(50, 50, 50, 50);
            
            CreateText(panel.transform, "最后避难所", 48, FontStyles.Bold);
            
            CreateButton(panel.transform, "新游戏", () => uiManager.OnNewGameClicked(), 200, 60);
            CreateButton(panel.transform, "继续游戏", () => uiManager.OnContinueClicked(), 200, 60);
            CreateButton(panel.transform, "读取存档", () => uiManager.OnLoadGameClicked(), 200, 60);
            CreateButton(panel.transform, "设置", () => uiManager.OnSettingsClicked(), 200, 60);
            CreateButton(panel.transform, "退出", () => uiManager.OnQuitClicked(), 200, 60);
        }
        
        private static void SetupGamePanel(GameObject panel, UIManager uiManager)
        {
            var rt = panel.GetComponent<RectTransform>();
            
            // Top bar - Day/Time
            var topBar = CreateUIObject("TopBar", panel.transform);
            var topBarRT = topBar.GetComponent<RectTransform>();
            topBarRT.anchorMin = new Vector2(0, 1);
            topBarRT.anchorMax = new Vector2(1, 1);
            topBarRT.pivot = new Vector2(0.5f, 1);
            topBarRT.sizeDelta = new Vector2(0, 80);
            topBarRT.anchoredPosition = Vector2.zero;
            
            var topLayout = topBar.AddComponent<HorizontalLayoutGroup>();
            topLayout.childAlignment = TextAnchor.MiddleCenter;
            topLayout.spacing = 20;
            topLayout.padding = new RectOffset(20, 20, 10, 10);
            
            uiManager.dayTimeText = CreateText(topBar.transform, "第 1 天 清晨", 28, FontStyles.Bold).GetComponent<TextMeshProUGUI>();
            uiManager.weatherText = CreateText(topBar.transform, "☀ 晴天", 24).GetComponent<TextMeshProUGUI>();
            
            // Resources panel (left side)
            var resourcePanel = CreateUIObject("ResourcePanel", panel.transform);
            var resRT = resourcePanel.GetComponent<RectTransform>();
            resRT.anchorMin = new Vector2(0, 0);
            resRT.anchorMax = new Vector2(0.35f, 0.7f);
            resRT.pivot = new Vector2(0, 0);
            resRT.anchoredPosition = new Vector2(10, 100);
            resRT.sizeDelta = new Vector2(-10, -100);
            
            var resLayout = resourcePanel.AddComponent<VerticalLayoutGroup>();
            resLayout.childAlignment = TextAnchor.UpperLeft;
            resLayout.spacing = 5;
            resLayout.padding = new RectOffset(10, 10, 10, 10);
            
            var resTitle = CreateText(resourcePanel.transform, "资源", 24, FontStyles.Bold);
            uiManager.resourceContainer = CreateUIObject("ResourceContainer", resourcePanel.transform).transform;
            uiManager.resourceContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 300);
            
            // Characters panel (middle)
            var charPanel = CreateUIObject("CharacterPanel", panel.transform);
            var charRT = charPanel.GetComponent<RectTransform>();
            charRT.anchorMin = new Vector2(0.35f, 0);
            charRT.anchorMax = new Vector2(0.7f, 0.7f);
            charRT.pivot = new Vector2(0, 0);
            charRT.anchoredPosition = new Vector2(10, 100);
            charRT.sizeDelta = new Vector2(-10, -100);
            
            var charLayout = charPanel.AddComponent<VerticalLayoutGroup>();
            charLayout.childAlignment = TextAnchor.UpperLeft;
            charLayout.spacing = 5;
            charLayout.padding = new RectOffset(10, 10, 10, 10);
            
            CreateText(charPanel.transform, "人员", 24, FontStyles.Bold);
            uiManager.characterContainer = CreateUIObject("CharacterContainer", charPanel.transform).transform;
            uiManager.characterContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 300);
            
            // Right side - could be used for building preview or details
            var detailPanel = CreateUIObject("DetailPanel", panel.transform);
            var detRT = detailPanel.GetComponent<RectTransform>();
            detRT.anchorMin = new Vector2(0.7f, 0);
            detRT.anchorMax = new Vector2(1, 0.7f);
            detRT.pivot = new Vector2(1, 0);
            detRT.anchoredPosition = new Vector2(-10, 100);
            detRT.sizeDelta = new Vector2(-10, -100);
            
            var detLayout = detailPanel.AddComponent<VerticalLayoutGroup>();
            detLayout.childAlignment = TextAnchor.UpperLeft;
            detLayout.spacing = 5;
            detLayout.padding = new RectOffset(10, 10, 10, 10);
            
            CreateText(detailPanel.transform, "详情 / 建筑", 24, FontStyles.Bold);
            
            // Bottom bar - Action buttons
            var bottomBar = CreateUIObject("BottomBar", panel.transform);
            var botRT = bottomBar.GetComponent<RectTransform>();
            botRT.anchorMin = new Vector2(0, 0);
            botRT.anchorMax = new Vector2(1, 0);
            botRT.pivot = new Vector2(0.5f, 0);
            botRT.sizeDelta = new Vector2(0, 120);
            botRT.anchoredPosition = Vector2.zero;
            
            var botLayout = bottomBar.AddComponent<HorizontalLayoutGroup>();
            botLayout.childAlignment = TextAnchor.MiddleCenter;
            botLayout.spacing = 10;
            botLayout.padding = new RectOffset(20, 20, 10, 10);
            
            uiManager.personnelButton = CreateButton(bottomBar.transform, "人员", () => uiManager.TogglePanel(uiManager.personnelPanel), 100, 50);
            uiManager.buildingButton = CreateButton(bottomBar.transform, "建筑", () => uiManager.TogglePanel(uiManager.buildingPanel), 100, 50);
            uiManager.resourceButton = CreateButton(bottomBar.transform, "资源", () => uiManager.TogglePanel(uiManager.resourceDetailPanel), 100, 50);
            uiManager.logButton = CreateButton(bottomBar.transform, "日志", () => uiManager.TogglePanel(uiManager.logPanel), 100, 50);
            uiManager.nextTimeSlotButton = CreateButton(bottomBar.transform, "下一阶段", () => uiManager.OnNextTimeSlotClicked(), 140, 60);
            uiManager.nextTimeSlotButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 24;
            uiManager.saveButton = CreateButton(bottomBar.transform, "存档", () => uiManager.TogglePanel(uiManager.saveLoadGamePanel), 100, 50);
            uiManager.menuButton = CreateButton(bottomBar.transform, "菜单", () => uiManager.OnMenuClicked(), 100, 50);
        }
        
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
            var go = new GameObject("Btn_" + text);
            go.transform.SetParent(parent, false);
            
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            
            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.3f, 0.4f, 0.9f);
            
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());
            
            var tmp = CreateText(go.transform, text, 20, FontStyles.Bold);
            tmp.GetComponent<RectTransform>().sizeDelta = new Vector2(width - 20, height - 10);
            
            return button;
        }
    }
}