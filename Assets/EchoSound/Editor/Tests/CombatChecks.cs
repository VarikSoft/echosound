using System;
using UnityEditor;
using UnityEngine;
using EchoSound.Combat;
using EchoSound.UI;

namespace EchoSound.Editor.Tests
{
    /// <summary>
    /// Verification checks for HealthComponent, StaminaComponent, MonsterStats, and prefab wiring.
    /// Accessible via EchoSound/Checks/Validate combat and stats.
    /// </summary>
    public static class CombatChecks
    {
        [MenuItem("EchoSound/Checks/Validate combat and stats")]
        public static void ValidateCombatAndStats()
        {
            ValidateHealth();
            ValidateStamina();
            ValidateMonsterStats();
            ValidatePlayerPrefab();
            Debug.Log("[CombatChecks] All combat and stat checks passed.");
        }

        private static void ValidateHealth()
        {
            var go = new GameObject("TestHealth");
            try
            {
                var health = go.AddComponent<HealthComponent>();
                health.maxHealth = 100f;
                health.currentHealth = 100f;
                health.invulnerabilityDuration = 0f;

                bool died = false;
                health.OnDeath += () => died = true;

                health.TakeDamage(new DamageInfo(30f, DamageType.MonsterBite));
                Require(Mathf.Approximately(health.CurrentHealth, 70f), "Health should be 70 after 30 damage.");
                Require(health.IsAlive, "Entity should still be alive at 70 HP.");

                health.Heal(15f);
                Require(Mathf.Approximately(health.CurrentHealth, 85f), "Health should be 85 after 15 heal.");

                health.TakeDamage(new DamageInfo(200f, DamageType.Generic));
                Require(Mathf.Approximately(health.CurrentHealth, 0f), "Health should clamp at 0.");
                Require(!health.IsAlive, "Entity should be dead at 0 HP.");
                Require(died, "OnDeath event should fire when health reaches 0.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void ValidateStamina()
        {
            var go = new GameObject("TestStamina");
            try
            {
                var stamina = go.AddComponent<StaminaComponent>();
                stamina.maxStamina = 100f;
                stamina.currentStamina = 100f;
                stamina.sprintDrainRate = 50f;
                stamina.jumpCost = 20f;

                Require(stamina.CanSprint, "Should be able to sprint at full stamina.");
                Require(stamina.TryConsumeJump(), "Should be able to jump at full stamina.");
                Require(Mathf.Approximately(stamina.CurrentStamina, 80f), "Stamina should be 80 after jump.");

                stamina.ConsumeSprint(2f); // 100 drain -> clamps to 0 and triggers exhaustion
                Require(Mathf.Approximately(stamina.CurrentStamina, 0f), "Stamina should clamp at 0.");
                Require(stamina.IsExhausted, "Stamina should enter exhausted state at 0.");
                Require(!stamina.CanSprint, "Cannot sprint while exhausted.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void ValidateMonsterStats()
        {
            var monsterGo = new GameObject("TestMonster");
            var playerGo = new GameObject("TestPlayer");
            try
            {
                monsterGo.AddComponent<HealthComponent>();
                var stats = monsterGo.AddComponent<MonsterStats>();
                stats.attackDamage = 25f;
                stats.attackRange = 2.5f;
                stats.attackCooldown = 1.0f;
                monsterGo.transform.position = Vector3.zero;

                var playerHealth = playerGo.AddComponent<HealthComponent>();
                playerHealth.maxHealth = 100f;
                playerHealth.currentHealth = 100f;
                playerHealth.invulnerabilityDuration = 0f;
                playerGo.transform.position = new Vector3(1.5f, 0f, 0f);

                bool hit = stats.TryAttack(playerHealth);
                Require(hit, "Monster within range should successfully attack player.");
                Require(Mathf.Approximately(playerHealth.CurrentHealth, 75f), "Player health should drop to 75 after 25 damage.");

                bool hitOnCooldown = stats.TryAttack(playerHealth);
                Require(!hitOnCooldown, "Monster should not attack again while on cooldown.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(monsterGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
            }
        }

        private static void ValidatePlayerPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EchoSound/Prefabs/Player/FirstPersonPlayer.prefab");
            Require(prefab != null, "FirstPersonPlayer.prefab must exist.");
            Require(prefab.GetComponent<HealthComponent>() != null, "FirstPersonPlayer.prefab missing HealthComponent.");
            Require(prefab.GetComponent<StaminaComponent>() != null, "FirstPersonPlayer.prefab missing StaminaComponent.");
            Require(prefab.GetComponent<PlayerStatsHUD>() != null, "FirstPersonPlayer.prefab missing PlayerStatsHUD.");
            Require(prefab.GetComponent<PauseMenuController>() != null, "FirstPersonPlayer.prefab missing PauseMenuController.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("[CombatChecks] Assertion failed: " + message);
            }
        }
    }
}
