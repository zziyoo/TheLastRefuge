using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;

namespace LastRefuge.Editor
{
    public class CreateValidCJKFontAsset
    {
        [MenuItem("Tools/Last Refuge/Create Valid CJK Font Asset")]
        public static void CreateFontAsset()
        {
            string fontPath = "Assets/Fonts/NotoSansSC-Regular.ttf";
            if (!File.Exists(fontPath))
            {
                EditorUtility.DisplayDialog("Error", "Font file not found at: " + fontPath, "OK");
                return;
            }

            // Load the font as a Unity Font asset
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (sourceFont == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to load font from project.", "OK");
                return;
            }

            string assetPath = "Assets/Fonts/NotoSansSC-Regular.asset";

            // Use the Font Asset Creator API with Dynamic mode (7-argument version for this TMP version)
            // CreateFontAsset(Font sourceFontFile, int samplingPointSize, int atlasWidth, GlyphRenderMode renderMode, int atlasPadding, int characterSetSelection, AtlasPopulationMode populationMode)
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,                    // sampling point size
                2048,                  // atlas width
                GlyphRenderMode.SDFAA,  // render mode
                5,                     // atlas padding
                0,                     // character set selection (0 = Unicode range)
                AtlasPopulationMode.Dynamic
            );

            if (fontAsset != null)
            {
                AssetDatabase.CreateAsset(fontAsset, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                UnityEngine.Debug.Log($"Created CJK Font Asset at: {assetPath}");
                UnityEngine.Debug.Log($"Atlas size: {fontAsset.atlasWidth}x{fontAsset.atlasHeight}");
                UnityEngine.Debug.Log($"Atlas texture count: {fontAsset.atlasTextures?.Length ?? 0}");
                if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0)
                {
                    UnityEngine.Debug.Log($"Atlas texture[0]: {fontAsset.atlasTextures[0]?.name ?? "null"}");
                }
                EditorUtility.DisplayDialog("Success", $"CJK Font Asset created at:\n{assetPath}", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Failed to create TMP Font Asset.", "OK");
            }
        }

        [MenuItem("Tools/Last Refuge/Set CJK Font as Default")]
        public static void SetCJKFontAsDefault()
        {
            string assetPath = "Assets/Fonts/NotoSansSC-Regular.asset";
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);

            if (fontAsset == null)
            {
                EditorUtility.DisplayDialog("Error", "CJK Font Asset not found. Run 'Create Valid CJK Font Asset' first.", "OK");
                return;
            }

            var settings = TMP_Settings.instance;
            if (settings != null)
            {
                var serializedSettings = new SerializedObject(settings);
                var defaultFontProp = serializedSettings.FindProperty("m_defaultFontAsset");
                var fallbackFontsProp = serializedSettings.FindProperty("m_fallbackFontAssets");

                defaultFontProp.objectReferenceValue = fontAsset;

                bool hasFallback = false;
                for (int i = 0; i < fallbackFontsProp.arraySize; i++)
                {
                    if (fallbackFontsProp.GetArrayElementAtIndex(i).objectReferenceValue == fontAsset)
                    {
                        hasFallback = true;
                        break;
                    }
                }

                if (!hasFallback)
                {
                    fallbackFontsProp.InsertArrayElementAtIndex(0);
                    fallbackFontsProp.GetArrayElementAtIndex(0).objectReferenceValue = fontAsset;
                }

                serializedSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();

                UnityEngine.Debug.Log("Set CJK Font as default font in TMP Settings");
                EditorUtility.DisplayDialog("Success", "CJK Font set as default in TMP Settings", "OK");
            }
        }
    }
}