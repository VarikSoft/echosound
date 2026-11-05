using System;
using UnityEngine;
using UnityEngine.Rendering;
using EchoSound.Sound;

namespace EchoSound.VFX
{
    /// <summary>Per-camera response map; acoustic profiles do not replace visual materials.</summary>
    internal sealed class AcousticSurfaceBuffer : IDisposable
    {
        private readonly Camera camera;
        private readonly CommandBuffer commands;
        private readonly Material material;
        private RenderTexture texture;
        public Texture Texture => texture != null ? (Texture)texture : Texture2D.whiteTexture;

        public AcousticSurfaceBuffer(Camera owner)
        {
            camera = owner;
            var shader = Resources.Load<Shader>("AcousticResponse");
            if (shader == null || !shader.isSupported) return;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            commands = new CommandBuffer { name = "EchoSound acoustic surface response" };
            camera.AddCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, commands);
        }

        public void Prepare()
        {
            if (commands == null) return;
            int width = Mathf.Max(1, camera.pixelWidth), height = Mathf.Max(1, camera.pixelHeight);
            if (texture == null || texture.width != width || texture.height != height)
            {
                if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
                texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                { name = "Acoustic response", filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                texture.Create();
            }
            commands.Clear();
            commands.SetRenderTarget(texture);
            commands.ClearRenderTarget(false, true, new Color(1, 1, 0, 0));
            commands.SetGlobalVector("_EchoAbyssFade", Vector4.zero);
            foreach (AcousticSurface surface in AcousticSurface.Active)
            {
                if (surface == null || !surface.isActiveAndEnabled || surface.profile == null) continue;
                var profile = surface.profile;
                commands.SetGlobalVector("_AcousticDrawResponse", new Vector4(Mathf.Clamp01(profile.reflection),
                    Mathf.Clamp(profile.echoPersistence, 0.05f, 1), (int)profile.kind / 10f, Mathf.Clamp01(profile.hintStrength)));
                foreach (Renderer renderer in surface.Renderers)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    if ((camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                    if (AcousticSurface.Find(renderer) != surface) continue;
                    for (int submesh = 0; submesh < renderer.sharedMaterials.Length; submesh++)
                        commands.DrawRenderer(renderer, material, submesh);
                }
            }
            // Abyss geometry remains opaque; extinction is applied after the surface echo calculation.
            commands.SetGlobalVector("_AcousticDrawResponse", new Vector4(0, 1, 0, 0));
            foreach (EchoVoidSurface abyss in EchoVoidSurface.Active)
            {
                if (abyss == null || !abyss.isActiveAndEnabled) continue;
                commands.SetGlobalVector("_EchoAbyssFade", new Vector4(abyss.RimHeight,
                    Mathf.Max(0, abyss.fadeStartDepth),
                    Mathf.Max(abyss.fadeStartDepth + 0.1f, abyss.fadeEndDepth), abyss.useDepthFade ? 1 : 0));
                foreach (Renderer renderer in abyss.Renderers)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    if ((camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                    for (int submesh = 0; submesh < renderer.sharedMaterials.Length; submesh++)
                        commands.DrawRenderer(renderer, material, submesh);
                }
            }
            commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        }

        public void Dispose()
        {
            if (commands != null)
            {
                if (camera != null) camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, commands);
                commands.Release();
            }
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
