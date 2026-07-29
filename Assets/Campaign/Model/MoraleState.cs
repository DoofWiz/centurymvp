using System;
using UnityEngine;

namespace Century.Campaign.Model
{
    /// <summary>The morale ladder, shared language with the battle layer's cohesion bands.</summary>
    public enum MoraleBand
    {
        Broken = 0,
        Breaking = 1,
        Wavering = 2,
        Confident = 3,
        Fearless = 4
    }

    /// <summary>Party morale, displayed by its band name alone — the men are "Wavering", not "38%".</summary>
    [Serializable]
    public sealed class MoraleState
    {
        [Range(0f, 1f)] public float Value01 = 0.6f;

        public MoraleBand Band
        {
            get
            {
                if (Value01 < 0.15f) return MoraleBand.Broken;
                if (Value01 < 0.35f) return MoraleBand.Breaking;
                if (Value01 < 0.55f) return MoraleBand.Wavering;
                if (Value01 < 0.80f) return MoraleBand.Confident;
                return MoraleBand.Fearless;
            }
        }

        public void Adjust(float delta) => Value01 = Mathf.Clamp01(Value01 + delta);

        public string ToDisplayString() => Band.ToString();
    }
}
