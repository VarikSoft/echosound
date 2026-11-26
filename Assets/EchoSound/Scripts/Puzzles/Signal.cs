using System;
using UnityEngine;

namespace EchoSound.Puzzles
{
    /// <summary>
    /// An on/off output shared by every mechanism: buttons, plates and laser receivers write it,
    /// gates combine it, doors and movers react to it. Receivers depend only on this, not on what drives it.
    /// One signal per GameObject; put a gate on its own object.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Signal : MonoBehaviour
    {
        [SerializeField] private bool isOn;

        public bool IsOn => isOn;
        public event Action<bool> Changed;

        public void Set(bool on)
        {
            if (on == isOn) return;
            isOn = on;
            Changed?.Invoke(on);
        }
    }
}
