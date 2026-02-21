using UnityEngine;
using Fusion;
using TMPro;
using UnityEngine.UI;

namespace Com.MyCompany.MyGame
{
    // main menu launcher, handles player name + character selection before starting the game
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
            // swap to the loading label while fusion connects
            controlPanel.SetActive(false);
            progressLabel.SetActive(true);

            if (Network.NetworkSessionManager.Instance == null)
            {
                Debug.LogError("[FusionLauncher] NetworkSessionManager Instance is NULL! Make sure it is in the scene.");
                return;
            }

            // convert the dropdown selection to bytes so we can send it as a connection token
            byte[] token = null;
            if (colorDropdown != null)
            {
                Network.NetworkSessionManager.Instance.LocalCharacterIndex = colorDropdown.value;
                int index = colorDropdown.value;
                token = System.BitConverter.GetBytes(index);
                Debug.Log($"[FusionLauncher] Selected Character Index: {index}");
            }

            // AutoHostOrClient means fusion decides if we're host or client automatically
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
