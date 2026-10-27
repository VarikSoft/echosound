using UnityEngine;
using EchoSound.Generation;

namespace EchoSound.Testing
{
    /// <summary>
    /// Test-scene wiring: initial generation, the R shortcut, and recovery after falling.
    /// Layout generation and player movement live in separate classes.
    /// </summary>
    public sealed class GenerationPlayground : MonoBehaviour
    {
        public LevelGenerator generator;
        public FirstPersonPlayer player;
        [Tooltip("Falling this far below the level returns the player to the spawn point.")]
        public float fallDepth = 6f;

        private void OnEnable()
        {
            if (generator != null) generator.Generated += PlacePlayerAtStart;
        }

        private void OnDisable()
        {
            if (generator != null) generator.Generated -= PlacePlayerAtStart;
        }

        private void Start()
        {
            generator.Generate(generator.seed);
        }

        private void Update()
        {
            if (generator.Current == null) return;

            if (Cursor.lockState == CursorLockMode.Locked && Input.GetKeyDown(KeyCode.R))
            {
                generator.Generate(unchecked(generator.seed + 1));
            }

            bool fellIntoPit = player.transform.position.y < generator.transform.position.y - fallDepth;
            if (fellIntoPit || Input.GetKeyDown(KeyCode.T))
            {
                PlacePlayerAtStart(generator.Current);
            }
        }

        private void PlacePlayerAtStart(GeneratedLevel level)
        {
            player.Teleport(level.Spawn + Vector3.up * 0.1f);
        }
    }
}
