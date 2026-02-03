using UnityEngine;

namespace Com.MyCompany.MyGame
{
    public static class TeamVisualsHelper
    {
        // Cache the texture so we don't regenerate it every time
        private static Texture2D _softParticleTexture;

        private static Texture2D GetSoftTexture()
        {
            if (_softParticleTexture != null) return _softParticleTexture;

            // Generate a 64x64 soft radial gradient texture
            int size = 64;
            _softParticleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    // Optional: Make it fall off non-linearly for softer edges (pow 2 or 3)
                    alpha = Mathf.Pow(alpha, 2f); 
                    
                    _softParticleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            _softParticleTexture.Apply();
            return _softParticleTexture;
        }

        public static void ClearVisuals(GameObject target)
        {
            Transform fire = target.transform.Find("FireContainer");
            if (fire != null) Object.Destroy(fire.gameObject);

            Transform ice = target.transform.Find("IceContainer");
            if (ice != null) Object.Destroy(ice.gameObject);
        }

        public static void ApplyFireVisuals(GameObject target, Renderer renderer)
        {
            // Clean up Ice if it exists
            Transform ice = target.transform.Find("IceContainer");
            if (ice != null) Object.Destroy(ice.gameObject);

            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 0.2f, 0f); 
                renderer.material.EnableKeyword("_EMISSION");
                // Pulsing emission handled by shader or constant for now
                renderer.material.SetColor("_EmissionColor", new Color(0.8f, 0.1f, 0f) * 3f);
            }

            if (target.transform.Find("FireContainer") != null) return;

            GameObject root = new GameObject("FireContainer");
            root.transform.SetParent(target.transform, false);
            root.transform.localPosition = Vector3.zero;

            // 1. Flames (Core)
            CreateParticleSystem(root, "Flames", 
                startColor: new Color(1f, 0.6f, 0f), 
                endColor: new Color(1f, 0f, 0f, 0f),
                size: 0.8f, speed: 3f, count: 40, lifetime: 0.8f,
                isSmoke: false);

            // 2. Smoke (Dark, rising high)
            var smoke = CreateParticleSystem(root, "Smoke", 
                startColor: new Color(0.2f, 0.2f, 0.2f, 0.5f), 
                endColor: new Color(0f, 0f, 0f, 0f), 
                size: 1.5f, speed: 4f, count: 15, lifetime: 2.5f,
                isSmoke: true);
            
