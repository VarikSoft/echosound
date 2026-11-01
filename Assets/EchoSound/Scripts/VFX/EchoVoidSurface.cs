using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.VFX
{
    /// <summary>Opaque abyss geometry with optional smooth depth extinction; the bottom stays hidden.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class EchoVoidSurface : MonoBehaviour
    {
        public bool useDepthFade;
        [Tooltip("Floor-level reference. Defaults to this object's world height.")]
        public Transform rim;
        [Min(0)] public float fadeStartDepth = 0.75f;
        [Min(1)] public float fadeEndDepth = 16;
        public float RimHeight => rim != null ? rim.position.y : transform.position.y;
        private static readonly HashSet<EchoVoidSurface> active = new HashSet<EchoVoidSurface>();
        public static IEnumerable<EchoVoidSurface> Active => active;
        private Renderer[] renderers;
        public Renderer[] Renderers => renderers ?? (renderers = GetComponentsInChildren<Renderer>(true));
        private void OnEnable() { renderers = null; active.Add(this); }
        private void OnDisable() => active.Remove(this);
        private void OnTransformChildrenChanged() => renderers = null;
    }
}
