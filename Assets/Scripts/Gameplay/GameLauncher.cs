using UnityEngine;
using UnityEngine.SceneManagement;
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
            
            var uiManagerType = System.Type.GetType("LastRefuge.UI.UIManager, LastRefuge.UI");
            if (uiManagerType == null)
            {
                UnityEngine.Debug.LogError("UIManager type not found! UI assembly may not be loaded.");
                return;
            }
            
            var instanceProperty = uiManagerType.GetProperty("Instance");
            if (instanceProperty == null)
            {
                UnityEngine.Debug.LogError("UIManager.Instance property not found!");
                return;
            }
            
            var uiManager = instanceProperty.GetValue(null);
            if (uiManager == null)
            {
                UnityEngine.Debug.LogError("UIManager.Instance is null! UI may not be initialized properly.");
                return;
            }
            
            UnityEngine.Debug.Log("GameLauncher found UIManager.Instance");
            
            // By default, show main menu. Auto-start only for debug.
            if (autoStartNewGame)
            {
                var gameManager = GameManager.Instance;
                if (gameManager != null)
                {
                    gameManager.NewGame(string.IsNullOrEmpty(fixedSeed) ? null : fixedSeed);
                }
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