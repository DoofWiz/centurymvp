namespace Century.Core.Ui
{
    /// <summary>
    /// Easing curves for UI motion.
    /// </summary>
    /// <remarks>
    /// Kept separate from the tween helpers so curves can be reused by anything — world-space
    /// animation, camera moves, audio fades — without dragging in UIElements.
    ///
    /// The defaults matter for feel: numbers counting up want <see cref="OutCubic"/> so they decelerate
    /// into their final value, bars want <see cref="OutQuad"/>, and anything appearing wants
    /// <see cref="OutBack"/> for a slight overshoot that reads as physical.
    /// </remarks>
    public static class Easing
    {
        public static float Linear(float t) => t;

        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);

        public static float OutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float InOutCubic(float t) =>
            t < 0.5f ? 4f * t * t * t : 1f - Pow3(-2f * t + 2f) * 0.5f;

        /// <summary>Slight overshoot past the target before settling. Good for things appearing.</summary>
        public static float OutBack(float t)
        {
            const float overshoot = 1.70158f;
            float inv = t - 1f;
            return 1f + (overshoot + 1f) * inv * inv * inv + overshoot * inv * inv;
        }

        /// <summary>Rings briefly around the target. Use sparingly — for promotions and the like.</summary>
        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            const float period = 0.3f;
            const float twoPi = 6.28318548f;
            float s = period / 4f;

            return Pow2Neg10(t) * Sin((t - s) * twoPi / period) + 1f;
        }

        private static float Pow3(float v) => v * v * v;

        private static float Pow2Neg10(float t) => (float)System.Math.Pow(2d, -10d * t);

        private static float Sin(float v) => (float)System.Math.Sin(v);
    }
}
