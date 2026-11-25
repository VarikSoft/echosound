using System;
using UnityEngine;

namespace EchoSound.Characters
{
    public enum CharacterAnimationSlot { Idle, Walk, Run, Jump, Fall, Land }

    /// <summary>One clip per slot. An empty slot means "not set".</summary>
    [Serializable]
    public sealed class CharacterAnimationClips
    {
        public const int SlotCount = 6;

        public AnimationClip idle;
        public AnimationClip walk;
        public AnimationClip run;
        public AnimationClip jump;
        public AnimationClip fall;
        public AnimationClip land;

        public AnimationClip this[CharacterAnimationSlot slot]
        {
            get => slot switch
            {
                CharacterAnimationSlot.Idle => idle,
                CharacterAnimationSlot.Walk => walk,
                CharacterAnimationSlot.Run => run,
                CharacterAnimationSlot.Jump => jump,
                CharacterAnimationSlot.Fall => fall,
                _ => land
            };
            set
            {
                switch (slot)
                {
                    case CharacterAnimationSlot.Idle: idle = value; break;
                    case CharacterAnimationSlot.Walk: walk = value; break;
                    case CharacterAnimationSlot.Run: run = value; break;
                    case CharacterAnimationSlot.Jump: jump = value; break;
                    case CharacterAnimationSlot.Fall: fall = value; break;
                    default: land = value; break;
                }
            }
        }
    }
}
