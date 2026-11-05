using UnityEngine;

namespace EchoSound.Sound
{
    public enum AcousticMaterialKind { Unmarked, Metal, Concrete, Wood, Soft }

    /// <summary>Gameplay tuning shared by walls, floors and props; not a frequency-domain simulation.</summary>
    [CreateAssetMenu(menuName = "EchoSound/Acoustic material")]
    public sealed class AcousticMaterial : ScriptableObject
    {
        [Header("Identification during an echo")]
        public AcousticMaterialKind kind;
        public string displayName = "";
        public string playerHint = "";
        [Range(0, 1)] public float hintStrength = 0.7f;

        [Tooltip("Pressure retained by a single reflection. Zero absorbs the reflected sound.")]
        [Range(0, 1)] public float reflection = 0.65f;
        [Tooltip("Extra exponential loss per metre along a reflected path.")]
        [Range(0, 1)] public float reflectionDamping = 0.02f;
        [Tooltip("Surface echo memory relative to the normal trail duration.")]
        [Range(0.05f, 1)] public float echoPersistence = 0.7f;
        [Tooltip("Pressure multiplier for footsteps, jumping and impacts on this surface.")]
        [Range(0, 2)] public float contactLoudness = 1;

        public float ReflectionGain(float pathLength)
        {
            return Mathf.Clamp01(reflection) * Mathf.Exp(-Mathf.Max(0, reflectionDamping) * Mathf.Max(0, pathLength));
        }
    }
}