            // 3. Embers (Tiny, bright, chaotic)
            CreateEmbers(root);
        }

        public static void ApplyIceVisuals(GameObject target, Renderer renderer)
        {
            // Clean up Fire if it exists
            Transform fire = target.transform.Find("FireContainer");
            if (fire != null) Object.Destroy(fire.gameObject);

            if (renderer != null)
            {
                renderer.material.color = new Color(0.5f, 0.9f, 1f); 
                renderer.material.EnableKeyword("_EMISSION");
                renderer.material.SetColor("_EmissionColor", new Color(0f, 0.6f, 1f) * 2f);
            }

            if (target.transform.Find("IceContainer") != null) return;

            GameObject root = new GameObject("IceContainer");
            root.transform.SetParent(target.transform, false);
            root.transform.localPosition = Vector3.zero;

            // 1. Icy Mist (Soft base)
            CreateParticleSystem(root, "Mist", 
                startColor: new Color(0f, 0.8f, 1f, 0.4f), 
                endColor: new Color(1f, 1f, 1f, 0f), 
                size: 1.2f, speed: 0.5f, count: 30, lifetime: 2.0f,
                isSmoke: false);

            // 2. Snow/Ice Flakes (Falling)
            CreateSnow(root);
        }

        private static ParticleSystem CreateParticleSystem(GameObject parent, string name, Color startColor, Color endColor, float size, float speed, int count, float lifetime, bool isSmoke)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            obj.transform.localPosition = new Vector3(0, -0.5f, 0); // Feet
            obj.transform.localRotation = Quaternion.Euler(-90, 0, 0); // Up

            var ps = obj.AddComponent<ParticleSystem>();
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            
            // Material with Soft Texture
            // Default to our Custom Shaders
            string shaderName = isSmoke ? "Mobile/Particles/Alpha Blended" : (name.Contains("Flames") ? "Custom/StylizedFire" : "Custom/StylizedIce");
            // Fallback for snow/mist if distinct logic desired, but let's stick to the requested HLSL
            if (name.Contains("Mist")) shaderName = "Custom/StylizedIce";
            if (name.Contains("Snow")) shaderName = "Mobile/Particles/Additive"; // Snow uses simple add
            if (name.Contains("Smoke")) shaderName = "Mobile/Particles/Alpha Blended";

            var mat = new Material(Shader.Find(shaderName)); 
            if (mat.shader.name == "Hidden/InternalErrorShader" || !mat.shader.isSupported)
                mat = new Material(Shader.Find("Mobile/Particles/Alpha Blended")); // Ultimate fallback

            mat.mainTexture = GetSoftTexture();
            
            // Set Shader properties if needed
            if (shaderName.Contains("Fire"))
            {
                mat.SetFloat("_DetailScale", 2f);
                mat.SetFloat("_WobbleAmount", 0.3f); // Restored to 0.3f
            }

            renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Main
            var main = ps.main;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = startColor;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Emission (RESTORED)
            var em = ps.emission;
            em.rateOverTime = count; // Full count

            // Shape
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = isSmoke ? 25 : 10;
            sh.radius = 0.4f;

            // Color over Lifetime
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new Gradient()
            {
                colorKeys = new GradientColorKey[] { new GradientColorKey(startColor, 0), new GradientColorKey(endColor, 1f) },
                alphaKeys = new GradientAlphaKey[] { new GradientAlphaKey(startColor.a, 0), new GradientAlphaKey(endColor.a, 1f) }
            };

            // Size over Lifetime
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0, 0.5f), 
                new Keyframe(0.2f, 1f), 
                new Keyframe(1f, isSmoke ? 2f : 0f) // Smoke grows, Fire shrinks
            ));

            return ps;
        }

        private static void CreateEmbers(GameObject parent)
        {
            GameObject obj = new GameObject("Embers");
            obj.transform.SetParent(parent.transform, false);
            obj.transform.localPosition = new Vector3(0, -0.5f, 0);

            var ps = obj.AddComponent<ParticleSystem>();
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            
            // Use Fire shader for Embers to get wobble
            var mat = new Material(Shader.Find("Custom/StylizedFire"));
            mat.mainTexture = GetSoftTexture(); 
            mat.SetFloat("_WobbleAmount", 0.4f); // Restored
            renderer.material = mat;

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f); // Tiny
            main.startColor = Color.yellow;
            main.gravityModifier = -0.2f; // float up
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.rateOverTime = 20; // Restored

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.5f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-1f, 1f); // Restored
            vel.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f; // Restored
            noise.frequency = 0.5f;
        }

        private static void CreateSnow(GameObject parent)
        {
            GameObject obj = new GameObject("Snow");
            obj.transform.SetParent(parent.transform, false);
            obj.transform.localPosition = new Vector3(0, 2f, 0); // Start above head
            obj.transform.localRotation = Quaternion.Euler(90, 0, 0); // Down

            var ps = obj.AddComponent<ParticleSystem>();
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            
            // Use Ice shader for Snow to get Sparkle
            var mat = new Material(Shader.Find("Custom/StylizedIce"));
            mat.mainTexture = GetSoftTexture(); 
            renderer.material = mat;

            var main = ps.main;
            main.startLifetime = 1.5f;
            main.startSpeed = 2f;
            main.startSize = 0.15f;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // Follow player

            var em = ps.emission;
            em.rateOverTime = 30; // Restored

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(1.5f, 1.5f, 0.5f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.y = -1f; // Fall

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new Gradient() {
                 alphaKeys = new GradientAlphaKey[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0, 1) }
            };
        }
    }
}
