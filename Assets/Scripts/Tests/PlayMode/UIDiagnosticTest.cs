using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TMPro;
using LastRefuge.Gameplay;
using LastRefuge.Save;

namespace LastRefuge.Tests
{
    public class UIDiagnosticTest
    {
        private const string SceneName = "Main";

        [UnityTest]
        public IEnumerator DumpUiAfterNewGame()
        {
            string dir = Path.Combine(Application.persistentDataPath, "UIDiag_Saves");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            SaveSystem.OverrideSaveDirectory = dir;

            if (GameManager.Instance != null)
            {
                UnityEngine.Object.Destroy(GameManager.Instance.gameObject);
            }
            yield return null;

            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var uiType = Type.GetType("LastRefuge.UI.UIManager, LastRefuge.UI");
            var uiSetupType = Type.GetType("LastRefuge.UI.UISetup, LastRefuge.UI");
            var existing = uiType?.GetProperty("Instance")?.GetValue(null);
            bool alive = existing != null && (UnityEngine.Object)existing != null;
            if (!alive || GameObject.Find("MainCanvas") == null)
            {
                uiSetupType.GetMethod("SetupUI", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            }
            yield return null;
            yield return null;

            var uiManager = uiType.GetProperty("Instance").GetValue(null);
            Assert.IsNotNull(uiManager, "UIManager.Instance");

            var gm = GameManager.Instance;
            Assert.IsNotNull(gm, "GameManager.Instance");

            uiType.GetMethod("OnNewGameClicked").Invoke(uiManager, null);
            yield return null;
            yield return null;

            var canvas = GameObject.Find("MainCanvas");
            var sb = new StringBuilder();
            sb.AppendLine("===== UI DUMP =====");
            if (canvas != null)
            {
                Dump(canvas.transform, "", sb);
            }
            else
            {
                sb.AppendLine("MainCanvas NOT FOUND");
            }
            UnityEngine.Debug.Log(sb.ToString());

            int food = gm.resourceSystem.GetAmount(LastRefuge.Data.ResourceType.Food);
            int wood = gm.resourceSystem.GetAmount(LastRefuge.Data.ResourceType.Wood);
            int water = gm.resourceSystem.GetAmount(LastRefuge.Data.ResourceType.Water);
            int chars = gm.characterSystem.GetAliveCharacters().Length;
            int builds = gm.buildingSystem.GetAllBuildings().Length;
            UnityEngine.Debug.Log($"===== STATE ===== food={food} wood={wood} water={water} chars={chars} buildings={builds}");

            // --- Binding chain report: does the UI actually receive the data? ---
            var mono = (MonoBehaviour)uiManager;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var resContainer = uiType.GetField("resourceContainer", flags)?.GetValue(uiManager) as Transform;
            var charContainer = uiType.GetField("characterContainer", flags)?.GetValue(uiManager) as Transform;
            var resPrefab = uiType.GetField("resourceItemPrefab", flags)?.GetValue(uiManager) as GameObject;
            var charPrefab = uiType.GetField("characterItemPrefab", flags)?.GetValue(uiManager) as GameObject;
            var gamePanel = uiType.GetField("gamePanel", flags)?.GetValue(uiManager) as GameObject;
            var mainMenuPanel = uiType.GetField("mainMenuPanel", flags)?.GetValue(uiManager) as GameObject;
            var cachedGm = uiType.GetField("_gameManager", flags)?.GetValue(uiManager);

            sb.Length = 0;
            sb.AppendLine("===== BINDING =====");
            sb.AppendLine($"mainMenuPanel={(mainMenuPanel == null ? "NULL" : mainMenuPanel.activeSelf ? "active" : "inactive")}");
            sb.AppendLine($"gamePanel={(gamePanel == null ? "NULL" : gamePanel.activeSelf ? "active" : "inactive")}");
            sb.AppendLine($"resourceContainer={(resContainer == null ? "NULL" : resContainer.name)} children={(resContainer == null ? -1 : resContainer.childCount)}");
            sb.AppendLine($"characterContainer={(charContainer == null ? "NULL" : charContainer.name)} children={(charContainer == null ? -1 : charContainer.childCount)}");
            sb.AppendLine($"resourceItemPrefab={(resPrefab == null ? "NULL" : resPrefab.name + " active=" + resPrefab.activeSelf)}");
            sb.AppendLine($"characterItemPrefab={(charPrefab == null ? "NULL" : charPrefab.name + " active=" + charPrefab.activeSelf)}");
            sb.AppendLine($"UIManager._gameManager={(cachedGm == null ? "NULL" : "set")}");
            if (resContainer != null)
            {
                var rt = resContainer.GetComponent<RectTransform>();
                sb.AppendLine($"resourceContainer rect w={rt.rect.width:F0} h={rt.rect.height:F0}");
                foreach (Transform c in resContainer)
                {
                    var itemRt = c.GetComponent<RectTransform>();
                    var texts = c.GetComponentsInChildren<TextMeshProUGUI>(true);
                    string joined = "";
                    foreach (var t in texts) joined += "[" + t.text + "]";
                    sb.AppendLine($"  item {c.name} active={c.gameObject.activeSelf} h={itemRt.rect.height:F0} w={itemRt.rect.width:F0} pos=({itemRt.anchoredPosition.x:F0},{itemRt.anchoredPosition.y:F0}) {joined}");
                }
            }
            UnityEngine.Debug.Log(sb.ToString());

            yield return null;
        }

        private static void Dump(Transform t, string indent, StringBuilder sb)
        {
            var rt = t.GetComponent<RectTransform>();
            string size = rt != null ? $"w={rt.rect.width:F0} h={rt.rect.height:F0}" : "no-rt";
            var tmp = t.GetComponent<TextMeshProUGUI>();
            string txt = tmp != null ? $" \"{tmp.text}\"" : "";
            sb.AppendLine($"{indent}{t.name} [{size}]{(t.gameObject.activeInHierarchy ? "" : " (inactive)")}{txt}");
            for (int i = 0; i < t.childCount; i++)
            {
                Dump(t.GetChild(i), indent + "  ", sb);
            }
        }
    }
}
