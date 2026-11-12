using UnityEngine;

namespace EchoSound
{
    /// <summary>Standalone animation viewer; no microphone, player controller or sound events.</summary>
    public sealed class ExplorerShowcase : MonoBehaviour
    {
        public Animator animator;
        public Transform model;
        private readonly string[] states = { "Idle", "Walk", "Run", "Jump", "Fall", "Land" };
        private int selected;
        private float elapsed;
        private bool rotate = true;

        private void Update()
        {
            for (int i = 0; i < states.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Select(i);
            if (Input.GetKeyDown(KeyCode.Space)) rotate = !rotate;
            if (rotate) model.Rotate(0, Time.deltaTime * 18, 0);
            elapsed += Time.deltaTime;
            if (selected >= 3 && elapsed > 1.6f) Select(selected);
        }

        private void Select(int index)
        {
            selected = index;
            elapsed = 0;
            animator.Play(states[index], 0, 0);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(24, 24, 350, 155), "ECHO / FIELD EXPLORER");
            GUI.Label(new Rect(40, 55, 320, 24), "1–6: animation     Space: turntable");
            for (int i = 0; i < states.Length; i++)
                if (GUI.Button(new Rect(40 + i % 3 * 106, 88 + i / 3 * 36, 100, 30), states[i])) Select(i);
        }
    }
}
