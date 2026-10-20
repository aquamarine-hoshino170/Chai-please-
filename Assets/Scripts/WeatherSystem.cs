using UnityEngine;

namespace TapriTycoon
{
    /// <summary>
    /// Dynamic Weather System supporting SUNNY, RAIN (600 particles), and FOG:
    /// - Configures and controls Unity's ParticleSystem for 600 falling raindrops
    /// - Controls RenderSettings exponential fog, directional light intensity, and wet ground smoothness
    /// - Slows customer arrival rate slightly during Rain/Fog while boosting hot Chai demand
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public enum WeatherType
        {
            SUNNY,
            RAIN,
            FOG
        }

        [Header("Current Weather State")]
        public WeatherType currentWeather = WeatherType.SUNNY;
        public float autoCycleIntervalSeconds = 32f;
        public KeyCode cycleWeatherKey = KeyCode.C;

        [Header("Unity Particle System (600 Rain Particles)")]
        public ParticleSystem rainParticleSystem;
        public int rainMaxParticles = 600;
        public float rainEmissionRate = 300f;

        [Header("Lighting & Atmosphere References")]
        public Light directionalSunLight;
        public Camera mainCamera;
        public Material courtyardGroundMaterial;
        public Material highwayRoadMaterial;

        private float _weatherTimer;
        private float _targetSunIntensity = 1.3f;
        private float _targetFogDensity = 0.012f;
        private Color _targetSkyColor = new Color(0.16f, 0.08f, 0.04f);
        private float _targetGroundSmoothness = 0.15f;

        private void Start()
        {
            _weatherTimer = autoCycleIntervalSeconds;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            ConfigureRainParticleSystem();
            SetWeather(WeatherType.SUNNY, false);
        }

        /// <summary>
        /// Programmatically ensures the Unity ParticleSystem is configured for 600 monsoon raindrops.
        /// </summary>
        private void ConfigureRainParticleSystem()
        {
            if (rainParticleSystem == null) return;

            var main = rainParticleSystem.main;
            main.maxParticles = rainMaxParticles; // 600 particles as requested
            main.startLifetime = 1.8f;
            main.startSpeed = 16f;
            main.startSize = 0.08f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = rainParticleSystem.emission;
            emission.rateOverTime = rainEmissionRate;

            var shape = rainParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 1f, 22f);

