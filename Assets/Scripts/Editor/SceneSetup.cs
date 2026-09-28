using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using LastRefuge.Gameplay;

namespace LastRefuge.Editor
{
    public class SceneSetup
    {
        [MenuItem("Tools/Last Refuge/Setup Main Scene")]
        public static void SetupMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            
            // Set up camera for 2D/UI
            var camera = Camera.main;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            
            // Create GameManager
            var gmGO = new GameObject("GameManager");
            gmGO.AddComponent<GameManager>();
            gmGO.AddComponent<GameBootstrap>();
            
            // Save scene
            string scenePath = "Assets/Scenes/Main.unity";
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);
            
            // Set as first scene in build settings
            var buildScenes = EditorBuildSettings.scenes;
            System.Array.Resize(ref buildScenes, buildScenes.Length + 1);
            buildScenes[buildScenes.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = buildScenes;
            
            UnityEngine.Debug.Log("Main scene created at: " + scenePath);
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