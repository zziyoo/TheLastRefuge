using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LastRefuge.Core;
using LastRefuge.Data;
using LastRefuge.Systems;

namespace LastRefuge.UI
{
    public class CharacterItemUI : MonoBehaviour
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI workText;
        public Image healthBar;
        public Image hungerBar;
        public Image stressBar;
        public Image fatigueBar;
        public Button[] workButtons;
        
        private CharacterState character;
        private ICharacterSystem characterSystem;
        
        public void Setup(CharacterState character, ICharacterSystem system)
        {
            this.character = character;
            this.characterSystem = system;
            
            Refresh();
        }
        
        public void Refresh()
        {
            if (character == null) return;
            
            if (nameText) nameText.text = $"{character.name} ({character.profession})";
            if (statusText) statusText.text = $"HP:{character.health} 饥饿:{character.hunger:F0} 压力:{character.stress:F0} 疲劳:{character.fatigue:F0}";
            if (workText) workText.text = $"工作: {character.currentWork.GetDisplayName()}";
            
            if (healthBar) healthBar.fillAmount = (float)character.health / character.maxHealth;
            if (hungerBar) hungerBar.fillAmount = character.hunger / 100f;
            if (stressBar) stressBar.fillAmount = character.stress / 100f;
            if (fatigueBar) fatigueBar.fillAmount = character.fatigue / 100f;
            
            if (hungerBar) hungerBar.color = character.hunger > 70 ? Color.red : (character.hunger > 40 ? Color.yellow : Color.green);
            if (stressBar) stressBar.color = character.stress > 70 ? Color.red : (character.stress > 40 ? Color.yellow : Color.green);
            if (fatigueBar) fatigueBar.color = character.fatigue > 70 ? Color.red : (character.fatigue > 40 ? Color.yellow : Color.green);
        }
        
        public void OnWorkButtonClicked(WorkType workType)
        {
            if (characterSystem != null && character != null)
            {
                characterSystem.AssignWork(character.characterId, workType);
            }
        }
    }
}