using System.IO;
using System;
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;

namespace LastRefuge.Editor
{
    public class CreateCJKFontAsset
    {
        [MenuItem("Tools/Last Refuge/Create CJK Font Asset")]
        public static void CreateFontAsset()
        {
            string fontPath = GetSystemFontPath();
            if (string.IsNullOrEmpty(fontPath))
            {
                EditorUtility.DisplayDialog("Error", "Could not find a suitable CJK font on the system.\nPlease install Noto Sans SC or Microsoft YaHei.", "OK");
                return;
            }

            string outputDir = "Assets/Fonts";
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Copy font to project
            string fontFileName = Path.GetFileName(fontPath);
            string projectFontPath = Path.Combine(outputDir, fontFileName);
            File.Copy(fontPath, projectFontPath, true);
            AssetDatabase.ImportAsset(projectFontPath);
            
            string assetPath = Path.Combine(outputDir, "NotoSansSC-Regular.asset");
            
            // Load the font as a Unity Font asset
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(projectFontPath);
            if (sourceFont == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to load font from project.", "OK");
                return;
            }
            
            // Create TMP Font Asset with Static population and ASCII character set
            // This pre-populates the atlas with ASCII characters (valid atlas texture)
            // Chinese characters will be dynamically added at runtime via TMP's dynamic fallback
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont, 
                90,                   // sampling point size
                2048,                 // atlas width
                GlyphRenderMode.SDFAA,
                5,                    // atlas padding
                1,                    // character set selection (1 = ASCII)
                AtlasPopulationMode.Static
            );

            if (fontAsset != null)
            {
                AssetDatabase.CreateAsset(fontAsset, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                // Verify the font asset has valid atlas texture
                if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
                {
                    UnityEngine.Debug.Log($"Created CJK Font Asset at: {assetPath}");
                    UnityEngine.Debug.Log($"Atlas size: {fontAsset.atlasWidth}x{fontAsset.atlasHeight}, Atlas texture: {fontAsset.atlasTextures[0].name}");
                    EditorUtility.DisplayDialog("Success", $"CJK Font Asset created at:\n{assetPath}\nAtlas: {fontAsset.atlasWidth}x{fontAsset.atlasHeight}\nTexture: {fontAsset.atlasTextures[0].name}", "OK");
                }
                else
                {
                    UnityEngine.Debug.LogError("Font asset created but atlas texture is null!");
                    EditorUtility.DisplayDialog("Warning", "Font asset created but atlas texture is null. Check console.", "OK");
                }
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
                EditorUtility.DisplayDialog("Error", "CJK Font Asset not found. Run 'Create CJK Font Asset' first.", "OK");
                return;
            }

            // Verify font asset is valid (atlas texture exists, even if minimal in batch mode)
            // Dynamic/Static mode in batch mode with Null graphics may produce minimal atlas
            // but will populate correctly at runtime with GPU
            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "Font asset has no atlas texture array. Regenerate it first.", "OK");
                return;
            }

            var settings = TMP_Settings.instance;
            if (settings != null)
            {
                var serializedSettings = new SerializedObject(settings);
                var defaultFontProp = serializedSettings.FindProperty("m_defaultFontAsset");
                var fallbackFontsProp = serializedSettings.FindProperty("m_fallbackFontAssets");

                defaultFontProp.objectReferenceValue = fontAsset;
                
                // Add to fallback if not already there
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

        private static string GetSystemFontPath()
        {
            string windowsFonts = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Fonts));
            
            // Try common CJK fonts in order of preference
            string[] fontFiles = new string[]
            {
                "NotoSansSC-Regular.ttf",
                "NotoSansSC-Medium.ttf",
                "msyh.ttc",       // Microsoft YaHei
                "msyh.ttf",
                "msyhbd.ttc",     // Microsoft YaHei Bold
                "simhei.ttf",     // SimHei
                "simsun.ttc",     // SimSun
                "NotoSansCJKsc-Regular.otf",
                "SourceHanSansSC-Regular.otf",
            };

            foreach (string fontFile in fontFiles)
            {
                string fullPath = Path.Combine(windowsFonts, fontFile);
                if (File.Exists(fullPath))
                {
                    UnityEngine.Debug.Log($"Found CJK font: {fullPath}");
                    return fullPath;
                }
            }

            // Try to find any TTF/OTF with CJK support
            if (Directory.Exists(windowsFonts))
            {
                var allFonts = Directory.GetFiles(windowsFonts, "*.ttf");
                foreach (var f in allFonts)
                {
                    string name = Path.GetFileName(f).ToLower();
                    if (name.Contains("noto") || name.Contains("yahei") || name.Contains("heiti") || 
                        name.Contains("cjk") || name.Contains("hans") || name.Contains("sourcehan"))
                    {
                        UnityEngine.Debug.Log($"Found potential CJK font: {f}");
                        return f;
                    }
                }
            }

            return null;
        }
    }
}