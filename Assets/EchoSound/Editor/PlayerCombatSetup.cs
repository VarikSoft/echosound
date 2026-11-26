using System.IO;
using UnityEditor;
using UnityEngine;
using EchoSound.Combat;
using EchoSound.Sound;
using EchoSound.UI;

namespace EchoSound.Editor
{
    /// <summary>
    /// Configures HealthComponent, StaminaComponent, and PlayerStatsHUD
    /// on the FirstPersonPlayer prefab.
    /// </summary>
    public static class PlayerCombatSetup
    {
        private const string PlayerPrefabPath = "Assets/EchoSound/Prefabs/Player/FirstPersonPlayer.prefab";
        private const string MonsterPrefabPath = "Assets/EchoSound/Prefabs/Monsters/StalkerMonster.prefab";
        private const string BreathingCuePath = "Assets/EchoSound/Resources/Audio/Player/Exhausted_Breathing.asset";
        private const string PlayerHurtCuePath = "Assets/EchoSound/Resources/Audio/Player/Player_Hurt.asset";
        private const string PlayerDeathCuePath = "Assets/EchoSound/Resources/Audio/Player/Player_Death.asset";
        private const string StalkerFootstepCuePath = "Assets/EchoSound/Resources/Audio/Monsters/Stalker_Footstep.asset";
        private const string StalkerAlertCuePath = "Assets/EchoSound/Resources/Audio/Monsters/Stalker_Alert.asset";
        private const string StalkerAttackCuePath = "Assets/EchoSound/Resources/Audio/Monsters/Stalker_Attack.asset";

        [InitializeOnLoadMethod]
        private static void AutoInit()
        {
            EditorApplication.delayCall += () =>
            {
                CheckAndWirePlayerPrefab();
                CheckAndCreateMonsterPrefab();
            };
        }

