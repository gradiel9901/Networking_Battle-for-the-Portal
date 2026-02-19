using UnityEngine;
using Fusion;
using TMPro;
using UnityEngine.UI;

namespace Com.MyCompany.MyGame
{
    public class FusionLauncher : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private TMP_Dropdown colorDropdown;
        [SerializeField] private Button startButton;
        [SerializeField] private GameObject controlPanel;
        [SerializeField] private GameObject progressLabel;

        private void Start()
        {
            startButton.onClick.AddListener(StartGame);
            controlPanel.SetActive(true);
            progressLabel.SetActive(false);
        }

        private void StartGame()
        {
            controlPanel.SetActive(false);
            progressLabel.SetActive(true);

            if (Network.NetworkSessionManager.Instance == null)
            {
                Debug.LogError("[FusionLauncher] NetworkSessionManager Instance is NULL! Make sure it is in the scene.");
                return;
            }

            // Store Character Selection
            byte[] token = null;
            if (colorDropdown != null)
            {
                Network.NetworkSessionManager.Instance.LocalCharacterIndex = colorDropdown.value;
                int index = colorDropdown.value;
                token = System.BitConverter.GetBytes(index);
                Debug.Log($"[FusionLauncher] Selected Character Index: {index}");
            }

            // Using AutoHostOrClient as requested for testing
            Network.NetworkSessionManager.Instance.StartGame(GameMode.AutoHostOrClient, token);
        }

        public string GetLocalPlayerName()
        {
            return nameInputField != null ? nameInputField.text : "Player";
        }

        public int GetLocalPlayerTeamIndex()
        {
            return colorDropdown != null ? colorDropdown.value : 0;
        }
    }
}
