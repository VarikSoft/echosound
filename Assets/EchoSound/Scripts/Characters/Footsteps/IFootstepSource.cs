using System;

namespace EchoSound.Characters
{
    /// <summary>Reports foot contacts. Consumers (audio, effects, perception) do not care what produces them.</summary>
    public interface IFootstepSource
    {
        event Action<FootstepEvent> Footstep;
    }
}
