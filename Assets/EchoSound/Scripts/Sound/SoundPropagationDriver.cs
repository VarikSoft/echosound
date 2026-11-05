using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoSound.Sound
{
    [AddComponentMenu("")]
    public sealed class SoundPropagationDriver : MonoBehaviour
    {
        private void OnEnable() => SceneManager.sceneUnloaded += OnSceneUnloaded;
        private void OnDisable() => SceneManager.sceneUnloaded -= OnSceneUnloaded;
        private void Update() => SoundBus.Advance(Time.time);
        private void OnSceneUnloaded(Scene scene) => SoundBus.ClearPending();
    }
}
