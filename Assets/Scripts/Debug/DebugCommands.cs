using System;
using System.Collections.Generic;
using UnityEngine;
using LastRefuge.Core;
using LastRefuge.Data;
using LastRefuge.Systems;

namespace LastRefuge.Debug
{
    public class DebugCommands : MonoBehaviour
    {
        private GameManager gameManager;
        
        private void Start()
        {
            gameManager = GameManager.Instance;
        }
        
        private void Update()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            
            HandleDebugKeys();
        }
        
        private void HandleDebugKeys()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                PrintGameState();
            }
            
            if (Input.GetKeyDown(KeyCode.F2))
            {
                AddResources();
            }
            
            if (Input.GetKeyDown(KeyCode.F3))
            {
                SpawnCharacter();
            }
            
            if (Input.GetKeyDown(KeyCode.F4))
            {
                SetDay(10);
            }
            
            if (Input.GetKeyDown(KeyCode.F5))
            {
                gameManager.SaveGame(false);
            }
            
            if (Input.GetKeyDown(KeyCode.F6))
            {
                BuildDebugBuilding();
            }
            
            if (Input.GetKeyDown(KeyCode.F7))
            {
                AdvanceDay();
            }
            
            if (Input.GetKeyDown(KeyCode.F8))
            {
                TriggerRandomEvent();
            }
        }
        
        public void PrintGameState()
        {
            if (gameManager == null) return;
            
            var state = gameManager.gameState;
            var resSys = gameManager.resourceSystem;
            var charSys = gameManager.characterSystem;
            var buildSys = gameManager.buildingSystem;
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== GAME STATE ===");
            sb.AppendLine($"Seed: {state.gameSeed}");
            sb.AppendLine($"Day: {state.currentDay}");
            sb.AppendLine($"TimeSlot: {state.currentTimeSlot}");
            sb.AppendLine($"GameplayState: {state.gameplayState}");
            sb.AppendLine();
            
            sb.AppendLine("--- RESOURCES ---");
            var resources = resSys.GetAllResources();
            foreach (var kvp in resources)
            {
                int cap = resSys.GetCapacity(kvp.Key);
                int net = resSys.GetNetDailyChange(kvp.Key);
                int days = resSys.GetEstimatedDaysRemaining(kvp.Key);
                sb.AppendLine($"{kvp.Key}: {kvp.Value}/{cap} (净:{net}/天, 剩余:{days}天)");
            }
            sb.AppendLine();
            
            sb.AppendLine("--- CHARACTERS ---");
            var characters = charSys.GetAliveCharacters();
            foreach (var c in characters)
            {
                sb.AppendLine($"{c.name} ({c.profession}) HP:{c.health} 饥饿:{c.hunger:F0} 压力:{c.stress:F0} 疲劳:{c.fatigue:F0} 工作:{c.currentWork} 效率:{charSys.GetWorkEfficiency(c):F2}");
            }
            sb.AppendLine();
            
            sb.AppendLine("--- BUILDINGS ---");
            var buildings = buildSys.GetAllBuildings();
            foreach (var b in buildings)
            {
                var def = buildSys.GetBuildingDefinition(b.definitionId);
                string defName = def?.name ?? b.definitionId;
                sb.AppendLine($"{defName} Lv{b.level} 耐久:{b.durability} 启用:{b.enabled} 工人:{b.assignedWorkers?.Length ?? 0}");
            }
            
            UnityEngine.Debug.Log(sb.ToString());
        }
        
        public void AddResources()
        {
            if (gameManager == null) return;
            
            var resSys = gameManager.resourceSystem;
            
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                resSys.Add(type, 1000, "Debug");
            }
            
            UnityEngine.Debug.Log("Added 1000 of all resources");
        }
        
        public void SpawnCharacter()
        {
            if (gameManager == null) return;
            
            var charSys = gameManager.characterSystem;
            var randSys = gameManager.randomSystem;
            
            int count = charSys.GetAllCharacters().Length;
            string id = $"debug_char_{count}";
            string name = $"Debug{count + 1}";
            var prof = (Profession)randSys.NextInt(Enum.GetValues(typeof(Profession)).Length);
            
            charSys.CreateCharacter(id, name, prof);
            
            UnityEngine.Debug.Log($"Spawned character: {name} ({prof})");
        }
        
        public void SetDay(int day)
        {
            if (gameManager == null) return;
            
            gameManager.timeSystem.SetTime(day, TimeSlot.Morning);
            gameManager.timeSystem.SetGameplayState(GameplayState.Morning);
            
            UnityEngine.Debug.Log($"Set day to {day}");
        }
        
        public void BuildDebugBuilding()
        {
            if (gameManager == null) return;
            
            var buildSys = gameManager.buildingSystem;
            var randSys = gameManager.randomSystem;
            
            var definitions = new List<string>(buildSys.GetType().GetField("buildingDefinitions", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(buildSys) as Dictionary<string, BuildingDefinition>).Keys;
            
            if (definitions.Count > 0)
            {
                string defId = definitions[randSys.NextInt(definitions.Count)];
                var building = buildSys.Build(defId);
                
                if (building != null)
                {
                    UnityEngine.Debug.Log($"Built: {defId}");
                }
                else
                {
                    UnityEngine.Debug.Log($"Failed to build: {defId} (insufficient resources)");
                }
            }
        }
        
        public void AdvanceDay()
        {
            if (gameManager == null) return;
            
            for (int i = 0; i < 6; i++)
            {
                gameManager.AdvanceTimeSlot();
            }
            
            UnityEngine.Debug.Log($"Advanced to Day {gameManager.GetCurrentDay()}");
        }
        
        public void TriggerRandomEvent()
        {
            UnityEngine.Debug.Log("Random event triggered (not implemented yet)");
        }
        
        public void KillRandomCharacter()
        {
            if (gameManager == null) return;
            
            var charSys = gameManager.characterSystem;
            var characters = charSys.GetAliveCharacters();
            
            if (characters.Length > 0)
            {
                var victim = characters[UnityEngine.Random.Range(0, characters.Length)];
                charSys.KillCharacter(victim.characterId, "Debug Kill");
                UnityEngine.Debug.Log($"Killed: {victim.name}");
            }
        }
        
        public void HealAllCharacters()
        {
            if (gameManager == null) return;
            
            var charSys = gameManager.characterSystem;
            var characters = charSys.GetAllCharacters();
            
            foreach (var c in characters)
            {
                if (c.alive)
                {
                    c.health = c.maxHealth;
                    c.hunger = 0;
                    c.stress = 0;
                    c.fatigue = 0;
                }
            }
            
            UnityEngine.Debug.Log("Healed all characters");
        }
        
        public void SetGameSpeed(float speed)
        {
            Time.timeScale = speed;
            UnityEngine.Debug.Log($"Game speed set to {speed}x");
        }
    }
}