using UnityEngine;

namespace EchoSound.Combat
{
    public enum DamageType
    {
        Generic = 0,
        Physical = 1,
        MonsterBite = 2,
        Fall = 3,
        Environmental = 4
    }

    [System.Serializable]
    public struct DamageInfo
    {
        public float amount;
        public DamageType type;
        public GameObject source;
        public Vector3 hitPoint;
        public Vector3 hitNormal;

        public DamageInfo(float amount, DamageType type = DamageType.Generic, GameObject source = null, Vector3 hitPoint = default, Vector3 hitNormal = default)
        {
            this.amount = amount;
            this.type = type;
            this.source = source;
            this.hitPoint = hitPoint;
            this.hitNormal = hitNormal;
        }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        float CurrentHealth { get; }
        float MaxHealth { get; }
        void TakeDamage(DamageInfo damage);
        void Heal(float amount);
    }
}
