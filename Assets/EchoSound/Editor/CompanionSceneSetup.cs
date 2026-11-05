using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using EchoSound.Companion;
using EchoSound.Generation;
using EchoSound.Interaction;
using EchoSound.Testing;

namespace EchoSound.Editor
{
    /// <summary>Creates the companion prefab and a companion test scene based on the generation test scene.</summary>
    public static class CompanionSceneSetup
    {
        private const string SourceScene = "Assets/EchoSound/Scenes/GenerationTest.unity";
        private const string TargetScene = "Assets/EchoSound/Scenes/CompanionTest.unity";
        private const string PrefabFolder = "Assets/EchoSound/Prefabs/Companion";
        private const string PrefabPath = PrefabFolder + "/AICompanion.prefab";
        private const string Materials = "Assets/EchoSound/Materials/";

        [MenuItem("EchoSound/Create companion test scene")]
        public static void CreateScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene) != null &&
                !EditorUtility.DisplayDialog("Companion test scene", TargetScene + " already exists. Replace it?", "Replace", "Cancel"))
            {
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            // A prefab from before the shared interaction rules has no Interactor; rebuild it.
            if (prefab == null || prefab.GetComponent<Interactor>() == null) prefab = CreateCompanionPrefab();

            Scene scene = EditorSceneManager.OpenScene(SourceScene);
            var generator = Object.FindAnyObjectByType<LevelGenerator>();
            var player = Object.FindAnyObjectByType<FirstPersonPlayer>();
            if (generator == null || player == null)
            {
                Debug.LogError(SourceScene + " needs a LevelGenerator and a FirstPersonPlayer.");
                return;
            }

            var companionObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var brain = companionObject.GetComponent<CompanionBrain>();
            var body = brain.GetComponent<CompanionAgent>();
            body.curator = player.transform;
            // Script changes to a prefab instance are lost on save unless recorded as overrides.
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);

            // The player gets the same Interactor as the companion. Added only to this scene's instance;
            // apply the override to FirstPersonPlayer.prefab to give the player hands everywhere.
            if (player.GetComponent<PlayerInteractor>() == null)
            {
                var hands = Undo.AddComponent<PlayerInteractor>(player.gameObject);
                hands.view = player.view;
                player.GetComponent<Interactor>().displayName = "Player";
            }

            var controls = new GameObject("Companion Test Controls - Enter talk, E use door");
            var playground = controls.AddComponent<CompanionPlayground>();
            playground.generator = generator;
            playground.player = player;
            playground.companion = brain;
            playground.doorMaterial = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Door.mat");
            controls.AddComponent<CuratorChat>().companion = brain;

            // Saving under a new path leaves GenerationTest unchanged.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TargetScene);
            Debug.Log("Created " + TargetScene + ". Run Ollama, press Play, then press Enter to give an order.");
        }

        private static GameObject CreateCompanionPrefab()
        {
            var root = new GameObject("AI Companion");
            var agent = root.AddComponent<NavMeshAgent>();
            // Matches the radius CompanionPlayground bakes with; pit walkways are 1.6 m wide.
            agent.radius = 0.3f;
            agent.height = 1.6f;
            agent.speed = 3.5f;
            agent.angularSpeed = 540;
            agent.acceleration = 12;
            // Enabled by CompanionAgent.Place() once a NavMesh exists.
            agent.enabled = false;
            root.AddComponent<CompanionAgent>();
            root.AddComponent<CompanionPerception>();
            root.AddComponent<Interactor>().displayName = "Companion";
            root.AddComponent<CompanionBrain>();

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0, 0.8f, 0);
            body.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
            body.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Materials + "PuzzleTeal.mat");

            // Shows which way the companion faces.
            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Visor";
            visor.transform.SetParent(root.transform, false);
            visor.transform.localPosition = new Vector3(0, 1.3f, 0.25f);
            visor.transform.localScale = new Vector3(0.4f, 0.12f, 0.12f);
            visor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Materials + "SecretGold.mat");
            Object.DestroyImmediate(visor.GetComponent<BoxCollider>());

            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/EchoSound/Prefabs", "Companion");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
