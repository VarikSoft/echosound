using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.Combat
{
    /// <summary>
    /// Allows the player to throw key decoys (G or Right-Click) that clatter on impact
    /// and emit loud SoundEvents to distract nearby monsters.
    /// </summary>
    [AddComponentMenu("EchoSound/Combat/Player Decoy Thrower")]
    public class PlayerDecoyThrower : MonoBehaviour
    {
        [Header("Input")]
        public KeyCode throwKey = KeyCode.G;
        public bool allowRightClick = true;

        [Header("Throw Physics")]
        [Min(1f)] public float throwForce = 12f;
        public float upwardBoost = 2.5f;
        [Min(0.1f)] public float throwCooldown = 0.8f;

        [Header("Decoy Supply")]
        [Min(1)] public int maxDecoys = 3;
        [SerializeField] private int currentDecoys = 3;
        [Tooltip("Seconds to recover 1 decoy (0 = no auto-recharge).")]
        [Min(0f)] public float rechargeSeconds = 12f;

        [Header("Audio")]
        public SoundCue impactCue;

        public int CurrentDecoys => currentDecoys;
        public int MaxDecoys => maxDecoys;

        private float nextThrowTime;
        private float rechargeProgress;
        private Collider playerCollider;
        private Camera viewCamera;

        private void Awake()
        {
            currentDecoys = Mathf.Clamp(currentDecoys, 0, maxDecoys);
            playerCollider = GetComponent<Collider>();
            viewCamera = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || Cursor.lockState != CursorLockMode.Locked) return;

            if (rechargeSeconds > 0f && currentDecoys < maxDecoys)
            {
                rechargeProgress += Time.deltaTime;
                if (rechargeProgress >= rechargeSeconds)
                {
                    rechargeProgress -= rechargeSeconds;
                    currentDecoys++;
                }
            }

            bool pressed = Input.GetKeyDown(throwKey) || (allowRightClick && Input.GetMouseButtonDown(1));
            if (pressed && Time.time >= nextThrowTime && currentDecoys > 0)
            {
                ThrowDecoy();
            }
        }

        public bool ThrowDecoy()
        {
            if (currentDecoys <= 0) return false;

            currentDecoys--;
            nextThrowTime = Time.time + throwCooldown;

            Transform origin = viewCamera != null ? viewCamera.transform : transform;
            Vector3 spawnPos = origin.position + origin.forward * 0.65f + Vector3.down * 0.1f;

            var decoyGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            decoyGo.name = "ThrownKeysDecoy";
            decoyGo.transform.position = spawnPos;
            decoyGo.transform.localScale = Vector3.one * 0.16f;

            var renderer = decoyGo.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = new Color(0.85f, 0.72f, 0.25f);
            }

            var col = decoyGo.GetComponent<SphereCollider>();
            if (col != null && playerCollider != null)
            {
                Physics.IgnoreCollision(col, playerCollider);
            }

            var rb = decoyGo.AddComponent<Rigidbody>();
            rb.mass = 0.25f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.linearVelocity = origin.forward * throwForce + Vector3.up * upwardBoost;
            rb.angularVelocity = Random.insideUnitSphere * 12f;

            var proj = decoyGo.AddComponent<DecoyProjectile>();
            if (impactCue != null) proj.impactCue = impactCue;

            return true;
        }
    }
}
