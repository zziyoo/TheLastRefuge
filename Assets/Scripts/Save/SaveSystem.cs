using System;
using System.IO;
using System.Text;
using System.Linq;
using UnityEngine;
using LastRefuge.Core;
using LastRefuge.Data;

namespace LastRefuge.Save
{
    public class SaveSystem : ISaveSystem
    {
        private const string SAVE_FOLDER = "Saves";
        private const string AUTO_SAVE_NAME = "autosave.json";
        private const string MANUAL_SAVE_PREFIX = "save_";
        private const int CURRENT_SAVE_VERSION = 1;
        private const int CURRENT_CONTENT_VERSION = 1;
        
        /// <summary>
        /// Overrides the save folder, used by tests to keep player saves untouched.
        /// Null (the normal case) means the production folder under persistentDataPath.
        /// </summary>
        public static string OverrideSaveDirectory = null;
        
        private GameState gameState;
        private string gameVersion = "0.1.0";
        
        public event Action<bool, string> OnSaveComplete;
        public event Action<bool, string> OnLoadComplete;
        
        public void Initialize(GameState state, string version)
        {
            gameState = state;
            gameVersion = version;
            
            string savePath = GetSaveDirectory();
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }
        }
        
        private string GetSaveDirectory()
        {
            if (!string.IsNullOrEmpty(OverrideSaveDirectory))
            {
                return OverrideSaveDirectory;
            }
            return Path.Combine(Application.persistentDataPath, SAVE_FOLDER);
        }
        
        public void CreateNewGame(string seed = null)
        {
            if (string.IsNullOrEmpty(seed))
            {
                seed = GenerateSeed();
            }
            
            gameState.gameSeed = seed;
            gameState.currentDay = 1;
            gameState.currentTimeSlot = TimeSlot.Morning;
            gameState.gameplayState = GameplayState.GameStart;
            gameState.saveVersion = CURRENT_SAVE_VERSION;
            gameState.contentVersion = CURRENT_CONTENT_VERSION;
            gameState.saveTimestamp = DateTime.UtcNow.ToString("o");
            
            gameState.resources = Array.Empty<ResourceState>();
            gameState.characters = Array.Empty<CharacterState>();
            gameState.buildings = Array.Empty<BuildingState>();
            gameState.relationships = Array.Empty<RelationshipState>();
            gameState.eventHistory = Array.Empty<EventHistoryEntry>();
            gameState.gameFlags = new GameFlags();
            gameState.worldState = new WorldState();
        }
        
        private string GenerateSeed()
        {
            return $"{DateTime.UtcNow.Ticks}_{UnityEngine.Random.Range(10000, 99999)}";
        }
        
