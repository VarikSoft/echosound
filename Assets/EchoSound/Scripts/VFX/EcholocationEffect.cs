using UnityEngine;

namespace EchoSound.VFX
{
    /// <summary>
    /// Fullscreen image effect placed on the Player Camera or Security Cameras.
    /// Reconstructs world-space position from the depth buffer and renders echolocation pulses,
    /// glowing grid highlights, and geometric edge detection.
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
        public float masterBrightness = 1.0f;

        [Tooltip("Reveal a faint silhouette of normal textured geometry when hit by a pulse.")]
        [Range(0f, 1f)]
        public float revealOriginalTexture = 0.35f;

        private Material material;
        private Camera targetCamera;

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
        }

        private void OnDisable()
        {
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
            material.SetVector(PropCameraWS, targetCamera.transform.position);
            material.SetFloat(PropMasterBrightness, masterBrightness);
            material.SetFloat(PropRevealTexture, revealOriginalTexture);

            Graphics.Blit(source, destination, material);
        }
    }
}
