using UnityEngine;
using TMPro;

namespace LastRefuge.UI
{
    public class ResourceItemUI : MonoBehaviour
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI amountText;
        public TextMeshProUGUI changeText;
        public TextMeshProUGUI daysRemainingText;
        public Image iconImage;
        public Slider capacitySlider;
        
        public void Setup(string name, int amount, int capacity, int netChange, int daysRemaining, Sprite icon = null)
        {
            if (nameText) nameText.text = name;
            if (amountText) amountText.text = $"{amount} / {capacity}";
            if (changeText) changeText.text = netChange >= 0 ? $"+{netChange}/天" : $"{netChange}/天";
            if (changeText) changeText.color = netChange >= 0 ? Color.green : Color.red;
            if (daysRemainingText) daysRemainingText.text = daysRemaining >= 0 ? (daysRemaining == -1 ? "∞" : $"{daysRemaining}天") : "--";
            if (iconImage && icon) iconImage.sprite = icon;
            if (capacitySlider)
            {
                capacitySlider.maxValue = capacity;
                capacitySlider.value = amount;
            }
        }
    }
}