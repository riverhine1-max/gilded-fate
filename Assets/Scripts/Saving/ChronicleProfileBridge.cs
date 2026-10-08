using System;
using System.Collections.Generic;
using GildedFate.Chronicle;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.Saving
{
    /// <summary>
    /// The only place the game's real progression touches the Chronicle. Each call forwards one milestone to the player's
    /// ChronicleProgress (stored inside the existing PlayerProfile). The game saves the profile right after these hooks,
    /// exactly as it already does for its own statistics, so there is no second save path.
    /// </summary>
    public static class ChronicleHooks
    {
        public static ChronicleProgress Of(PlayerProfile profile)
        {
            profile.EnsureMeta();
            return profile.chronicle;
        }

        /// <summary>Call once after the profile loads: repairs missing fields and, a single time, credits runs the player
        /// finished before the Chronicle existed. Returns true when the profile changed and should be saved.</summary>
        public static bool Prepare(PlayerProfile profile)
        {
            var chronicle = Of(profile);
            if (chronicle.legacyMigrated) return false;
            var history = new List<ChronicleLegacyRun>();
            if (profile.runHistory != null)
                foreach (var record in profile.runHistory)
                    if (Enum.TryParse(record.hero, out HeroId hero))
                        history.Add(new ChronicleLegacyRun { hero = (int)hero, act = record.act, floor = record.floor, victory = record.victory });
            chronicle.SeedFromLegacy(history, profile.winsByHero);
            return true;
        }

        /// <summary>Routes Chronicle warnings (missing art, bad scene data) to the Unity console.</summary>
        public static void BindLogging()
        {
            ChronicleLog.Warn = message => Debug.LogWarning("[Chronicle] " + message);
            ChronicleLog.Info = message => Debug.Log("[Chronicle] " + message);
        }

        /// <summary>A combat was won. A boss kill credits that act; any win credits the floor reached.</summary>
        public static ChronicleUnlockResult CombatWon(PlayerProfile profile, HeroId hero, int act, bool boss, int overallFloor)
        {
            var chronicle = Of(profile);
            return boss ? chronicle.RegisterBossDefeated((ChronicleHero)(int)hero, act, overallFloor) : chronicle.RegisterFloorReached((ChronicleHero)(int)hero, overallFloor);
        }

        /// <summary>A run ended, by victory or defeat. Counted once per run ID.</summary>
        public static ChronicleUnlockResult RunEnded(PlayerProfile profile, HeroId hero, bool victory, int act, int overallFloor, string runId)
        {
            return Of(profile).RegisterRunEnded((ChronicleHero)(int)hero, victory, act, overallFloor, runId);
        }

        public static void EventEncountered(PlayerProfile profile, string eventId) { Of(profile).RegisterEventEncountered(eventId); }
    }
}
