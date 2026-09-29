using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LastRefuge.Gameplay;
using LastRefuge.Data;
using System.Collections.Generic;

namespace LastRefuge.UI
{
    public class UISetup : MonoBehaviour
    {
        private static TMP_FontAsset cjkFont;
        private static Sprite buttonSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SetupUI()
        {
            InitializeResources();

            // Check if we already have a fully initialized UI
            var existingCanvas = GameObject.Find("MainCanvas");
            if (existingCanvas != null)
            {
                var existingUIManager = existingCanvas.GetComponentInChildren<UIManager>();
                if (existingUIManager != null && existingUIManager.mainMenuPanel != null && existingUIManager.saveLoadPanel != null && existingUIManager.settingsPanel != null)
                {
                    return; // Already fully initialized
                }
            }
            
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
            var uiMgr = uiManagerGO.AddComponent<UIManager>();
            
            // Create Main Menu Panel
            var mainMenuPanel = CreatePanel("MainMenuPanel", canvasGO.transform);
            uiMgr.mainMenuPanel = mainMenuPanel;
            SetupMainMenu(mainMenuPanel, uiMgr);
            
            // Create Game Panel
            var gamePanel = CreatePanel("GamePanel", canvasGO.transform);
            gamePanel.SetActive(false);
            uiMgr.gamePanel = gamePanel;
            SetupGamePanel(gamePanel, uiMgr);
            
            // Create SaveLoad Panel (main menu level)
            var saveLoadPanel = CreatePanel("SaveLoadPanel", canvasGO.transform);
            saveLoadPanel.SetActive(false);
            uiMgr.saveLoadPanel = saveLoadPanel;
            SetupSaveLoadPanel(saveLoadPanel, uiMgr);
            
            // Create Settings Panel (main menu level)
            var settingsPanel = CreatePanel("SettingsPanel", canvasGO.transform);
            settingsPanel.SetActive(false);
            uiMgr.settingsPanel = settingsPanel;
            SetupSettingsPanel(settingsPanel, uiMgr);
            
            // Create sub-panels (game panel level)
            uiMgr.personnelPanel = CreateSubPanel("PersonnelPanel", gamePanel.transform);
            uiMgr.buildingPanel = CreateSubPanel("BuildingPanel", gamePanel.transform);
            uiMgr.resourceDetailPanel = CreateSubPanel("ResourceDetailPanel", gamePanel.transform);
            uiMgr.logPanel = CreateSubPanel("LogPanel", gamePanel.transform);
            uiMgr.saveLoadGamePanel = CreateSubPanel("SaveLoadGamePanel", gamePanel.transform);

            // Create UI prefabs for resource and character items
            CreateResourceItemPrefab(uiMgr);
            CreateCharacterItemPrefab(uiMgr);
        }