            var velocity = rainParticleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = -3.2f; // monsoon wind slant
            velocity.y = -14f;
        }

        private void Update()
        {
            _weatherTimer -= Time.deltaTime;
            if (_weatherTimer <= 0f)
            {
                CycleWeather();
            }

            if (Input.GetKeyDown(cycleWeatherKey))
            {
                CycleWeather();
            }

            // Smoothly interpolate lighting, fog density, and wet PBR ground reflections
            if (directionalSunLight != null)
            {
                directionalSunLight.intensity = Mathf.Lerp(
                    directionalSunLight.intensity,
                    _targetSunIntensity,
                    Time.deltaTime * 3f
                );
            }

            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, _targetFogDensity, Time.deltaTime * 2.5f);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, _targetSkyColor, Time.deltaTime * 2.5f);

            if (mainCamera != null)
            {
                mainCamera.backgroundColor = RenderSettings.fogColor;
            }

            if (courtyardGroundMaterial != null && courtyardGroundMaterial.HasProperty("_Smoothness"))
            {
                float cur = courtyardGroundMaterial.GetFloat("_Smoothness");
                courtyardGroundMaterial.SetFloat("_Smoothness", Mathf.Lerp(cur, _targetGroundSmoothness, Time.deltaTime * 3f));
            }

            if (highwayRoadMaterial != null && highwayRoadMaterial.HasProperty("_Smoothness"))
            {
                float cur = highwayRoadMaterial.GetFloat("_Smoothness");
                highwayRoadMaterial.SetFloat("_Smoothness", Mathf.Lerp(cur, _targetGroundSmoothness, Time.deltaTime * 3f));
            }
        }

        public void CycleWeather()
        {
            WeatherType next = (WeatherType)(((int)currentWeather + 1) % 3);
            SetWeather(next, true);
        }

        public void SetWeather(WeatherType nextWeather, bool announceInWorld = true)
        {
            currentWeather = nextWeather;
            _weatherTimer = autoCycleIntervalSeconds;

            switch (currentWeather)
            {
                case WeatherType.SUNNY:
                    _targetSunIntensity = 1.35f;
                    _targetFogDensity = 0.012f;
                    _targetSkyColor = new Color(0.16f, 0.08f, 0.04f);
                    _targetGroundSmoothness = 0.14f;
                    if (rainParticleSystem != null && rainParticleSystem.isPlaying)
                        rainParticleSystem.Stop();
                    break;

                case WeatherType.RAIN:
                    _targetSunIntensity = 0.72f;
                    _targetFogDensity = 0.022f;
                    _targetSkyColor = new Color(0.08f, 0.12f, 0.17f);
                    _targetGroundSmoothness = 0.78f; // Wet reflective puddles
                    if (rainParticleSystem != null && !rainParticleSystem.isPlaying)
                        rainParticleSystem.Play();

                    if (GameManager.Instance != null && GameManager.Instance.tableManager != null)
                        GameManager.Instance.tableManager.BoostAllSeatedCustomersDemand(1);
                    break;

                case WeatherType.FOG:
                    _targetSunIntensity = 0.85f;
                    _targetFogDensity = 0.045f; // Dense winter/monsoon morning fog
                    _targetSkyColor = new Color(0.18f, 0.21f, 0.24f);
                    _targetGroundSmoothness = 0.42f;
                    if (rainParticleSystem != null && rainParticleSystem.isPlaying)
                        rainParticleSystem.Stop();

                    if (GameManager.Instance != null && GameManager.Instance.tableManager != null)
                        GameManager.Instance.tableManager.BoostAllSeatedCustomersDemand(1);
                    break;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.UpdateHUD();

                if (announceInWorld)
                {
                    string banner = currentWeather == WeatherType.RAIN
                        ? "RAIN (600 Particles)! Slower Arrivals, High Chai Demand (3-5)!"
                        : currentWeather == WeatherType.FOG
                        ? "CHILLY FOG! Customers Crave Extra Hot Masala Chai (2-5)!"
                        : "SUNNY SKIES! Fast Customer Arrivals (1-5 Chai)!";

                    Color textCol = currentWeather == WeatherType.SUNNY ? Color.yellow : Color.cyan;
                    GameManager.Instance.SpawnFloatingText(new Vector3(0.8f, 3.6f, 0f), banner, textCol);
                }
            }
        }

        public float GetArrivalDelayMultiplier()
        {
            switch (currentWeather)
            {
                case WeatherType.RAIN:
                    return 1.5f; // Slower customer arrivals during monsoon rain
                case WeatherType.FOG:
                    return 1.25f; // Slightly slower arrivals in fog
                default:
                    return 1.0f;
            }
        }

        public int RollCustomerChaiDemand()
        {
            switch (currentWeather)
            {
                case WeatherType.RAIN:
                    return Random.Range(3, 6); // 3 to 5 hot chai in Rain
                case WeatherType.FOG:
                    return Random.Range(2, 6); // 2 to 5 hot chai in Fog
                default:
                    return Random.Range(1, 6); // 1 to 5 chai in Sunny
            }
        }

        public string GetWeatherStatusSummary()
        {
            switch (currentWeather)
            {
                case WeatherType.RAIN:
                    return "RAIN (600 drops) · 3-5 Chai · 0.65x Arrival";
                case WeatherType.FOG:
                    return "MISTY FOG · 2-5 Chai · 0.8x Arrival";
                default:
                    return "SUNNY · 1-5 Chai · 1.0x Arrival";
            }
        }
    }
}
