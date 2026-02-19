using TMPro;
using UnityEngine;

namespace Com.MyCompany.MyGame
{
    public class DeathUIManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private UnityEngine.UI.Button spectateButton;
        [SerializeField] private TextMeshProUGUI countdownText;

        public static DeathUIManager Instance;

        private void Awake()
        {
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

            ToggleDeathScreen(false);
        }

        public void ToggleDeathScreen(bool show)
        {
            if (deathPanel != null)
                deathPanel.SetActive(show);

if (!show && countdownText != null)
                countdownText.text = "";
        }

        public void UpdateCountdown(int seconds)
        {
            if (countdownText == null) return;
            countdownText.text = seconds > 0 ? $"Respawning in {seconds}..." : "";
        }
    }
}
