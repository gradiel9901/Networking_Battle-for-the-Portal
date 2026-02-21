using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Com.MyCompany.MyGame
{
    public class SettingsMenuUI : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _mainMenuPanel; // To return to

        [Header("Sliders")]
        [SerializeField] private Slider _bgmSlider;
        [SerializeField] private Slider _playerSoundSlider;
        [SerializeField] private Slider _npcSoundSlider;

        [Header("Buttons")]
        [SerializeField] private Button _applyButton;

        public static SettingsMenuUI Instance { get; private set; }

        public bool IsSettingsOpen => _settingsPanel != null && _settingsPanel.activeSelf;

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

            if (_settingsPanel != null) _settingsPanel.SetActive(false);

            if (_applyButton != null) _applyButton.onClick.AddListener(ApplySettings);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (IsSettingsOpen)
                {
                    CloseSettings();
                }
            }
        }

        public void OpenSettings()
        {
            if (_settingsPanel == null) return;
            
            // Read current values so we don't apply un-saved changes if we previously hit Escape
            if (SoundManager.Instance != null)
            {
                if (_bgmSlider != null) _bgmSlider.value = SoundManager.Instance.BgmVolume;
                if (_playerSoundSlider != null) _playerSoundSlider.value = SoundManager.Instance.PlayerVolume;
                if (_npcSoundSlider != null) _npcSoundSlider.value = SoundManager.Instance.NpcVolume;
            }

            if (_mainMenuPanel != null) _mainMenuPanel.SetActive(false);
            _settingsPanel.SetActive(true);
        }

        public void CloseSettings()
        {
            if (_settingsPanel == null) return;
            
            _settingsPanel.SetActive(false);
            if (_mainMenuPanel != null) _mainMenuPanel.SetActive(true);
        }

        private void ApplySettings()
        {
            if (SoundManager.Instance != null)
            {
                float bgmVal = _bgmSlider != null ? _bgmSlider.value : 1f;
                float playerVal = _playerSoundSlider != null ? _playerSoundSlider.value : 1f;
                float npcVal = _npcSoundSlider != null ? _npcSoundSlider.value : 1f;

                SoundManager.Instance.ApplyVolumes(bgmVal, playerVal, npcVal);
            }
            
            CloseSettings();
        }
    }
}
