using UnityEngine;
using EchoSound.Sound;
using EchoSound.VFX;

namespace EchoSound.UI
{
    /// <summary>A short identification hint, shown only while the aimed surface has an active echo.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class AcousticMaterialReadout : MonoBehaviour
    {
        [Min(0.5f)] public float readDistance = 5;
        [Min(0.0001f)] public float visibilityThreshold = 0.002f;
        public bool showMaterialName = true;
        public string CurrentLabel { get; private set; } = "";
        private Camera view;
        private GUIStyle style;
        private readonly RaycastHit[] hits = new RaycastHit[16];

        private void Awake() => view = GetComponent<Camera>();
        private void Update() => Refresh();

        /// <summary>Optional sample time supports deterministic reveal checks.</summary>
        public void Refresh(float sampleTime = -1)
        {
            CurrentLabel = "";
            if (view == null) view = GetComponent<Camera>();
            if (!showMaterialName || view == null || !view.enabled) return;
            var effect = GetComponent<EcholocationEffect>();
            var manager = EcholocationManager.Instance;
            if (effect == null || !effect.isActiveAndEnabled || manager == null) return;
            Ray ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            int count = Physics.RaycastNonAlloc(ray, hits, readDistance, SoundBus.ObstacleMask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return;
            int nearest = -1;
            float distance = float.MaxValue;
            var playerBody = GetComponentInParent<CharacterController>();
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider == playerBody) continue;
                if (hits[i].distance < distance) { distance = hits[i].distance; nearest = i; }
            }
            if (nearest < 0) return;
            var surface = AcousticSurface.Find(hits[nearest].collider);
            if (surface == null || surface.profile.kind == AcousticMaterialKind.Unmarked) return;
            if (manager.SurfaceVisibility(hits[nearest].point, surface.profile, sampleTime) < visibilityThreshold) return;
            CurrentLabel = surface.profile.displayName + "  ·  " + surface.profile.playerHint;
        }

        private void OnDisable() => CurrentLabel = "";
        private void OnGUI()
        {
            if (string.IsNullOrEmpty(CurrentLabel)) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            GUI.Label(new Rect(12, Screen.height * 0.64f, Screen.width - 24, 35), CurrentLabel, style);
        }
    }
}