        private static void InitializeResources()
        {
            // Load CJK font from TMP Settings
            cjkFont = TMP_Settings.defaultFontAsset;
            if (cjkFont == null)
            {
                cjkFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            // Create a simple button background sprite (procedural)
            buttonSprite = CreateButtonSprite();
        }

        private static Sprite CreateButtonSprite()
        {
            // Create a simple 16x16 texture for button background
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
            
            return Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
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
            // Must be explicit: with control disabled the layout group sizes children
            // from their own sizeDelta, so the viewport stays 0 and the list is clipped.
            resLayout.childControlWidth = true;
            resLayout.childControlHeight = true;
            resLayout.childForceExpandWidth = true;
            resLayout.childForceExpandHeight = false;
            
            var resTitle = CreateText(resourcePanel.transform, "资源", 24, FontStyles.Bold);
            
            // Add ScrollRect for resource list
            var resScrollRect = resourcePanel.AddComponent<ScrollRect>();
            var resViewport = CreateUIObject("Viewport", resourcePanel.transform);
            var vpRT = resViewport.GetComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = Vector2.zero;
            vpRT.offsetMax = Vector2.zero;
            var vpMask = resViewport.AddComponent<UnityEngine.UI.Mask>();
            vpMask.showMaskGraphic = false;
            var vpImage = resViewport.AddComponent<Image>();
            vpImage.color = Color.clear;

            // The panel's VerticalLayoutGroup drives child heights. Without a flexible
            // height the viewport is sized to its preferred height (0) and the whole
            // resource list is clipped away, leaving the panel looking empty.
            var vpLayoutElement = resViewport.AddComponent<LayoutElement>();
            vpLayoutElement.minHeight = 0f;
            vpLayoutElement.preferredHeight = 0f;
            vpLayoutElement.flexibleHeight = 1f;
            
            var resContent = CreateUIObject("ResourceContainer", resViewport.transform);
            uiManager.resourceContainer = resContent.transform;
            var contentRT = resContent.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.sizeDelta = new Vector2(0, 300);
            contentRT.anchoredPosition = Vector2.zero;
            
            var resContentLayout = resContent.AddComponent<VerticalLayoutGroup>();
            resContentLayout.childAlignment = TextAnchor.UpperLeft;
            resContentLayout.spacing = 5;
            resContentLayout.padding = new RectOffset(10, 10, 10, 10);
            resContentLayout.childControlWidth = true;
            resContentLayout.childControlHeight = false;
            resContentLayout.childForceExpandWidth = true;
            resContentLayout.childForceExpandHeight = false;
            
            var contentSizeFitter = resContent.AddComponent<ContentSizeFitter>();
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            resScrollRect.viewport = resViewport.GetComponent<RectTransform>();
            resScrollRect.content = contentRT;
            resScrollRect.vertical = true;
            resScrollRect.horizontal = false;
            resScrollRect.movementType = ScrollRect.MovementType.Clamped;
            
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
            // Same reason as the resource column: without control the viewport collapses.
            charLayout.childControlWidth = true;
            charLayout.childControlHeight = true;
            charLayout.childForceExpandWidth = true;
            charLayout.childForceExpandHeight = false;
            
            CreateText(charPanel.transform, "人员", 24, FontStyles.Bold);
            
            // Add ScrollRect for character list
            var charScrollRect = charPanel.AddComponent<ScrollRect>();
            var charViewport = CreateUIObject("Viewport", charPanel.transform);
            var charVpRT = charViewport.GetComponent<RectTransform>();
            charVpRT.anchorMin = Vector2.zero;
            charVpRT.anchorMax = Vector2.one;
            charVpRT.offsetMin = Vector2.zero;
            charVpRT.offsetMax = Vector2.zero;
            var charVpMask = charViewport.AddComponent<UnityEngine.UI.Mask>();
            charVpMask.showMaskGraphic = false;
            var charVpImage = charViewport.AddComponent<Image>();
            charVpImage.color = Color.clear;

            // Same reason as the resource viewport: without a flexible height the
            // parent VerticalLayoutGroup collapses it and the survivor list disappears.
            var charVpLayoutElement = charViewport.AddComponent<LayoutElement>();
            charVpLayoutElement.minHeight = 0f;
            charVpLayoutElement.preferredHeight = 0f;
            charVpLayoutElement.flexibleHeight = 1f;
            
            var charContent = CreateUIObject("CharacterContainer", charViewport.transform);
            uiManager.characterContainer = charContent.transform;
            var charContentRT = charContent.GetComponent<RectTransform>();
            charContentRT.anchorMin = new Vector2(0, 1);
            charContentRT.anchorMax = new Vector2(1, 1);
            charContentRT.pivot = new Vector2(0.5f, 1);
            charContentRT.sizeDelta = new Vector2(0, 300);
            charContentRT.anchoredPosition = Vector2.zero;
            
            var charContentLayout = charContent.AddComponent<VerticalLayoutGroup>();
            charContentLayout.childAlignment = TextAnchor.UpperLeft;
            charContentLayout.spacing = 5;
            charContentLayout.padding = new RectOffset(10, 10, 10, 10);
            charContentLayout.childControlWidth = true;
            charContentLayout.childControlHeight = false;
            charContentLayout.childForceExpandWidth = true;
            charContentLayout.childForceExpandHeight = false;
            
            var charContentSizeFitter = charContent.AddComponent<ContentSizeFitter>();
            charContentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            charScrollRect.viewport = charViewport.GetComponent<RectTransform>();
            charScrollRect.content = charContentRT;
            charScrollRect.vertical = true;
            charScrollRect.horizontal = false;
            charScrollRect.movementType = ScrollRect.MovementType.Clamped;
            
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
            // Same reason as the other two columns.
            detLayout.childControlWidth = true;
            detLayout.childControlHeight = true;
            detLayout.childForceExpandWidth = true;
            detLayout.childForceExpandHeight = false;
            
            CreateText(detailPanel.transform, "详情 / 建筑", 24, FontStyles.Bold);

            // The detail panel is the "building" column: it lists the shelters and
            // facilities the colony actually owns. Same scroll pattern as the other two
            // columns so the content is clipped inside the panel.
            var detScrollRect = detailPanel.AddComponent<ScrollRect>();
            var detViewport = CreateUIObject("Viewport", detailPanel.transform);
            var detVpRT = detViewport.GetComponent<RectTransform>();
            detVpRT.anchorMin = Vector2.zero;
            detVpRT.anchorMax = Vector2.one;
            detVpRT.offsetMin = Vector2.zero;
            detVpRT.offsetMax = Vector2.zero;
            var detVpMask = detViewport.AddComponent<UnityEngine.UI.Mask>();
            detVpMask.showMaskGraphic = false;
            var detVpImage = detViewport.AddComponent<Image>();
            detVpImage.color = Color.clear;

            var detVpLayoutElement = detViewport.AddComponent<LayoutElement>();
            detVpLayoutElement.minHeight = 0f;
            detVpLayoutElement.preferredHeight = 0f;
            detVpLayoutElement.flexibleHeight = 1f;

            var detContent = CreateUIObject("DetailContainer", detViewport.transform);
            uiManager.detailContainer = detContent.transform;
            var detContentRT = detContent.GetComponent<RectTransform>();
            detContentRT.anchorMin = new Vector2(0, 1);
            detContentRT.anchorMax = new Vector2(1, 1);
            detContentRT.pivot = new Vector2(0.5f, 1);
            detContentRT.sizeDelta = new Vector2(0, 300);
            detContentRT.anchoredPosition = Vector2.zero;

            var detContentLayout = detContent.AddComponent<VerticalLayoutGroup>();
            detContentLayout.childAlignment = TextAnchor.UpperLeft;
            detContentLayout.spacing = 5;
            detContentLayout.padding = new RectOffset(10, 10, 10, 10);
            detContentLayout.childControlWidth = true;
            detContentLayout.childControlHeight = false;
            detContentLayout.childForceExpandWidth = true;
            detContentLayout.childForceExpandHeight = false;

            var detContentSizeFitter = detContent.AddComponent<ContentSizeFitter>();
            detContentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            detScrollRect.viewport = detVpRT;
            detScrollRect.content = detContentRT;
            detScrollRect.vertical = true;
            detScrollRect.horizontal = false;
            detScrollRect.movementType = ScrollRect.MovementType.Clamped;
            
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
            if (cjkFont != null)
            {
                tmp.font = cjkFont;
            }
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

        private static void CreateResourceItemPrefab(UIManager uiManager)
        {
            var go = new GameObject("ResourceItemPrefab");
            go.transform.SetParent(uiManager.transform);
            // Keep the template hidden: it must never render as a stray row. UIManager
            // activates every instance it clones.
            go.SetActive(false);
            
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 40);
            
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 10;
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            
            var nameText = CreateText(go.transform, "Resource", 18);
            nameText.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 30);
            
            var amountText = CreateText(go.transform, "0 / 0", 18);
            amountText.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 30);
            
