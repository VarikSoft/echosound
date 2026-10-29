namespace EchoSound.Sound
{
    /// <summary>
    /// Categories of sounds emitted in EchoSound.
    /// Used by echolocation VFX, enemy perception, and audio playback.
    /// </summary>
    public enum SoundType
    {
        Generic = 0,
        FootstepWalk = 1,
        FootstepRun = 2,
        Jump = 3,
        Land = 4,
        Interaction = 5,
        CameraPing = 6,
        Voice = 7,
        Impact = 8,
        Enemy = 9,
        Custom = 10
    }
}
