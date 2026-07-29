using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>
    /// Tuning data for the campaign layer. A ScriptableObject rather than constants so the numbers
    /// can be balanced without a recompile, and so a designer can diff two tuning sets.
    /// Create via Assets > Create > Century > Campaign Settings.
    /// </summary>
    [CreateAssetMenu(menuName = "Century/Campaign Settings", fileName = "CampaignSettings")]
    public sealed class CampaignSettings : ScriptableObject
    {
        [Header("World")]
        [Tooltip("Unity world units that represent one kilometre of overmap terrain.")]
        public float UnitsPerKilometre = 100f;

        [Tooltip("Playable area on the ground plane. Parties spawn and wander inside this.")]
        public Vector2 WorldBoundsMin = new Vector2(-450f, -450f);
        public Vector2 WorldBoundsMax = new Vector2(450f, 450f);

        [Header("Time")]
        [Tooltip("Campaign minutes that elapse per real second at normal speed.")]
        public float MinutesPerRealSecond = 20f;

        [Header("March")]
        [Tooltip("Kilometres per hour for a full-strength, well-fed, high-morale party on open ground.")]
        public float BaseMarchSpeedKph = 4.5f;

        [Tooltip("Designer knob. Global multiplier on how fast EVERY party actually crosses the " +
                 "overmap, applied on top of march speed. Lower = slower, calmer overmap; 1 = raw " +
                 "speed. Takes effect live — tune it in the inspector while playing.")]
        [Range(0.05f, 2f)] public float OvermapSpeedScale = 0.35f;

        [Header("Consumption (per man per day)")]
        public float RationsPerManPerDay = 1f;

        [Header("Attrition")]
        public float StarvationMoralePerHour = 0.012f;
        public float StarvationHealthPerHour = 0.006f;

        [Tooltip("March-stamina drain multiplier during the night hours. Night flight is expensive.")]
        public float NightMarchStaminaMultiplier = 1.75f;

        [Tooltip("Scales how far ENEMY parties detect the player. Below 1 biases the fog of war in " +
                 "the player's favour: you generally see hunters turn toward you before they do. " +
                 "Stealthed stalkers remain the exception, by design.")]
        [Range(0.2f, 1.5f)] public float EnemyDetectionOfPlayerFactor = 0.45f;

        [Header("Ambush")]
        [Tooltip("A hostile revealed inside this range catches the column by surprise.")]
        public float AmbushRevealRange = 48f;

        [Tooltip("Campaign minutes the surprise lasts. A battle joined inside it is an ambush.")]
        public float SurpriseWindowMinutes = 45f;
        public float CampedStaminaRecoveryPerHour = 0.08f;
        public float MarchStaminaCostPerHour = 0.03f;
        public float MarchEquipmentWearPerHour = 0.0015f;

        [Header("Detection and contact")]
        [Tooltip("Distance at which two parties collide and a battle begins. Deliberately much " +
                 "smaller than detection range — the gap between the two is the chase.")]
        public float ContactRadius = 14f;

        [Tooltip("Multiplier on a party's visibility while it is stealthed.")]
        [Range(0.05f, 1f)] public float StealthVisibilityMultiplier = 0.4f;

        [Header("Party detection (designer knobs, applied when a new campaign starts)")]
        [Tooltip("How far the player's century notices other parties, in world units. Feeds the " +
                 "overmap contact warning.")]
        public float PlayerDetectionRadius = 90f;

        [Tooltip("Enemy warband detection range, in world units. Randomised per band between these. " +
                 "Smaller = warbands spot the column later, so fewer surprise pursuits.")]
        public float WarbandDetectionRadiusMin = 55f;
        public float WarbandDetectionRadiusMax = 90f;

        [Tooltip("Enemy warband march-speed modifier vs the player's baseline of 1, randomised per " +
                 "band. Keep the max near or below 1 so the column can actually outrun pursuers.")]
        public float WarbandSpeedModifierMin = 0.85f;
        public float WarbandSpeedModifierMax = 1.05f;

        [Tooltip("Real seconds between encounter and AI evaluations. Cheap, but no need to run per frame.")]
        public float DirectorIntervalSeconds = 0.2f;

        [Header("Overmap AI")]
        [Tooltip("Campaign minutes between idle decisions.")]
        public float AiDecisionIntervalMinutes = 120f;

        [Tooltip("How long a warband will chase before giving up, in campaign minutes.")]
        public float PursuitDurationMinutes = 300f;

        [Tooltip("Contact is suppressed for this many campaign minutes after a battle.")]
        public float PostBattleCooldownMinutes = 240f;

        [Tooltip("Minimum distance of a self-chosen wander destination, in world units.")]
        public float WanderMinDistance = 80f;
        public float WanderMaxDistance = 260f;

        public Vector3 ClampToWorld(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, WorldBoundsMin.x, WorldBoundsMax.x);
            position.z = Mathf.Clamp(position.z, WorldBoundsMin.y, WorldBoundsMax.y);
            return position;
        }
    }
}
