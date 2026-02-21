using UnityEngine;
using UnityEngine.UI;

namespace Com.MyCompany.MyGame
{
    // just a simple wrapper around a UI Slider to display health
    public class HPBarController : MonoBehaviour
    {
        [SerializeField] private Slider hpSlider;

        private void Start()
        {
            // try to grab the slider automatically if it wasnt set in inspector
            if (hpSlider == null)
            {
                hpSlider = GetComponent<Slider>();
            }
        }

        public void UpdateHealth(int currentHealth, int maxHealth)
        {
            if (hpSlider != null)
            {
                hpSlider.maxValue = maxHealth;
                hpSlider.value = currentHealth;
            }
        }
    }
}