        [MenuItem("EchoSound/Combat/Setup Player Combat and Stats")]
        public static void CheckAndWirePlayerPrefab()
        {
            if (!File.Exists(PlayerPrefabPath)) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            bool modified = false;

            try
            {
                // 1. HealthComponent
                var health = root.GetComponent<HealthComponent>();
                if (health == null)
                {
                    health = root.AddComponent<HealthComponent>();
                    health.maxHealth = 100f;
                    health.currentHealth = 100f;
                    health.invulnerabilityDuration = 0.4f;
                    modified = true;
                    Debug.Log("[PlayerCombatSetup] Added HealthComponent to FirstPersonPlayer prefab.");
                }

                if (health.hurtCue == null)
                {
                    var hurt = AssetDatabase.LoadAssetAtPath<SoundCue>(PlayerHurtCuePath);
                    if (hurt != null) { health.hurtCue = hurt; modified = true; }
                }
                if (health.deathCue == null)
                {
                    var death = AssetDatabase.LoadAssetAtPath<SoundCue>(PlayerDeathCuePath);
                    if (death != null) { health.deathCue = death; modified = true; }
                }

                // 2. StaminaComponent
                var stamina = root.GetComponent<StaminaComponent>();
                if (stamina == null)
                {
                    stamina = root.AddComponent<StaminaComponent>();
                    stamina.maxStamina = 100f;
                    stamina.currentStamina = 100f;
                    stamina.sprintDrainRate = 22f;
                    stamina.jumpCost = 15f;
                    stamina.regenRate = 20f;
                    stamina.regenDelay = 1.2f;
                    stamina.recoveryThreshold = 0.20f;
                    stamina.exhaustedAcousticRadius = 6f;

                    var cue = AssetDatabase.LoadAssetAtPath<SoundCue>(BreathingCuePath);
                    if (cue != null) stamina.exhaustedBreathingCue = cue;

                    modified = true;
                    Debug.Log("[PlayerCombatSetup] Added StaminaComponent to FirstPersonPlayer prefab.");
                }
                else if (stamina.exhaustedBreathingCue == null)
                {
                    var cue = AssetDatabase.LoadAssetAtPath<SoundCue>(BreathingCuePath);
                    if (cue != null)
                    {
                        stamina.exhaustedBreathingCue = cue;
                        modified = true;
                    }
                }

                // 3. PlayerStatsHUD
                var hud = root.GetComponent<PlayerStatsHUD>();
                if (hud == null)
                {
                    hud = root.AddComponent<PlayerStatsHUD>();
                    hud.health = health;
                    hud.stamina = stamina;
                    hud.showHUD = true;
                    modified = true;
                    Debug.Log("[PlayerCombatSetup] Added PlayerStatsHUD to FirstPersonPlayer prefab.");
                }

                // 4. PauseMenuController
                var pause = root.GetComponent<PauseMenuController>();
                if (pause == null)
                {
                    pause = root.AddComponent<PauseMenuController>();
                    pause.health = health;
                    modified = true;
                    Debug.Log("[PlayerCombatSetup] Added PauseMenuController to FirstPersonPlayer prefab.");
                }

                // 5. PlayerDecoyThrower
                var decoy = root.GetComponent<PlayerDecoyThrower>();
                if (decoy == null)
                {
                    decoy = root.AddComponent<PlayerDecoyThrower>();
                    modified = true;
                    Debug.Log("[PlayerCombatSetup] Added PlayerDecoyThrower to FirstPersonPlayer prefab.");
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Debug.Log("[PlayerCombatSetup] Successfully saved FirstPersonPlayer prefab with Combat and Stats components.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("EchoSound/Combat/Setup Stalker Monster Prefab")]
        public static void CheckAndCreateMonsterPrefab()
        {
            if (File.Exists(MonsterPrefabPath))
            {
                GameObject existingRoot = PrefabUtility.LoadPrefabContents(MonsterPrefabPath);
                bool modified = false;
                try
                {
                    var existingStats = existingRoot.GetComponent<MonsterStats>();
                    if (existingStats != null)
                    {
                        if (existingStats.attackCue == null)
                        {
                            existingStats.attackCue = AssetDatabase.LoadAssetAtPath<SoundCue>(StalkerAttackCuePath);
                            if (existingStats.attackCue != null) modified = true;
                        }
                        if (existingStats.alertCue == null)
                        {
                            existingStats.alertCue = AssetDatabase.LoadAssetAtPath<SoundCue>(StalkerAlertCuePath);
                            if (existingStats.alertCue != null) modified = true;
                        }
                    }

                    var existingStalker = existingRoot.GetComponent<StalkerController>();
                    if (existingStalker != null && existingStalker.stepCue == null)
                    {
                        existingStalker.stepCue = AssetDatabase.LoadAssetAtPath<SoundCue>(StalkerFootstepCuePath);
                        if (existingStalker.stepCue != null) modified = true;
                    }

                    if (modified)
                    {
                        PrefabUtility.SaveAsPrefabAsset(existingRoot, MonsterPrefabPath);
                        AssetDatabase.SaveAssets();
                        Debug.Log("[PlayerCombatSetup] Wired audio cues onto StalkerMonster prefab.");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(existingRoot);
                }
                return;
            }

            string folder = Path.GetDirectoryName(MonsterPrefabPath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            var root = new GameObject("StalkerMonster");
            try
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Body";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, 1f, 0f);
                var col = visual.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);

                var cc = root.AddComponent<CharacterController>();
                cc.height = 2f;
                cc.center = new Vector3(0f, 1f, 0f);
                cc.radius = 0.45f;

                var health = root.AddComponent<HealthComponent>();
                health.maxHealth = 120f;
                health.currentHealth = 120f;
                health.invulnerabilityDuration = 0.25f;

                var stamina = root.AddComponent<StaminaComponent>();
                stamina.maxStamina = 100f;
                stamina.currentStamina = 100f;
                stamina.sprintDrainRate = 18f;
                stamina.regenRate = 25f;
                stamina.regenDelay = 1.5f;
                stamina.exhaustedAcousticRadius = 0f;

                var stats = root.AddComponent<MonsterStats>();
                stats.monsterName = "Stalker";
                stats.attackDamage = 25f;
                stats.attackRange = 2.0f;
                stats.attackCooldown = 1.2f;
                stats.hearingSensitivity = 1.25f;
                stats.attackCue = AssetDatabase.LoadAssetAtPath<SoundCue>(StalkerAttackCuePath);
                stats.alertCue = AssetDatabase.LoadAssetAtPath<SoundCue>(StalkerAlertCuePath);

                var stalker = root.AddComponent<StalkerController>();
                stalker.stepCue = AssetDatabase.LoadAssetAtPath<SoundCue>(StalkerFootstepCuePath);

                PrefabUtility.SaveAsPrefabAsset(root, MonsterPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[PlayerCombatSetup] Created StalkerMonster prefab at " + MonsterPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