        public bool SaveGame(bool isAutoSave = false, string customName = null)
        {
            try
            {
                gameState.saveTimestamp = DateTime.UtcNow.ToString("o");
                
                var saveData = new SaveData
                {
                    saveVersion = CURRENT_SAVE_VERSION,
                    contentVersion = CURRENT_CONTENT_VERSION,
                    gameVersion = gameVersion,
                    gameSeed = gameState.gameSeed,
                    gameState = gameState,
                    saveTimestamp = gameState.saveTimestamp,
                    isAutoSave = isAutoSave
                };
                
                string json = JsonUtility.ToJson(saveData, true);
                
                string fileName = isAutoSave ? AUTO_SAVE_NAME : 
                    (customName ?? $"{MANUAL_SAVE_PREFIX}{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
                
                string savePath = GetSaveDirectory();
                string fullPath = Path.Combine(savePath, fileName);
                
                string tempPath = fullPath + ".tmp";
                File.WriteAllText(tempPath, json, Encoding.UTF8);

                string verifyJson = File.ReadAllText(tempPath, Encoding.UTF8);
                var verified = JsonUtility.FromJson<SaveData>(verifyJson);
                if (verified == null)
                {
                    throw new Exception("Save verification failed");
                }

                // Snapshot the previous save BEFORE touching the main file, so a failed
                // promotion can never leave the only recoverable copy destroyed.
                if (File.Exists(fullPath))
                {
                    File.Copy(fullPath, fullPath + ".bak", true);
                }

                // Promote the verified temp over the main file. The backup created above
                // already preserves the previous state in case this copy is interrupted.
                File.Copy(tempPath, fullPath, true);
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                OnSaveComplete?.Invoke(true, fullPath);
                
                EventBus.Publish(new GameSavedEvent
                {
                    isAutoSave = isAutoSave,
                    savePath = fullPath
                });
                
                UnityEngine.Debug.Log($"Game saved: {fullPath}");
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Save failed: {e.Message}\n{e.StackTrace}");
                OnSaveComplete?.Invoke(false, e.Message);
                return false;
            }
        }
        
        public bool LoadGame(string fileName)
        {
            try
            {
                string savePath = GetSaveDirectory();
                string fullPath = Path.Combine(savePath, fileName);
                string usedPath = fullPath;
                
                if (!TryReadSave(fullPath, out SaveData saveData))
                {
                    // The main file is missing or invalid: fall back to the last snapshot
                    // taken before the previous successful write (never a failed one).
                    string backupPath = fullPath + ".bak";
                    if (TryReadSave(backupPath, out saveData))
                    {
                        UnityEngine.Debug.LogWarning($"Main save missing or invalid ({fullPath}); recovered backup {backupPath}");
                        usedPath = backupPath;
                    }
                    else
                    {
                        throw new Exception($"No readable save or backup: {fullPath}");
                    }
                }
                
                MigrateSave(saveData);
                
                // Handle null/empty gameSeed - Unity JsonUtility serializes null as empty string
                string loadedSeed = saveData.gameSeed;
                if (string.IsNullOrEmpty(loadedSeed))
                {
                    loadedSeed = null;
                }
                gameState.gameSeed = loadedSeed;
                gameState.currentDay = saveData.gameState.currentDay;
                gameState.currentTimeSlot = saveData.gameState.currentTimeSlot;
                gameState.gameplayState = saveData.gameState.gameplayState;
                gameState.resources = saveData.gameState.resources;
                gameState.characters = saveData.gameState.characters;
                gameState.buildings = saveData.gameState.buildings;
                gameState.relationships = saveData.gameState.relationships;
                gameState.gameFlags = saveData.gameState.gameFlags ?? new GameFlags();
                gameState.eventHistory = saveData.gameState.eventHistory;
                gameState.worldState = saveData.gameState.worldState ?? new WorldState();
                gameState.saveVersion = saveData.saveVersion;
                gameState.contentVersion = saveData.contentVersion;
                gameState.saveTimestamp = saveData.saveTimestamp;
                
                OnLoadComplete?.Invoke(true, usedPath);
                
                EventBus.Publish(new GameLoadedEvent
                {
                    savePath = usedPath
                });
                
                UnityEngine.Debug.Log($"Game loaded: {usedPath}");
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Load failed: {e.Message}\n{e.StackTrace}");
                OnLoadComplete?.Invoke(false, e.Message);
                return false;
            }
        }
        
        /// <summary>
        /// Reads and validates a save file. A valid save has the "gameState" marker that
        /// every JsonUtility write contains; anything else (missing, empty, truncated,
        /// garbage or wrong-shaped) is rejected so loading falls back to the backup.
        /// </summary>
        private static bool TryReadSave(string path, out SaveData saveData)
        {
            saveData = null;
            if (!File.Exists(path)) return false;
            
            string raw;
            try
            {
                raw = File.ReadAllText(path, Encoding.UTF8);
            }
            catch
            {
                return false;
            }
            
            if (string.IsNullOrEmpty(raw) || !raw.Contains("\"gameState\"")) return false;
            
            try
            {
                saveData = JsonUtility.FromJson<SaveData>(raw);
            }
            catch
            {
                return false;
            }
            
            return saveData != null && saveData.gameState != null;
        }
        
        private void MigrateSave(SaveData saveData)
        {
            if (saveData.saveVersion < CURRENT_SAVE_VERSION)
            {
                UnityEngine.Debug.Log($"Migrating save from version {saveData.saveVersion} to {CURRENT_SAVE_VERSION}");
            }
            
            saveData.saveVersion = CURRENT_SAVE_VERSION;
            saveData.contentVersion = CURRENT_CONTENT_VERSION;
        }
        
        public string[] GetSaveFiles()
        {
            string savePath = GetSaveDirectory();
            if (!Directory.Exists(savePath)) return new string[0];
            
            return Directory.GetFiles(savePath, "*.json");
        }
        
        public SaveInfo GetSaveInfo(string fileName)
        {
            try
            {
                string savePath = GetSaveDirectory();
                string fullPath = Path.Combine(savePath, fileName);
                
                if (!File.Exists(fullPath)) return null;
                
                string json = File.ReadAllText(fullPath, Encoding.UTF8);
                var saveData = JsonUtility.FromJson<SaveData>(json);
                
                if (saveData == null) return null;
                
                return new SaveInfo
                {
                    fileName = fileName,
                    fullPath = fullPath,
                    gameSeed = saveData.gameSeed,
                    day = saveData.gameState.currentDay,
                    timeSlot = saveData.gameState.currentTimeSlot,
                    timestamp = saveData.saveTimestamp,
                    isAutoSave = saveData.isAutoSave,
                    population = saveData.gameState.characters?.Length ?? 0,
                    aliveCount = saveData.gameState.characters?.Count(c => c.alive) ?? 0
                };
            }
            catch
            {
                return null;
            }
        }
        
        public bool DeleteSave(string fileName)
        {
            try
            {
                string savePath = GetSaveDirectory();
                string fullPath = Path.Combine(savePath, fileName);
                
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Delete save failed: {e.Message}");
                return false;
            }
        }
    }
}