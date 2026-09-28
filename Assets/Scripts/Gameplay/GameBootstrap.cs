using UnityEngine;
using UnityEngine.SceneManagement;
using LastRefuge.Core;
using LastRefuge.Gameplay;

namespace LastRefuge.Gameplay
{
    public class GameBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
        
        private void Awake()
        {
            // Ensure GameManager exists
            if (GameManager.Instance == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();
                DontDestroyOnLoad(go);
            }
            
            // Ensure UIManager exists
            var uiManagerType = System.Type.GetType("LastRefuge.UI.UIManager, LastRefuge.UI");
            if (uiManagerType != null)
            {
                var instanceProperty = uiManagerType.GetProperty("Instance");
                if (instanceProperty != null)
                {
                    var instance = instanceProperty.GetValue(null);
                    if (instance == null)
                    {
                        var go = new GameObject("UIManager");
                        go.AddComponent(uiManagerType);
                        DontDestroyOnLoad(go);
                    }
                }
            }
        }
    }
}