using System;
using UnityEngine;

namespace Century.Campaign.Model
{
    /// <summary>Where the guided start stands. None is the free sandbox campaign.</summary>
    public enum OnboardingChapter
    {
        /// <summary>No onboarding: the sandbox start with the full century.</summary>
        None = 0,
        /// <summary>The scripted opening: alone, wounded, in the rain.</summary>
        Opening = 1,
        /// <summary>The Aftermath: the survivors' first hours on the overmap, objectives in order.</summary>
        Aftermath = 2,
        /// <summary>Through the passage: the open world, every system live.</summary>
        OpenWorld = 3
    }

    /// <summary>The Aftermath's objectives, in the order the brief gives them. One at a time.</summary>
    public enum AftermathObjective
    {
        /// <summary>Have twelve men: the battle-site POIs give them up.</summary>
        GatherSurvivors = 0,
        /// <summary>March to the safe place the decanus points out.</summary>
        FindSafePlace = 1,
        /// <summary>Standing on it: press CAMP, and be walked through the camp screen.</summary>
        MakeCamp = 2,
        /// <summary>The scout has found the ravine: guide the party there.</summary>
        ReachPassage = 3,
        /// <summary>Done. The open world is open.</summary>
        Complete = 4
    }

    /// <summary>
    /// Everything the guided start remembers, on the campaign state so it survives scene swaps and
    /// saves. Directors read this to decide what to reveal and what to say; the HUD reads it to
    /// show the objective. Plain fields for JsonUtility.
    /// </summary>
    [Serializable]
    public sealed class OnboardingState
    {
        public OnboardingChapter Chapter = OnboardingChapter.None;
        public AftermathObjective Objective = AftermathObjective.GatherSurvivors;

        /// <summary>Men the survivors' objective asks for.</summary>
        public int SurvivorTarget = 12;

        /// <summary>The Aftermath area: the party is leashed inside it until the passage is taken.</summary>
        public float ZoneMinX, ZoneMinZ, ZoneMaxX, ZoneMaxZ;

        /// <summary>Where the open world begins once the passage is crossed.</summary>
        public Vector3 OpenWorldEntry;

        // --- Story POIs (ids into CampaignState.PointsOfInterest) ---------------------------------
        public string CorpsesPoiId;
        public string HoundsPoiId;
        public string LootersPoiId;
        public string SafePlacePoiId;
        public string PassagePoiId;

        // --- Beats told / done ----------------------------------------------------------------
        /// <summary>The decanus' briefing on first arriving in the Aftermath.</summary>
        public bool ArrivalTold;
        public bool LootersRevealed;
        public bool SurvivorsGatheredTold;
        public bool SafePlaceTold;
        public bool CampPromptTold;
        public bool CampWalkthroughDone;
        public bool PassageTold;
        public bool OpenWorldTold;

        /// <summary>The overmap's own pop-ups, after the decanus has spoken.</summary>
        public bool OvermapTutorialTold;
        public bool StrangersTutorialTold;

        /// <summary>The objectives screen shown straight after the arrival briefing, clock held.</summary>
        public bool ObjectivesIntroTold;

        public bool IsActive => Chapter == OnboardingChapter.Opening || Chapter == OnboardingChapter.Aftermath;
        public bool IsAftermath => Chapter == OnboardingChapter.Aftermath;

        /// <summary>The Aftermath leash. True everywhere once the chapter is done.</summary>
        public bool AllowsPosition(Vector3 position)
        {
            if (Chapter != OnboardingChapter.Aftermath) return true;
            return position.x >= ZoneMinX && position.x <= ZoneMaxX
                   && position.z >= ZoneMinZ && position.z <= ZoneMaxZ;
        }

        /// <summary>The nearest point inside the Aftermath area.</summary>
        public Vector3 ClampToZone(Vector3 position)
        {
            if (Chapter != OnboardingChapter.Aftermath) return position;
            position.x = Mathf.Clamp(position.x, ZoneMinX, ZoneMaxX);
            position.z = Mathf.Clamp(position.z, ZoneMinZ, ZoneMaxZ);
            return position;
        }

        public Vector3 ZoneCentre => new Vector3((ZoneMinX + ZoneMaxX) * 0.5f, 0f, (ZoneMinZ + ZoneMaxZ) * 0.5f);
    }
}
