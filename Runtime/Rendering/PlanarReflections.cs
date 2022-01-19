﻿using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;
using Unity.Mathematics;

namespace UnityEngine.Rendering.Universal
{
    [ExecuteAlways]
    public class PlanarReflections : MonoBehaviour
    {
        [Serializable]
        public enum ResolutionModes
        {
            Full,
            Half,
            Third,
            Quarter,
            Multiplier,
            Custom,
        }

        [Serializable]
        public enum RendererMode
        {
            Match,
            Static,
            Offset
        }

        [Serializable]
        public class PlanarReflectionSettings
        {
            public ResolutionModes m_ResolutionMode = ResolutionModes.Third;
            public float m_ResolutionMultipliter = 1.0f;
            public int2 m_ResolutionCustom = new int2(320, 180);
            public float m_ClipPlaneOffset = 0.07f;
            public LayerMask m_ReflectLayers = -1;
            public bool m_Shadows;
            public RendererMode m_RendererMode;
            public int m_RendererIndex;
        }

        public static PlanarReflectionSettings m_settings = new PlanarReflectionSettings();

        public GameObject target;
        public static float m_planeOffset;

        private static Camera _reflectionCamera;
        private static Dictionary<Camera, RenderTexture> _reflectionTextures = new Dictionary<Camera, RenderTexture>();
        private static readonly int _planarReflectionTextureId = Shader.PropertyToID("_PlanarReflectionTexture");

        private int2 _oldReflectionTextureSize;

        public static event Action<ScriptableRenderContext, Camera> BeginPlanarReflections;

        private void OnEnable()
        {
            //RenderPipelineManager.beginCameraRendering += ExecutePlanarReflections;
        }

        // Cleanup all the objects we possibly have created
        private void OnDisable()
        {
            Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if(_reflectionCamera)
            {
                _reflectionCamera.targetTexture = null;
                SafeDestroy(_reflectionCamera.gameObject);
            }

            foreach (var textures in _reflectionTextures)
            {
                RenderTexture.ReleaseTemporary(textures.Value);
            }
            _reflectionTextures.Clear();
        }

        private static void SafeDestroy(Object obj)
        {
            if (Application.isEditor)
            {
                DestroyImmediate(obj);
            }
            else
            {
                Destroy(obj);
            }
        }

        private static void UpdateCamera(Camera src, Camera dest)
        {
            if (dest == null) return;

            dest.CopyFrom(src);
            dest.useOcclusionCulling = false;
            if (dest.gameObject.TryGetComponent(out UniversalAdditionalCameraData camData))
            {
                camData.renderShadows = m_settings.m_Shadows; // turn off shadows for the reflection camera
                switch (m_settings.m_RendererMode)
                {
                    case RendererMode.Static:
                        camData.SetRenderer(m_settings.m_RendererIndex);
                        break;
                    case RendererMode.Offset:
                        //TODO need API to get current index
                        break;
                    case RendererMode.Match:
                    default:
                        break;
                }
            }
        }

        private static void UpdateReflectionCamera(Camera realCamera, Transform transform)
        {
            if (_reflectionCamera == null)
                _reflectionCamera = CreateMirrorObjects(transform);

            // find out the reflection plane: position and normal in world space
            Vector3 pos = Vector3.zero;
            Vector3 normal = Vector3.up;
            /*if (target != null)
            {
                pos = target.transform.position + Vector3.up * m_planeOffset;
                normal = target.transform.up;
            }*/

            UpdateCamera(realCamera, _reflectionCamera);

            // Render reflection
            // Reflect camera around reflection plane
            var d = -Vector3.Dot(normal, pos) - m_planeOffset;
            var reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

            var reflection = Matrix4x4.identity;
            reflection *= Matrix4x4.Scale(new Vector3(1, -1, 1));

            CalculateReflectionMatrix(ref reflection, reflectionPlane);
            var oldPosition = realCamera.transform.position - new Vector3(0, pos.y * 2, 0);
            var newPosition = ReflectPosition(oldPosition);
            _reflectionCamera.transform.forward = Vector3.Scale(realCamera.transform.forward, new Vector3(1, -1, 1));
            _reflectionCamera.worldToCameraMatrix = realCamera.worldToCameraMatrix * reflection;

            // Setup oblique projection matrix so that near plane is our reflection
            // plane. This way we clip everything below/above it for free.
            var clipPlane = CameraSpacePlane(_reflectionCamera, pos - Vector3.up * 0.1f, normal, 1.0f);
            var projection = realCamera.CalculateObliqueMatrix(clipPlane);
            _reflectionCamera.projectionMatrix = projection;
            _reflectionCamera.cullingMask = m_settings.m_ReflectLayers; // never render water layer
            _reflectionCamera.transform.position = newPosition;
        }

        // Calculates reflection matrix around the given plane
        private static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, Vector4 plane)
        {
            reflectionMat.m00 = (1F - 2F * plane[0] * plane[0]);
            reflectionMat.m01 = (-2F * plane[0] * plane[1]);
            reflectionMat.m02 = (-2F * plane[0] * plane[2]);
            reflectionMat.m03 = (-2F * plane[3] * plane[0]);

            reflectionMat.m10 = (-2F * plane[1] * plane[0]);
            reflectionMat.m11 = (1F - 2F * plane[1] * plane[1]);
            reflectionMat.m12 = (-2F * plane[1] * plane[2]);
            reflectionMat.m13 = (-2F * plane[3] * plane[1]);

            reflectionMat.m20 = (-2F * plane[2] * plane[0]);
            reflectionMat.m21 = (-2F * plane[2] * plane[1]);
            reflectionMat.m22 = (1F - 2F * plane[2] * plane[2]);
            reflectionMat.m23 = (-2F * plane[3] * plane[2]);

            reflectionMat.m30 = 0F;
            reflectionMat.m31 = 0F;
            reflectionMat.m32 = 0F;
            reflectionMat.m33 = 1F;
        }

