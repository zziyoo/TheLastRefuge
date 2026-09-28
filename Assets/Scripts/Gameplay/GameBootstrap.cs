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
        }
    }
}