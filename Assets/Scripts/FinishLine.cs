using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Network
{
    // collision trigger at the end of the level, shows a win screen when the local player crosses it
    public class FinishLine : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _finishUICanvas;
        [SerializeField] private GameObject _finishPanel;
        [SerializeField] private TextMeshProUGUI _youWonText;
        [SerializeField] private TextMeshProUGUI _waitingText;
        [SerializeField] private Button _spectateButton;

        // prevent the trigger from firing multiple times
        private bool _hasFinished = false;

        private void Start()
        {
            if (_finishUICanvas != null)
                _finishUICanvas.SetActive(false);

            if (_spectateButton != null)
                _spectateButton.onClick.AddListener(OnSpectateClicked);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasFinished) return;

            var player = other.GetComponentInParent<NetworkPlayer>();
            if (player == null) return;

            // only the player controlling this character should trigger this
            if (!player.HasInputAuthority) return;

            _hasFinished = true;

            // tell the server this player finished via RPC
            player.RPC_SetFinished();

            ShowFinishUI();

            Debug.Log($"[FinishLine] {player.PlayerName} crossed the finish line!");

            // disable so no other players can trigger it again on this client
            gameObject.SetActive(false);
        }

        private void ShowFinishUI()
        {
            if (_finishUICanvas != null)
                _finishUICanvas.SetActive(true);

            if (_finishPanel != null)
                _finishPanel.SetActive(true);

            if (_youWonText != null)
                _youWonText.text = "You Won!";

            if (_waitingText != null)
                _waitingText.text = "Waiting for other players...";

            // unlock the cursor so the player can click the spectate button
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnSpectateClicked()
        {
            if (NetworkPlayer.Local != null)
            {
                NetworkPlayer.Local.SpectateNextPlayer();
            }
        }
    }
}
