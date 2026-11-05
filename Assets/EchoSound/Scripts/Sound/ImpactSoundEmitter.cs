using UnityEngine;

namespace EchoSound.Sound
{
    /// <summary>Attach to a throwable rigidbody to turn landings and collisions into sound pulses.</summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    [AddComponentMenu("EchoSound/Sound/Impact Sound Emitter")]
    public sealed class ImpactSoundEmitter : MonoBehaviour
    {
        [Min(0f)] public float minimumSpeed = 0.8f;
        [Min(1f)] public float fullStrengthSpeed = 10f;
        [Min(1f)] public float maximumRadius = 24f;
        [Min(0.05f)] public float cooldown = 0.15f;
        private float nextImpact;

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < nextImpact || collision.contactCount == 0) return;
            ContactPoint contact = collision.GetContact(0);
            float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            if (speed < minimumSpeed) return;
            nextImpact = Time.time + cooldown;
            float strength = Mathf.InverseLerp(minimumSpeed, Mathf.Max(minimumSpeed + 0.1f, fullStrengthSpeed), speed);
            SoundBus.Emit(contact.point + contact.normal * 0.03f,
                Mathf.Lerp(3f, maximumRadius, strength), Mathf.Lerp(0.25f, 1.8f, strength) * AcousticSurface.ContactGain(collision.collider),
                SoundType.Impact, gameObject);
        }
    }
}