            var changeText = CreateText(go.transform, "+0/天", 16);
            changeText.GetComponent<RectTransform>().sizeDelta = new Vector2(70, 30);
            
            var daysText = CreateText(go.transform, "∞", 16);
            daysText.GetComponent<RectTransform>().sizeDelta = new Vector2(50, 30);
            
            // Capacity slider
            var sliderObj = CreateUIObject("CapacitySlider", go.transform);
            var sliderRT = sliderObj.GetComponent<RectTransform>();
            sliderRT.sizeDelta = new Vector2(100, 20);
            var slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.value = 0;
            var sliderHandle = CreateUIObject("Handle", sliderObj.transform);
            var handleRT = sliderHandle.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(20, 20);
            var handleImage = sliderHandle.AddComponent<Image>();
            handleImage.color = Color.white;
            slider.handleRect = handleRT;
            slider.targetGraphic = handleImage;
            
            var fillObj = CreateUIObject("Fill", sliderObj.transform);
            var fillRT = fillObj.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            var fillImage = fillObj.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.6f, 1f, 0.8f);
            slider.fillRect = fillRT;
            
            var bgObj = CreateUIObject("Background", sliderObj.transform);
            var bgRT = bgObj.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            
            var itemUI = go.AddComponent<ResourceItemUI>();
            itemUI.nameText = nameText.GetComponent<TextMeshProUGUI>();
            itemUI.amountText = amountText.GetComponent<TextMeshProUGUI>();
            itemUI.changeText = changeText.GetComponent<TextMeshProUGUI>();
            itemUI.daysRemainingText = daysText.GetComponent<TextMeshProUGUI>();
            itemUI.capacitySlider = slider;
            
