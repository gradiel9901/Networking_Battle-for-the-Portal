using UnityEngine;

namespace Com.MyCompany.MyGame
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Volume Settings")]
        [Range(0f, 1f)] public float BgmVolume = 1f;
        [Range(0f, 1f)] public float PlayerVolume = 1f;
        [Range(0f, 1f)] public float NpcVolume = 1f;

        public static event System.Action<float> OnNpcVolumeChanged;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioSource _sfxSource2D;
        [SerializeField] private AudioSource _playerMovementSource; // Used for walking/running
        [SerializeField] private AudioSource _playerVoiceSource;    // Used for panting

        [Header("Audio Clips")]
        public AudioClip BGMClip;
        public AudioClip WalkClip;
        public AudioClip RunClip;
        public AudioClip JumpClip;
        public AudioClip PantingClip;
        public AudioClip AttackClip;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Ensure AudioSources exist
            if (_bgmSource == null) _bgmSource = gameObject.AddComponent<AudioSource>();
            if (_sfxSource2D == null) _sfxSource2D = gameObject.AddComponent<AudioSource>();
            if (_playerMovementSource == null) _playerMovementSource = gameObject.AddComponent<AudioSource>();
            if (_playerVoiceSource == null) _playerVoiceSource = gameObject.AddComponent<AudioSource>();

            // Setup looping for continuous sounds
            _bgmSource.loop = true;
            _playerMovementSource.loop = true;
            _playerVoiceSource.loop = true;

            // Start BGM immediately if assigned
            if (BGMClip != null)
            {
                PlayBGM(BGMClip);
            }
        }

        public void PlayBGM(AudioClip clip)
        {
            if (clip == null || _bgmSource == null) return;
            _bgmSource.clip = clip;
            _bgmSource.volume = BgmVolume;
            _bgmSource.Play();
        }

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || _sfxSource2D == null) return;
            _sfxSource2D.volume = PlayerVolume;
            _sfxSource2D.PlayOneShot(clip);
        }

        // Play 3D Sound at a specific location
        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeMultiplier = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, volumeMultiplier);
        }

        public void ApplyVolumes(float bgm, float player, float npc)
        {
            BgmVolume = bgm;
            PlayerVolume = player;
            NpcVolume = npc;

            if (_bgmSource != null) _bgmSource.volume = BgmVolume;
            
            if (_playerMovementSource != null) _playerMovementSource.volume = PlayerVolume;
            if (_playerVoiceSource != null) _playerVoiceSource.volume = PlayerVolume;
            if (_sfxSource2D != null) _sfxSource2D.volume = PlayerVolume;

            OnNpcVolumeChanged?.Invoke(NpcVolume);
        }

        public void SetPlayerMovementSound(bool isWalking, bool isRunning)
        {
            if (_playerMovementSource == null) return;

            if (isRunning && RunClip != null)
            {
                if (_playerMovementSource.clip != RunClip || !_playerMovementSource.isPlaying)
                {
                    _playerMovementSource.clip = RunClip;
                    _playerMovementSource.Play();
                }
            }
            else if (isWalking && WalkClip != null)
            {
                if (_playerMovementSource.clip != WalkClip || !_playerMovementSource.isPlaying)
                {
                    _playerMovementSource.clip = WalkClip;
                    _playerMovementSource.Play();
                }
            }
            else
            {
                _playerMovementSource.Stop();
                _playerMovementSource.clip = null;
            }
        }

        public void SetPantingSound(bool isPanting)
        {
            if (_playerVoiceSource == null || PantingClip == null) return;

            if (isPanting)
            {
                if (!_playerVoiceSource.isPlaying)
                {
                    _playerVoiceSource.clip = PantingClip;
                    _playerVoiceSource.Play();
                }
            }
            else
            {
                if (_playerVoiceSource.isPlaying)
                {
                    _playerVoiceSource.Stop();
                }
            }
        }
    }
}
