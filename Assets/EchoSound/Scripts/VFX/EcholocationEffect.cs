using UnityEngine;

namespace EchoSound.VFX
{
    /// <summary>
    /// Fullscreen image effect placed on the Player Camera or Security Cameras.
    /// Reconstructs geometry and renders neon sphere fronts, Fresnel contours and bloom.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [ImageEffectAllowedInSceneView]
    [AddComponentMenu("EchoSound/VFX/Echolocation Effect")]
    public class EcholocationEffect : MonoBehaviour
    {
        [Tooltip("The echolocation post-process shader. If unassigned, EchoSound/Echolocation is loaded automatically.")]
        public Shader echolocationShader;

        [Header("Tuning Overrides")]
        [Tooltip("Master brightness multiplier for echolocation illumination.")]
        [Range(0.1f, 3f)]
        public float masterBrightness = 0.6f;

        [Tooltip("Subtle material detail under the source-colored emissive scan.")]
        [Range(0f, 1f)]
        public float revealOriginalTexture = 0.1f;

        [Range(0f, 1f)] public float bloomStrength = 0.12f;

        private Material material;
        private Camera targetCamera;
        private AcousticSurfaceBuffer surfaceBuffer;

        private static readonly int PropFrustumCornersWS = Shader.PropertyToID("_FrustumCornersWS");
        private static readonly int PropCameraWS = Shader.PropertyToID("_CameraWS");
        private static readonly int PropMasterBrightness = Shader.PropertyToID("_MasterBrightness");
        private static readonly int PropRevealTexture = Shader.PropertyToID("_RevealTexture");

        private void OnEnable()
        {
            targetCamera = GetComponent<Camera>();
            if (targetCamera != null)
            {
                targetCamera.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.DepthNormals;
            }
            EnsureMaterial();
            surfaceBuffer = new AcousticSurfaceBuffer(targetCamera);
        }

        private void OnPreRender() => surfaceBuffer?.Prepare();

        private void OnDisable()
        {
            surfaceBuffer?.Dispose();
            surfaceBuffer = null;
            if (material != null)
            {
                DestroyImmediate(material);
                material = null;
            }
        }

        private void EnsureMaterial()
        {
            if (material == null)
            {
                if (echolocationShader == null)
                {
                    echolocationShader = Shader.Find("EchoSound/Echolocation");
                }

                if (echolocationShader != null && echolocationShader.isSupported)
                {
                    material = new Material(echolocationShader)
                    {
                        hideFlags = HideFlags.DontSave
                    };
                }
            }
        }

        [ImageEffectOpaque]
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            EnsureMaterial();

            if (material == null || targetCamera == null || !echolocationShader.isSupported)
            {
                Graphics.Blit(source, destination);
                return;
            }

            // Reconstruct frustum corner rays for depth -> world-space position decoding
            float fov = targetCamera.fieldOfView;
            float aspect = targetCamera.aspect;
            float halfHeight = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            Vector3 toRight = targetCamera.transform.right * (halfHeight * aspect);
            Vector3 toTop = targetCamera.transform.up * halfHeight;
            Vector3 forward = targetCamera.transform.forward;

            Vector3 topLeft = forward - toRight + toTop;
            Vector3 topRight = forward + toRight + toTop;
            Vector3 bottomRight = forward + toRight - toTop;
            Vector3 bottomLeft = forward - toRight - toTop;

            Matrix4x4 frustumCorners = Matrix4x4.identity;
            frustumCorners.SetRow(0, topLeft);
            frustumCorners.SetRow(1, topRight);
            frustumCorners.SetRow(2, bottomRight);
            frustumCorners.SetRow(3, bottomLeft);

            material.SetMatrix(PropFrustumCornersWS, frustumCorners);
            material.SetMatrix("_EchoViewToWorld", targetCamera.cameraToWorldMatrix);
            material.SetVector(PropCameraWS, targetCamera.transform.position);
            material.SetFloat(PropMasterBrightness, masterBrightness);
            material.SetFloat(PropRevealTexture, revealOriginalTexture);
            material.SetTexture("_EchoAcousticMap", surfaceBuffer != null ? surfaceBuffer.Texture : Texture2D.whiteTexture);

            if (bloomStrength <= 0f)
            {
                Graphics.Blit(source, destination, material, 0);
                return;
            }
            var echo = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
            var glowA = RenderTexture.GetTemporary(Mathf.Max(1, source.width / 2), Mathf.Max(1, source.height / 2), 0, source.format);
            var glowB = RenderTexture.GetTemporary(glowA.width, glowA.height, 0, source.format);
            echo.filterMode = glowA.filterMode = glowB.filterMode = FilterMode.Bilinear;
            try
            {
                Graphics.Blit(source, echo, material, 0);
                material.SetFloat("_ExtractGlow", 1f);
                material.SetVector("_BlurDirection", new Vector4(1, 0, 0, 0));
                Graphics.Blit(echo, glowA, material, 1);
                material.SetFloat("_ExtractGlow", 0f);
                material.SetVector("_BlurDirection", new Vector4(0, 1, 0, 0));
                Graphics.Blit(glowA, glowB, material, 1);
                material.SetVector("_BlurDirection", new Vector4(2, 0, 0, 0));
                Graphics.Blit(glowB, glowA, material, 1);
                material.SetVector("_BlurDirection", new Vector4(0, 2, 0, 0));
                Graphics.Blit(glowA, glowB, material, 1);
                material.SetTexture("_EchoBloom", glowB);
                material.SetFloat("_BloomStrength", bloomStrength);
                Graphics.Blit(echo, destination, material, 2);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(echo);
                RenderTexture.ReleaseTemporary(glowA);
                RenderTexture.ReleaseTemporary(glowB);
            }
        }
    }
}
