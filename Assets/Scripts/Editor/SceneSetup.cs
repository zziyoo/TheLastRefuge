using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using LastRefuge.Gameplay;

namespace LastRefuge.Editor
{
    public class SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        
        [MenuItem("Tools/Last Refuge/Setup Main Scene")]
        public static void SetupMainScene()
        {
            // Check if scene already exists
            var existingScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Scene scene;
            
            if (existingScene != null)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                UnityEngine.Debug.Log("Main scene already exists, updating...");
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }
            
            // Set up camera for 2D/UI
            var camera = Camera.main;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            
            // Find or create GameManager
            var gmGO = GameObject.Find("GameManager");
            GameManager gm;
            if (gmGO != null)
            {
                gm = gmGO.GetComponent<GameManager>();
                if (gm == null)
                {
                    gm = gmGO.AddComponent<GameManager>();
                    UnityEngine.Debug.Log("Added GameManager component to existing GameManager object");
                }
                else
                {
                    UnityEngine.Debug.Log("Reusing existing GameManager");
                }
            }
            else
            {
                var gmGO2 = new GameObject("GameManager");
                gm = gmGO2.AddComponent<GameManager>();
                UnityEngine.Debug.Log("Created new GameManager");
            }
            
            // Ensure GameBootstrap exists on GameManager
            var bootstrap = gmGO.GetComponent<GameBootstrap>();
            if (bootstrap == null)
            {
                gmGO.AddComponent<GameBootstrap>();
                UnityEngine.Debug.Log("Added GameBootstrap to GameManager");
            }
            else
            {
                UnityEngine.Debug.Log("GameBootstrap already exists on GameManager");
            }
            
            // Find or create GameLauncher
            var launcherGO = GameObject.Find("GameLauncher");
            if (launcherGO != null)
            {
                var launcher = launcherGO.GetComponent<GameLauncher>();
                if (launcher == null)
                {
                    launcherGO.AddComponent<GameLauncher>();
                    UnityEngine.Debug.Log("Added GameLauncher component to existing GameLauncher object");
                }
                else
                {
                    UnityEngine.Debug.Log("Reusing existing GameLauncher");
                }
            }
            else
            {
                var launcherGO2 = new GameObject("GameLauncher");
                launcherGO2.AddComponent<GameLauncher>();
                UnityEngine.Debug.Log("Created new GameLauncher");
            }
            
            // Save scene
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            
            // Set as first scene in build settings
            var buildScenes = EditorBuildSettings.scenes;
            var existingIndex = System.Array.FindIndex(buildScenes, s => s.path == "Assets/Scenes/Main.unity");
            
            if (existingIndex >= 0)
            {
                buildScenes[existingIndex] = new EditorBuildSettingsScene(ScenePath, true);
            }
            else
            {
                System.Array.Resize(ref buildScenes, buildScenes.Length + 1);
                buildScenes[buildScenes.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
            }
            EditorBuildSettings.scenes = buildScenes;
            
            UnityEngine.Debug.Log("Main scene created/updated at: " + ScenePath);
        }
        
        [MenuItem("Tools/Last Refuge/Configure Project Settings")]
        public static void ConfigureProjectSettings()
        {
            // Player Settings
            PlayerSettings.productName = "最后避难所";
            PlayerSettings.companyName = "LastRefuge";
            
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.lastrefuge.game");
            
            // Android Settings
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            
            // Graphics
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android, ApiCompatibilityLevel.NET_Standard_2_0);
            
            // Strip engine code
            PlayerSettings.stripEngineCode = true;
            
            // Quality
            QualitySettings.vSyncCount = 1;
            
            // Physics 2D
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
            
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("Project settings configured for mobile portrait");
        }
    }
}