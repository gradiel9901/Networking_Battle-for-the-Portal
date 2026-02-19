using UnityEngine;
using UnityEngine.UI;

namespace Com.MyCompany.MyGame
{

public class StaminaController : MonoBehaviour
    {
        [Header("HP")]
        [SerializeField] private Slider hpSlider;

        [Header("Stamina")]
        [SerializeField] private Slider staminaSlider;

        public static StaminaController Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        public void UpdateHealth(int currentHealth, int maxHealth)
        {
            if (hpSlider == null) return;
            hpSlider.maxValue = maxHealth;
            hpSlider.value    = currentHealth;
        }

        public void UpdateStamina(float current, float max)
        {
            if (staminaSlider == null) return;
            staminaSlider.maxValue = max;
            staminaSlider.value    = current;
        }
    }
}
