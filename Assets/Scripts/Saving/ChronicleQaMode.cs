#if UNITY_EDITOR || GILDED_CHRONICLE_QA
using System.IO;
using UnityEngine;

namespace GildedFate.Saving
{
    /// <summary>
    /// Isolation for the Chronicle QA build. While Active, every save (profile and run) goes to a separate QA folder, so a tester can
    /// unlock, simulate and reset freely without ever touching the player's real save; Steam is switched off (see GildedSteam);
    /// and the QA panel is available.
    /// Active is always true in the QA build (scripting define GILDED_CHRONICLE_QA, set only by the QA CI build).
    /// In the Unity Editor it is off until switched on from Gilded Fate > Chronicle > QA > Isolated QA Profile (Editor).
    /// This file is not compiled into a normal player build; ci/verify-qa-build.sh proves that on every Windows build.
    /// </summary>
    public static class ChronicleQaMode
    {
        public const string EditorKey = "GildedFate.ChronicleQaIsolation";
        public const string FolderName = "ChronicleQA";

#if GILDED_CHRONICLE_QA
        public static bool Active => true;
        public static bool IsQaBuild => true;
#elif UNITY_EDITOR
        public static bool Active => UnityEditor.EditorPrefs.GetBool(EditorKey, false);
        public static bool IsQaBuild => false;
#endif

        /// <summary>Where the QA profile and QA run save live, inside the game's normal data folder but never beside the real files.</summary>
        public static string SaveDirectory => Path.Combine(Application.persistentDataPath, FolderName);
    }
}
#endif
