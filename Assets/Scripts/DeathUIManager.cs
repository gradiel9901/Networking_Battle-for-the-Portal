using TMPro;
using UnityEngine;

namespace Com.MyCompany.MyGame
{
    // shows the death screen + countdown when the local player dies
    public class DeathUIManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private UnityEngine.UI.Button spectateButton;
        [SerializeField] private TextMeshProUGUI countdownText;

        // singleton so other scripts can get to this easily
        public static DeathUIManager Instance;

        private void Awake()
        {
            // make sure only one of these exists
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (deathPanel != null) deathPanel.SetActive(false);

            if (spectateButton != null)
                spectateButton.onClick.AddListener(OnSpectateClicked);
        }

        private void OnSpectateClicked()
        {
            if (Network.NetworkPlayer.Local != null)
                Network.NetworkPlayer.Local.SpectateNextPlayer();

            // hide the death panel when spectating
            ToggleDeathScreen(false);
        }

        public void ToggleDeathScreen(bool show)
        {
            if (deathPanel != null)
                deathPanel.SetActive(show);

            // clear the countdown text when hiding
            if (!show && countdownText != null)
                countdownText.text = "";
        }

        public void UpdateCountdown(int seconds)
        {
            if (countdownText == null) return;
            // show empty string when the timer hits 0
            countdownText.text = seconds > 0 ? $"Respawning in {seconds}..." : "";
        }
    }
}
