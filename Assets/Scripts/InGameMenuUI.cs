using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Com.MyCompany.MyGame
{
    public class InGameMenuUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitButton;

        public static InGameMenuUI Instance { get; private set; }

        public bool IsMenuOpen => _menuPanel != null && _menuPanel.activeSelf;

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

            if (_menuPanel != null) _menuPanel.SetActive(false);

            if (_resumeButton != null) _resumeButton.onClick.AddListener(ResumeGame);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(OpenSettings);
            if (_quitButton != null) _quitButton.onClick.AddListener(QuitGame);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                // If settings is open, SettingsMenuUI handles closing itself. We only toggle Main Menu if Settings is NOT open.
                bool isSettingsOpen = SettingsMenuUI.Instance != null && SettingsMenuUI.Instance.IsSettingsOpen;
                if (!isSettingsOpen)
                {
                    ToggleMenu();
                }
            }
        }

        public void ToggleMenu()
        {
            if (_menuPanel == null) return;

            bool isOpening = !_menuPanel.activeSelf;
            _menuPanel.SetActive(isOpening);

            if (isOpening)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void ResumeGame()
        {
            if (_menuPanel != null) _menuPanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OpenSettings()
        {
            if (SettingsMenuUI.Instance != null)
            {
                SettingsMenuUI.Instance.OpenSettings();
            }
            else
            {
                Debug.LogWarning("SettingsMenuUI is missing in the scene!");
            }
        }

        private void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
