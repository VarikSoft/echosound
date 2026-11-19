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
        private const string BreathingCuePath = "Assets/EchoSound/Resources/Audio/Player/Exhausted_Breathing.asset";

        [InitializeOnLoadMethod]
        private static void AutoInit()
        {
            EditorApplication.delayCall += () =>
            {
                CheckAndWirePlayerPrefab();
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
    }
}
