using UnityEngine;
using UnityEngine.SceneManagement;
using LastRefuge.Gameplay;

namespace LastRefuge.Gameplay
{
    public class GameLauncher : MonoBehaviour
    {
        [Header("Auto Start")]
        public bool autoStartNewGame = true;
        public string fixedSeed = "";
        
        private void Start()
        {
            StartCoroutine(InitializeGame());
        }
        
        private System.Collections.IEnumerator InitializeGame()
        {
            yield return null;
            
            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                UnityEngine.Debug.LogError("GameManager not found!");
                yield break;
            }
            
            if (autoStartNewGame)
            {
                gameManager.NewGame(string.IsNullOrEmpty(fixedSeed) ? null : fixedSeed);
            }
        }
    }
}