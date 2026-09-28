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

            // Use the Font Asset Creator API with correct parameter order
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,                         // samplingPointSize
                5,                          // atlasPadding
                GlyphRenderMode.SDFAA,      // renderMode
                2048,                       // atlasWidth
                2048,                       // atlasHeight
                AtlasPopulationMode.Dynamic,
                true                        // enableMultiAtlasSupport
            );

            if (fontAsset != null)
            {
                // Ensure Atlas Texture and Material are properly set up
                EnsureFontAssetPersistence(fontAsset);

                // Create the main Font Asset
                AssetDatabase.CreateAsset(fontAsset, assetPath);

                // Persist Atlas Texture as sub-asset (check if already persisted by CreateFontAsset)
                if (fontAsset.atlasTexture != null)
                {
                    string atlasName = fontAsset.name + " Atlas";
                    if (AssetDatabase.GetAssetPath(fontAsset.atlasTexture) != assetPath)
                    {
                        fontAsset.atlasTexture.name = atlasName;
                        AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
                        UnityEngine.Debug.Log($"Added Atlas Texture as sub-asset: {fontAsset.atlasTexture.name}");
                    }
                    else
                    {
                        UnityEngine.Debug.Log($"Atlas Texture already persisted as sub-asset: {fontAsset.atlasTexture.name}");
                    }
                }

                // Persist Material as sub-asset
                if (fontAsset.material != null)
                {
                    if (AssetDatabase.GetAssetPath(fontAsset.material) != assetPath)
                    {
                        fontAsset.material.name = fontAsset.name + " Material";
                        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                        UnityEngine.Debug.Log($"Added Material as sub-asset: {fontAsset.material.name}");
                    }
                    else
                    {
                        UnityEngine.Debug.Log($"Material already persisted as sub-asset: {fontAsset.material.name}");
                    }
                }

                // Persist any additional atlas textures (multi-atlas support)
                if (fontAsset.atlasTextures != null)
                {
                    for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                    {
                        if (fontAsset.atlasTextures[i] != null && AssetDatabase.GetAssetPath(fontAsset.atlasTextures[i]) != assetPath)
                        {
                            fontAsset.atlasTextures[i].name = fontAsset.name + " Atlas " + i;
                            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[i], fontAsset);
                            UnityEngine.Debug.Log($"Added Atlas Texture {i} as sub-asset: {fontAsset.atlasTextures[i].name}");
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Verify persistence by reloading
                var reloadedFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                if (reloadedFontAsset != null)
                {
                    UnityEngine.Debug.Log($"Reloaded Font Asset: {reloadedFontAsset.name}");
                    UnityEngine.Debug.Log($"Atlas size: {reloadedFontAsset.atlasWidth}x{reloadedFontAsset.atlasHeight}");
                    UnityEngine.Debug.Log($"Atlas texture count: {reloadedFontAsset.atlasTextures?.Length ?? 0}");
                    UnityEngine.Debug.Log($"Atlas Texture: {reloadedFontAsset.atlasTexture?.name ?? "null"}");
                    UnityEngine.Debug.Log($"Material: {reloadedFontAsset.material?.name ?? "null"}");
                    UnityEngine.Debug.Log($"Atlas Textures: {reloadedFontAsset.atlasTextures?.Length ?? 0}");
                    
                    if (reloadedFontAsset.atlasTextures != null && reloadedFontAsset.atlasTextures.Length > 0 && reloadedFontAsset.atlasTextures[0] != null && reloadedFontAsset.material != null)
                    {
                        EditorUtility.DisplayDialog("Success", $"CJK Font Asset created and persisted successfully!\nAtlas: {reloadedFontAsset.atlasWidth}x{reloadedFontAsset.atlasHeight}\nTexture: {reloadedFontAsset.atlasTextures[0].name}\nMaterial: {reloadedFontAsset.material.name}", "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Warning", "Font Asset created but Atlas Texture or Material may not be persisted correctly. Check console.", "OK");
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Failed to reload Font Asset after creation.", "OK");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Failed to create TMP Font Asset.", "OK");
            }
        }

        private static void EnsureFontAssetPersistence(TMP_FontAsset fontAsset)
        {
            // Ensure Material exists and is properly configured
            if (fontAsset.material == null)
            {
                // Create a new TMP Material with Distance Field shader
                Shader sdfShader = Shader.Find("TextMeshPro/Distance Field");
                if (sdfShader != null)
                {
                    Material material = new Material(sdfShader);
                    material.name = "Temp Material";
                    fontAsset.material = material;
                }
                else
                {
                    UnityEngine.Debug.LogError("Could not find TextMeshPro/Distance Field shader!");
                }
            }

            // Ensure Atlas Texture exists
            if (fontAsset.atlasTexture == null && (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0 || fontAsset.atlasTextures[0] == null))
            {
                UnityEngine.Debug.LogWarning("Font Asset has no Atlas Texture. The CreateFontAsset API should have created one in Dynamic mode.");
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

            // Verify persistence before setting as default
            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0 || fontAsset.atlasTextures[0] == null || fontAsset.material == null)
            {
                EditorUtility.DisplayDialog("Error", "Font Asset is missing Atlas Texture or Material. Re-create it first.", "OK");
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