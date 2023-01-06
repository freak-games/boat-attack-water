using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WaterSystem.Rendering;

namespace WaterSystem
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("URP Water System/Ocean")]
    public class Ocean : MonoBehaviour
    {
        public Texture2D defaultSurfaceMap;
        public Material defaultSeaMaterial;
        public Mesh[] defaultWaterMeshes;

        public float distanceBlend = 100.0f;
        public int randomSeed = 3234;

        public Color _scatteringColor = new Color(0.0f, 0.085f, 0.1f);

        public float _microWaveIntensity = 0.25f;

        [Range(3, 12)] public int waveCount = 6;
        public float amplitude;
        public float direction;
        public float wavelength;

        public Shader waterInf;

        [HideInInspector, SerializeField] public Data.Wave[] waves;

        private InfiniteWaterPass _infiniteWaterPass;
        private WaterFxPass _waterBufferPass;

        private static readonly int SurfaceMap = Shader.PropertyToID("_SurfaceMap");
        private static readonly int WaveCount = Shader.PropertyToID("_WaveCount");
        private static readonly int WaveData = Shader.PropertyToID("waveData");

        private static readonly int BoatAttackWaterDistanceBlend =
            Shader.PropertyToID("_BoatAttack_Water_DistanceBlend");

        private static readonly int ScatteringColor = Shader.PropertyToID("_ScatteringColor");

        private static readonly int BoatAttackWaterMicroWaveIntensity =
            Shader.PropertyToID("_BoatAttack_Water_MicroWaveIntensity");

        [SerializeField] private Mesh defaultInfinitewWaterMesh;

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

            _infiniteWaterPass ??= new InfiniteWaterPass(defaultInfinitewWaterMesh, waterInf);
            _waterBufferPass ??= new WaterFxPass();

            var urpData = cam.GetUniversalAdditionalCameraData();
            urpData.scriptableRenderer.EnqueuePass(_infiniteWaterPass);
            urpData.scriptableRenderer.EnqueuePass(_waterBufferPass);

            const float quantizeValue = 6.25f;
            const float forwards = 10f;
            const float yOffset = -0.25f;

            var newPos = cam.transform.TransformPoint(Vector3.forward * forwards);
            newPos.y = yOffset + transform.position.y;
            newPos.x = quantizeValue * (int)(newPos.x / quantizeValue);
            newPos.z = quantizeValue * (int)(newPos.z / quantizeValue);

            var blendDist = (distanceBlend + 10) / 100f;

            var matrix =
                Matrix4x4.TRS(newPos, Quaternion.identity, Vector3.one * blendDist); // transform.localToWorldMatrix;

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
            SetWaves();
        }

        private void SetWaves()
        {
            SetupWaves();

            Shader.SetGlobalTexture(SurfaceMap, defaultSurfaceMap);
            Shader.SetGlobalColor(ScatteringColor, _scatteringColor.linear);
            Shader.SetGlobalFloat(BoatAttackWaterMicroWaveIntensity, _microWaveIntensity);
            Shader.SetGlobalFloat(BoatAttackWaterDistanceBlend, distanceBlend);
            Shader.SetGlobalInt(WaveCount, waves.Length);
            Shader.SetGlobalVectorArray(WaveData, GetWaveData());
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

        private void SetupWaves()
        {
            var backupSeed = Random.state;
            Random.InitState(randomSeed);
            ;
            var a = amplitude;
            var d = direction;
            var l = wavelength;
            var numWave = waveCount;
            waves = new Data.Wave[numWave];

            var r = 1f / numWave;

            for (var i = 0; i < numWave; i++)
            {
                var p = Mathf.Lerp(0.5f, 1.5f, i * r);
                var amp = a * p * Random.Range(0.33f, 1.66f);
                var dir = d + Random.Range(-90f, 90f);
                var len = l * p * Random.Range(0.6f, 1.4f);
                waves[i] = new Data.Wave(amp, dir, len, Vector2.zero, false);
                Random.InitState(randomSeed + i + 1);
            }

            Random.state = backupSeed;
        }
    }
}