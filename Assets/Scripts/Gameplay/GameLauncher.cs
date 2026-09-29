using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using LastRefuge.Gameplay;

namespace LastRefuge.Gameplay
{
    public class GameLauncher : MonoBehaviour
    {
        [Header("Auto Start (Debug Only)")]
        public bool autoStartNewGame = false;
        public string fixedSeed = "";
        
        private bool _hasInitialized = false;
        
        private void Start()
        {
            UnityEngine.Debug.Log("GameLauncher.Start() called");
            if (_hasInitialized) return;
            _hasInitialized = true;
            
            // The UI is created once by UISetup at play mode entry. When the scene is
            // (re)loaded the canvas can take a few frames to materialize again, so retry
            // instead of failing on the first frame.
            StartCoroutine(ShowUiWhenReady());
        }
        
        private IEnumerator ShowUiWhenReady()
        {
            const int maxAttempts = 180;
            int attempts = 0;
            object uiManager = null;
            System.Type uiManagerType = null;
            
            while (attempts < maxAttempts)
            {
                uiManagerType = System.Type.GetType("LastRefuge.UI.UIManager, LastRefuge.UI");
                if (uiManagerType != null)
                {
                    var instanceProperty = uiManagerType.GetProperty("Instance");
                    if (instanceProperty != null)
                    {
                        uiManager = instanceProperty.GetValue(null);
                        // UnityEngine.Object cast: a destroyed Instance must compare as null.
                        if (uiManager != null && (UnityEngine.Object)uiManager != null)
                        {
                            break;
                        }
                    }
                }
                attempts++;
                yield return null;
            }
            
            if (uiManagerType == null)
            {
                UnityEngine.Debug.LogError("UIManager type not found! UI assembly may not be loaded.");
                yield break;
            }
            if (uiManager == null || (UnityEngine.Object)uiManager == null)
            {
                UnityEngine.Debug.LogError("UIManager.Instance is still null after waiting; UI may not be initialized properly.");
                yield break;
            }
            
            UnityEngine.Debug.Log("GameLauncher found UIManager.Instance");
            
            // The game must be fully wired before anything is shown: GameState plus
            // Resource / Character / Building systems all come from GameManager.
            var gameManager = GameManager.GetOrCreate();
            if (gameManager == null)
            {
                UnityEngine.Debug.LogError("GameLauncher: could not obtain a GameManager instance.");
                yield break;
            }
            gameManager.EnsureCoreSystems();

            // By default, show main menu. Auto-start only for debug.
            if (autoStartNewGame)
            {
                gameManager.NewGame(string.IsNullOrEmpty(fixedSeed) ? null : fixedSeed);

                var showGameMethod = uiManagerType.GetMethod("ShowGame");
                showGameMethod?.Invoke(uiManager, null);
                uiManagerType.GetMethod("RefreshUI")?.Invoke(uiManager, null);
            }
            else
            {
                // Show main menu by default
                var showMainMenuMethod = uiManagerType.GetMethod("ShowMainMenu");
                if (showMainMenuMethod != null)
                {
                    UnityEngine.Debug.Log("GameLauncher calling UIManager.ShowMainMenu() via reflection");
                    showMainMenuMethod.Invoke(uiManager, null);
                }
                else
                {
                    UnityEngine.Debug.LogError("UIManager.ShowMainMenu method not found!");
                }
            }
        }
    }
}