        private static Vector3 ReflectPosition(Vector3 pos)
        {
            var newPos = new Vector3(pos.x, -pos.y, pos.z);
            return newPos;
        }

        private static float GetScaleValue()
        {
            switch(m_settings.m_ResolutionMode)
            {
                case ResolutionModes.Full:
                    return 1f;
                case ResolutionModes.Half:
                    return 0.5f;
                case ResolutionModes.Third:
                    return 0.33f;
                case ResolutionModes.Quarter:
                    return 0.25f;
                case ResolutionModes.Multiplier:
                    return m_settings.m_ResolutionMultipliter;
                default:
                    return 0.5f; // default to half res
            }
        }

        // Compare two int2
        private static bool Int2Compare(int2 a, int2 b)
        {
            return a.x == b.x && a.y == b.y;
        }

        // Given position/normal of the plane, calculates plane in camera space.
        private static Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
        {
            var offsetPos = pos + normal * (m_settings.m_ClipPlaneOffset + m_planeOffset);
            var m = cam.worldToCameraMatrix;
            var cameraPosition = m.MultiplyPoint(offsetPos);
            var cameraNormal = m.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPosition, cameraNormal));
        }

        private static Camera CreateMirrorObjects(Transform transform)
        {
            var go = new GameObject("Planar Reflections",typeof(Camera));
            var cameraData = go.AddComponent(typeof(UniversalAdditionalCameraData)) as UniversalAdditionalCameraData;

            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            //cameraData.SetRenderer(1); TODO - specify renderer from settings

            var t = transform;
            var reflectionCamera = go.GetComponent<Camera>();
            reflectionCamera.transform.SetPositionAndRotation(t.position, t.rotation);
            reflectionCamera.depth = -10;
            reflectionCamera.enabled = false;
            go.hideFlags = HideFlags.HideAndDontSave;

            return reflectionCamera;
        }

        private static void PlanarReflectionTexture(Camera cam)
        {
            var dimensions = ReflectionResolution(cam, UniversalRenderPipeline.asset.renderScale);
            
            if (!_reflectionTextures.ContainsKey(cam))
            {
                _reflectionTextures.Add(cam, CreateTexture(dimensions));
            }
            else if(_reflectionTextures[cam] == null)
            {
                _reflectionTextures[cam] = CreateTexture(dimensions);
            }
            else if (_reflectionTextures[cam].width != dimensions.x)
            {
                RenderTexture.ReleaseTemporary(_reflectionTextures[cam]);
                _reflectionTextures[cam] = CreateTexture(dimensions);
            }
            _reflectionCamera.targetTexture =  _reflectionTextures[cam];
        }

        private static RenderTexture CreateTexture(int2 res)
        {
            Debug.Log("creating new RT");
            bool useHdr10 = RenderingUtils.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float);
            RenderTextureFormat hdrFormat = useHdr10 ? RenderTextureFormat.RGB111110Float : RenderTextureFormat.DefaultHDR;
            return RenderTexture.GetTemporary(res.x, res.y, 24,
                GraphicsFormatUtility.GetGraphicsFormat(hdrFormat, true));
        }

        private static int2 ReflectionResolution(Camera cam, float scale)
        {
            if (m_settings.m_ResolutionMode == ResolutionModes.Custom) return m_settings.m_ResolutionCustom;
            
            scale *= GetScaleValue();
            var x = (int)(cam.pixelWidth * scale);
            var y = (int)(cam.pixelHeight * scale);
            
            return new int2(x, y);
        }

        public static void Execute(ScriptableRenderContext context, Camera camera, Transform transform)
        {
            // we dont want to render planar reflections in reflections or previews
            if (camera.cameraType == CameraType.Reflection || camera.cameraType == CameraType.Preview)
                return;

            if (m_settings == null)
                return;
            
            UpdateReflectionCamera(camera, transform); // create reflected camera
            PlanarReflectionTexture(camera); // create and assign RenderTexture

            var data = new PlanarReflectionSettingData(); // save quality settings and lower them for the planar reflections
            data.Set(); // set quality settings

            BeginPlanarReflections?.Invoke(context, _reflectionCamera); // callback Action for PlanarReflection
            UniversalRenderPipeline.RenderSingleCamera(context, _reflectionCamera); // render planar reflections

            data.Restore(); // restore the quality settings
            Shader.SetGlobalTexture(_planarReflectionTextureId, _reflectionTextures[camera]); // Assign texture to water shader
        }

        class PlanarReflectionSettingData
        {
            private readonly bool _fog;
            private readonly int _maxLod;
            private readonly float _lodBias;

            public PlanarReflectionSettingData()
            {
                _fog = RenderSettings.fog;
                _maxLod = QualitySettings.maximumLODLevel;
                _lodBias = QualitySettings.lodBias;
            }

            public void Set()
            {
                GL.invertCulling = true;
                RenderSettings.fog = false; // disable fog for now as it's incorrect with projection
                QualitySettings.maximumLODLevel = 1;
                QualitySettings.lodBias = _lodBias * 0.5f;
            }

            public void Restore()
            {
                GL.invertCulling = false;
                RenderSettings.fog = _fog;
                QualitySettings.maximumLODLevel = _maxLod;
                QualitySettings.lodBias = _lodBias;
            }
        }
    }
}