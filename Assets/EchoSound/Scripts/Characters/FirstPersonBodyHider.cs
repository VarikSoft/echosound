using UnityEngine;
using UnityEngine.Rendering;

namespace EchoSound.Characters
{
    /// <summary>Hides the body from its owner's camera only. Shadows and other views keep it.</summary>
    public sealed class FirstPersonBodyHider : MonoBehaviour
    {
        public Camera ownerCamera;
        public Renderer[] renderers;

        private ShadowCastingMode[] original;
        private bool hidden;

        private void OnEnable()
        {
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
            original = new ShadowCastingMode[renderers.Length];
            Camera.onPreCull += Hide;
            Camera.onPostRender += Restore;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= Hide;
            Camera.onPostRender -= Restore;
            Restore(ownerCamera);
        }

        private void Hide(Camera camera)
        {
            if (camera != ownerCamera || hidden) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                original[i] = renderers[i].shadowCastingMode;
                renderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            hidden = true;
        }

        private void Restore(Camera camera)
        {
            if (camera != ownerCamera || !hidden) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].shadowCastingMode = original[i];
            hidden = false;
        }
    }
}