            uiManager.resourceItemPrefab = go;
        }

private static void CreateCharacterItemPrefab(UIManager uiManager)
        {
            var go = new GameObject("CharacterItemPrefab");
            go.transform.SetParent(uiManager.transform);
            // Keep the template hidden: it must never render as a stray row. UIManager
            // activates every instance it clones.
            go.SetActive(false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 120);
            
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 3;
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            
            // Name row
            var nameRow = CreateUIObject("NameRow", go.transform);
            var nameRowLayout = nameRow.AddComponent<HorizontalLayoutGroup>();
            nameRowLayout.childAlignment = TextAnchor.MiddleLeft;
            nameRowLayout.spacing = 10;
            nameRowLayout.childControlWidth = true;
            nameRowLayout.childControlHeight = false;
            nameRowLayout.childForceExpandWidth = false;
            nameRowLayout.childForceExpandHeight = false;
            
            var nameText = CreateText(nameRow.transform, "Name (Profession)", 18, FontStyles.Bold);
            nameText.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 25);
            
            // Status row with health/hunger/stress/fatigue bars
            var statusRow = CreateUIObject("StatusRow", go.transform);
            var statusRowLayout = statusRow.AddComponent<HorizontalLayoutGroup>();
            statusRowLayout.childAlignment = TextAnchor.MiddleLeft;
            statusRowLayout.spacing = 10;
            statusRowLayout.childControlWidth = true;
            statusRowLayout.childControlHeight = false;
            statusRowLayout.childForceExpandWidth = false;
            statusRowLayout.childForceExpandHeight = false;
            
            var statusText = CreateText(statusRow.transform, "HP:100 饥饿:0 压力:0 疲劳:0", 14);
            statusText.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 20);
            
            // Status bars: one Image for the background and one for the fill.
            // A GameObject can only hold a single Graphic, so the fill is a child object.
            var healthBar = CreateStatusBar(statusRow.transform, "HealthBar", Color.green, 1f);
            var hungerBar = CreateStatusBar(statusRow.transform, "HungerBar", Color.green, 0f);
            var stressBar = CreateStatusBar(statusRow.transform, "StressBar", Color.green, 0f);
            var fatigueBar = CreateStatusBar(statusRow.transform, "FatigueBar", Color.green, 0f);
            
            // Work row
            var workRow = CreateUIObject("WorkRow", go.transform);
            var workRowLayout = workRow.AddComponent<HorizontalLayoutGroup>();
            workRowLayout.childAlignment = TextAnchor.MiddleLeft;
            workRowLayout.spacing = 10;
            workRowLayout.childControlWidth = true;
            workRowLayout.childControlHeight = false;
            workRowLayout.childForceExpandWidth = false;
            workRowLayout.childForceExpandHeight = false;
            
            var workText = CreateText(workRow.transform, "工作: 待机", 14);
            workText.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 20);
            
            // Add work selection buttons (4 main types as per Phase 2 requirements)
            var workButtonsContainer = CreateUIObject("WorkButtons", workRow.transform);
            var workLayout = workButtonsContainer.AddComponent<HorizontalLayoutGroup>();
            workLayout.childAlignment = TextAnchor.MiddleLeft;
            workLayout.spacing = 5;
            workLayout.childControlWidth = true;
            workLayout.childControlHeight = false;
            workLayout.childForceExpandWidth = false;
            workLayout.childForceExpandHeight = false;
            
            // Create work type buttons (4 main types as specified)
            var workTypes = new[] { WorkType.Idle, WorkType.Farming, WorkType.Gathering, WorkType.Engineering };
            var workButtons = new List<Button>();
            foreach (var workType in workTypes)
            {
                var btn = CreateButton(workButtonsContainer.transform, workType.GetDisplayName(), null, 70, 30);
                btn.name = "Btn_" + workType;
                btn.onClick.RemoveAllListeners(); // Will be set up in UIManager
                workButtons.Add(btn);
            }
            
            var itemUI = go.AddComponent<CharacterItemUI>();
            itemUI.nameText = nameText.GetComponent<TextMeshProUGUI>();
            itemUI.statusText = statusText.GetComponent<TextMeshProUGUI>();
            itemUI.workText = workText.GetComponent<TextMeshProUGUI>();
            itemUI.healthBar = healthBar;
            itemUI.hungerBar = hungerBar;
            itemUI.stressBar = stressBar;
            itemUI.fatigueBar = fatigueBar;
            itemUI.workButtons = workButtons.ToArray();
            
            uiManager.characterItemPrefab = go;
        }
        
        /// <summary>
        /// Creates a status bar made of a background Image and a filled child Image.
        /// The returned Image is the fill, CharacterItemUI drives it with fillAmount.
        /// </summary>
        private static Image CreateStatusBar(Transform parent, string barName, Color fillColor, float fillAmount)
        {
            var barObj = CreateUIObject(barName, parent);
            barObj.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 16);
            
            var background = barObj.AddComponent<Image>();
            background.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            var fillObj = CreateUIObject("Fill", barObj.transform);
            var fillRT = fillObj.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            
            var fill = fillObj.AddComponent<Image>();
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = fillAmount;
            
            return fill;
        }
        
        private static void SetupSaveLoadPanel(GameObject panel, UIManager uiManager)
        {
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 20;
            layout.padding = new RectOffset(50, 50, 50, 50);
            
            CreateText(panel.transform, "读取存档", 48, FontStyles.Bold);
            
            var backBtn = CreateButton(panel.transform, "返回", () => uiManager.ShowMainMenu(), 200, 60);
        }
        
        private static void SetupSettingsPanel(GameObject panel, UIManager uiManager)
        {
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 20;
            layout.padding = new RectOffset(50, 50, 50, 50);
            
            CreateText(panel.transform, "设置", 48, FontStyles.Bold);
            
            // Audio settings
            var audioGroup = CreateUIObject("AudioSettings", panel.transform);
            var audioLayout = audioGroup.AddComponent<HorizontalLayoutGroup>();
            audioLayout.childAlignment = TextAnchor.MiddleLeft;
            audioLayout.spacing = 20;
            audioLayout.padding = new RectOffset(20, 20, 10, 10);
            
            CreateText(audioGroup.transform, "音乐", 24);
            var musicSlider = CreateSlider(audioGroup.transform, 0, 1, 1, "MusicVolume");
            
            var audioGroup2 = CreateUIObject("AudioSettings2", panel.transform);
            var audioLayout2 = audioGroup2.AddComponent<HorizontalLayoutGroup>();
            audioLayout2.childAlignment = TextAnchor.MiddleLeft;
            audioLayout2.spacing = 20;
            audioLayout2.padding = new RectOffset(20, 20, 10, 10);
            
            CreateText(audioGroup2.transform, "音效", 24);
            var sfxSlider = CreateSlider(audioGroup2.transform, 0, 1, 1, "SFXVolume");
            
            var uiGroup = CreateUIObject("UISettings", panel.transform);
            var uiLayout = uiGroup.AddComponent<HorizontalLayoutGroup>();
            uiLayout.childAlignment = TextAnchor.MiddleLeft;
            uiLayout.spacing = 20;
            uiLayout.padding = new RectOffset(20, 20, 10, 10);
            
            CreateText(uiGroup.transform, "文字速度", 24);
            var speedSlider = CreateSlider(uiGroup.transform, 0.5f, 2f, 1f, "TextSpeed");
            
            var backBtn = CreateButton(panel.transform, "返回", () => uiManager.ShowMainMenu(), 200, 60);
        }
        
        private static Slider CreateSlider(Transform parent, float min, float max, float defaultValue, string name)
        {
            var go = new GameObject("Slider_" + name);
            go.transform.SetParent(parent, false);
            var slider = go.AddComponent<Slider>();
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 30);
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultValue;
            slider.wholeNumbers = false;
            
            // Create slider background
            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgRT = bg.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            // Create fill area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRT = fillArea.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = Vector2.zero;
            fillAreaRT.anchorMax = Vector2.one;
            fillAreaRT.offsetMin = new Vector2(10, 5);
            fillAreaRT.offsetMax = new Vector2(-10, -5);
            var fillImage = fillArea.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.6f, 1f, 0.8f);
            slider.fillRect = fillAreaRT;
            slider.targetGraphic = fillImage;
            
            // Create handle
            var handle = new GameObject("Handle Slide");
            handle.transform.SetParent(fillArea.transform, false);
            var handleRT = handle.AddComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(20, 20);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = Color.white;
            slider.handleRect = handleRT;
            
            return slider;
        }
    }
}