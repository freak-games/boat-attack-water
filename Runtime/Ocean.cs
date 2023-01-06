using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using WaterSystem.Rendering;

namespace WaterSystem
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("URP Water System/Ocean")]
    public class Ocean : MonoBehaviour
    {
        public Texture2D defaultFoamMap; // a default foam texture map
        public Texture2D defaultSurfaceMap; // a default normal/caustic map
        public Texture2D defaultWaterFX; // texture with correct values for default WaterFX
        public Material defaultSeaMaterial;
        public Mesh[] defaultWaterMeshes;

        [HideInInspector, SerializeField] public Data.Wave[] waves;

        private float _maxWaveHeight;
        private float _waveHeight;

        [SerializeReference] public Data.OceanSettings settingsData = new Data.OceanSettings();

        private WaterFxPass _waterBufferPass;
        private Material _causticMaterial;
        private Texture2D _rampTexture;

        private static readonly int CameraRoll = Shader.PropertyToID("_CameraRoll");
        private static readonly int InvViewProjection = Shader.PropertyToID("_InvViewProjection");
        private static readonly int FoamMap = Shader.PropertyToID("_FoamMap");
        private static readonly int SurfaceMap = Shader.PropertyToID("_SurfaceMap");
        private static readonly int WaveHeight = Shader.PropertyToID("_WaveHeight");
        private static readonly int MaxWaveHeight = Shader.PropertyToID("_MaxWaveHeight");
        private static readonly int MaxDepth = Shader.PropertyToID("_MaxDepth");
        private static readonly int WaveCount = Shader.PropertyToID("_WaveCount");
        private static readonly int CubemapTexture = Shader.PropertyToID("_CubemapTexture");
        private static readonly int WaveData = Shader.PropertyToID("waveData");
        private static readonly int WaterFXShaderTag = Shader.PropertyToID("_WaterFXMap");

        private static readonly int BoatAttackWaterDistanceBlend =
            Shader.PropertyToID("_BoatAttack_Water_DistanceBlend");

        private static readonly int AbsorptionColor = Shader.PropertyToID("_AbsorptionColor");
        private static readonly int ScatteringColor = Shader.PropertyToID("_ScatteringColor");

        private static readonly int BoatAttackWaterMicroWaveIntensity =
            Shader.PropertyToID("_BoatAttack_Water_MicroWaveIntensity");

        private static readonly int BoatAttackWaterFoamIntensity =
            Shader.PropertyToID("_BoatAttack_water_FoamIntensity");

        private static readonly int RampTexture = Shader.PropertyToID("_BoatAttack_RampTexture");
        private static readonly string LowEndMobileQuality = "_LOWEND_MOBILE_QUALITY";

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
            Init();
        }

        private void OnDisable()
        {
            Cleanup();
        }

        void Cleanup()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        }

        private void BeginCameraRendering(ScriptableRenderContext src, Camera cam)
        {
            if (cam.cameraType == CameraType.Preview) return;

            if (settingsData.refType == Data.ReflectionType.PlanarReflection)
                PlanarReflections.Execute(src, cam, transform);

            _waterBufferPass ??= new WaterFxPass();

            var urpData = cam.GetUniversalAdditionalCameraData();
            urpData.scriptableRenderer.EnqueuePass(_waterBufferPass);

            var roll = cam.transform.localEulerAngles.z;
            Shader.SetGlobalFloat(CameraRoll, roll);
            Shader.SetGlobalMatrix(InvViewProjection,
                (GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix).inverse);

            const float quantizeValue = 6.25f;
            const float forwards = 10f;
            const float yOffset = -0.25f;

            var newPos = cam.transform.TransformPoint(Vector3.forward * forwards);
            newPos.y = yOffset + transform.position.y;
            newPos.x = quantizeValue * (int)(newPos.x / quantizeValue);
            newPos.z = quantizeValue * (int)(newPos.z / quantizeValue);

            var blendDist = (settingsData.distanceBlend + 10) / 100f;

            var matrix =
                Matrix4x4.TRS(newPos, Quaternion.identity, Vector3.one * blendDist); // transform.localToWorldMatrix;

            Shader.EnableKeyword(LowEndMobileQuality);

            settingsData.refType = Data.ReflectionType.ReflectionProbe;

            Shader.DisableKeyword("_REFLECTION_CUBEMAP");
            Shader.EnableKeyword("_REFLECTION_PROBES");
            Shader.DisableKeyword("_REFLECTION_PLANARREFLECTION");

            foreach (var mesh in defaultWaterMeshes)
            {
                Graphics.DrawMesh(mesh,
                    matrix,
                    defaultSeaMaterial,
                    gameObject.layer,
                    cam,
                    0,
                    null,
                    ShadowCastingMode.Off,
                    false,
                    null,
                    LightProbeUsage.Off,
                    null);
            }
        }

        [ContextMenu("Init")]
        public void Init()
        {
            GenerateColorRamp();
            SetWaves();

            PlanarReflections.m_planeOffset = transform.position.y;
            PlanarReflections.m_settings = settingsData.planarSettings;
            PlanarReflections.m_settings.m_ClipPlaneOffset = 0; //transform.position.y;

            Shader.DisableKeyword("_BOATATTACK_WATER_DEBUG");
        }

        private void SetWaves()
        {
            SetupWaves(settingsData._customWaves);

            Shader.SetGlobalTexture(FoamMap, defaultFoamMap);
            Shader.SetGlobalTexture(SurfaceMap, defaultSurfaceMap);
            Shader.SetGlobalTexture(WaterFXShaderTag, defaultWaterFX);

            _maxWaveHeight = 0f;
            foreach (var w in waves)
            {
                _maxWaveHeight += w.amplitude;
            }

            _maxWaveHeight /= waves.Length;

            _waveHeight = transform.position.y;

            Shader.SetGlobalColor(AbsorptionColor, settingsData._absorptionColor.gamma);
            Shader.SetGlobalColor(ScatteringColor, settingsData._scatteringColor.linear);

            Shader.SetGlobalFloat(WaveHeight, _waveHeight);
            Shader.SetGlobalFloat(BoatAttackWaterMicroWaveIntensity, settingsData._microWaveIntensity);
            Shader.SetGlobalFloat(MaxWaveHeight, _maxWaveHeight);
            Shader.SetGlobalFloat(MaxDepth, settingsData._waterMaxVisibility);
            Shader.SetGlobalFloat(BoatAttackWaterDistanceBlend, settingsData.distanceBlend);
            Shader.SetGlobalFloat(BoatAttackWaterFoamIntensity, settingsData._foamIntensity);

            Shader.SetGlobalInt(WaveCount, waves.Length);
            Shader.DisableKeyword("USE_STRUCTURED_BUFFER");
            Shader.SetGlobalVectorArray(WaveData, GetWaveData());
        }

        private void GenerateColorRamp()
        {
            const int rampCount = 2;
            const int rampRes = 128;

            var pixelHeight = Mathf.CeilToInt(rampCount / 4.0f);

            if (_rampTexture == null)
                _rampTexture = new Texture2D(rampRes, pixelHeight, GraphicsFormat.R8G8B8A8_SRGB,
                    TextureCreationFlags.None);
            _rampTexture.wrapMode = TextureWrapMode.Clamp;

            var cols = new Color[rampRes * pixelHeight];
            for (var i = 0; i < rampRes; i++)
            {
                var val = settingsData._waveDepthProfile.Evaluate(i / (float)rampRes);
                cols[i].b = Mathf.LinearToGammaSpace(val);
            }

            _rampTexture.SetPixels(cols);
            _rampTexture.Apply();
            Shader.SetGlobalTexture(RampTexture, _rampTexture);
        }

        private Vector4[] GetWaveData()
        {
            var waveData = new Vector4[20];
            for (var i = 0; i < waves.Length; i++)
            {
                waveData[i] = new Vector4(waves[i].amplitude, waves[i].direction, waves[i].wavelength,
                    waves[i].onmiDir);
                waveData[i + 10] = new Vector4(waves[i].origin.x, waves[i].origin.y, 0, 0);
            }

            return waveData;
        }

        private void SetupWaves(bool custom)
        {
            if (!custom)
            {
                //create basic waves based off basic wave settings
                var backupSeed = Random.state;
                Random.InitState(settingsData.randomSeed);
                var basicWaves = settingsData._basicWaveSettings;
                var a = basicWaves.amplitude;
                var d = basicWaves.direction;
                var l = basicWaves.wavelength;
                var numWave = basicWaves.waveCount;
                waves = new Data.Wave[numWave];

                var r = 1f / numWave;

                for (var i = 0; i < numWave; i++)
                {
                    var p = Mathf.Lerp(0.5f, 1.5f, i * r);
                    var amp = a * p * Random.Range(0.33f, 1.66f);
                    var dir = d + Random.Range(-90f, 90f);
                    var len = l * p * Random.Range(0.6f, 1.4f);
                    waves[i] = new Data.Wave(amp, dir, len, Vector2.zero, false);
                    Random.InitState(settingsData.randomSeed + i + 1);
                }

                Random.state = backupSeed;
            }
            else
            {
                waves = settingsData._waves.ToArray();
            }
        }
    }
}