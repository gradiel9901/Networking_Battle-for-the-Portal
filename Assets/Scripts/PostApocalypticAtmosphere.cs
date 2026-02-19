using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Network
{
    [RequireComponent(typeof(Volume))]
    public class PostApocalypticAtmosphere : MonoBehaviour
    {
        [Header("Light Reference")]
        [SerializeField] private Light _directionalLight;

        [Header("Atmosphere Tweaks")]
        [SerializeField] private bool _applyOnAwake = true;

        private Volume _volume;

        private void Awake()
        {
            _volume = GetComponent<Volume>();

            if (_applyOnAwake)
            {
                ApplyAtmosphere();
            }
        }

        [ContextMenu("Apply Post-Apocalyptic Atmosphere")]
        public void ApplyAtmosphere()
        {
            SetupVolume();
            SetupDirectionalLight();
            SetupFog();
            SetupAmbientLight();
        }

        private void SetupVolume()
        {
            if (_volume == null) _volume = GetComponent<Volume>();

            _volume.isGlobal = true;
            _volume.priority = 1;

            var profile = _volume.profile;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _volume.profile = profile;
            }

            // ── Color Adjustments ──
            if (!profile.TryGet(out ColorAdjustments colorAdj))
                colorAdj = profile.Add<ColorAdjustments>(true);

            colorAdj.active = true;
            colorAdj.postExposure.Override(-0.5f);
            colorAdj.contrast.Override(30f);
            colorAdj.saturation.Override(-40f);
            colorAdj.colorFilter.Override(new Color(0.75f, 0.82f, 0.65f, 1f)); // Sickly green tint

            // ── Lift Gamma Gain ──
            if (!profile.TryGet(out LiftGammaGain lgg))
                lgg = profile.Add<LiftGammaGain>(true);

            lgg.active = true;
            lgg.lift.Override(new Vector4(-0.05f, -0.02f, -0.08f, 0f));   // Push shadows towards green
            lgg.gamma.Override(new Vector4(-0.03f, 0.02f, -0.05f, 0f));   // Green midtones
            lgg.gain.Override(new Vector4(-0.05f, -0.02f, -0.08f, 0f));   // Desaturated highlights

            // ── Tonemapping ──
            if (!profile.TryGet(out Tonemapping tonemap))
                tonemap = profile.Add<Tonemapping>(true);

            tonemap.active = true;
            tonemap.mode.Override(TonemappingMode.ACES);

            // ── Bloom ──
            if (!profile.TryGet(out Bloom bloom))
                bloom = profile.Add<Bloom>(true);

            bloom.active = true;
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(new Color(0.6f, 0.7f, 0.5f, 1f)); // Greenish bloom

            // ── Vignette ──
            if (!profile.TryGet(out Vignette vignette))
                vignette = profile.Add<Vignette>(true);

            vignette.active = true;
            vignette.color.Override(new Color(0.1f, 0.12f, 0.08f, 1f)); // Dark green-black edges
            vignette.intensity.Override(0.45f);
            vignette.smoothness.Override(0.4f);

            // ── Film Grain ──
            if (!profile.TryGet(out FilmGrain grain))
                grain = profile.Add<FilmGrain>(true);

            grain.active = true;
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.35f);
            grain.response.Override(0.6f);

            // ── Chromatic Aberration ──
            if (!profile.TryGet(out ChromaticAberration ca))
                ca = profile.Add<ChromaticAberration>(true);

            ca.active = true;
            ca.intensity.Override(0.1f);

            // ── Color Curves ── (crush blacks, muted highlights)
            if (!profile.TryGet(out ColorCurves curves))
                curves = profile.Add<ColorCurves>(true);

            curves.active = true;

            Debug.Log("[PostApocalypticAtmosphere] Volume profile configured.");
        }

        private void SetupDirectionalLight()
        {
            if (_directionalLight == null)
            {
                _directionalLight = FindAnyObjectByType<Light>();
                if (_directionalLight == null || _directionalLight.type != LightType.Directional)
                {
                    Debug.LogWarning("[PostApocalypticAtmosphere] No Directional Light found!");
                    return;
                }
            }

            // Dim, sickly yellow-green sunlight through thick clouds
            _directionalLight.color = new Color(0.65f, 0.7f, 0.5f, 1f);
            _directionalLight.intensity = 0.6f;

            // Low angle sun — feels like dusk / overcast
            _directionalLight.transform.rotation = Quaternion.Euler(25f, -30f, 0f);

            // Soft shadows
            _directionalLight.shadows = LightShadows.Soft;
            _directionalLight.shadowStrength = 0.85f;

            Debug.Log("[PostApocalypticAtmosphere] Directional light configured.");
        }

        private void SetupFog()
        {
            // Heavy greenish fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.22f, 0.25f, 0.18f, 1f); // Dark olive fog
            RenderSettings.fogDensity = 0.025f;

            Debug.Log("[PostApocalypticAtmosphere] Fog configured.");
        }

        private void SetupAmbientLight()
        {
            // Dark, greenish ambient
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.18f, 0.12f, 1f); // Dark muted green
            RenderSettings.ambientIntensity = 0.8f;

            // Muddy reflections
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.2f;

            Debug.Log("[PostApocalypticAtmosphere] Ambient lighting configured.");
        }

        [ContextMenu("Reset to Defaults")]
        public void ResetToDefaults()
        {
            if (_volume != null && _volume.profile != null)
            {
                _volume.profile.components.Clear();
            }

            RenderSettings.fog = false;
            RenderSettings.ambientLight = Color.white;
            RenderSettings.ambientIntensity = 1f;

            if (_directionalLight != null)
            {
                _directionalLight.color = Color.white;
                _directionalLight.intensity = 1f;
            }

            Debug.Log("[PostApocalypticAtmosphere] Reset to defaults.");
        }
    }
}
