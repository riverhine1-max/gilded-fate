using System.IO;
using GildedFate.Saving;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    /// <summary>
    /// Gilded Fate > Chronicle > QA. Editor only (this file lives under Assets/Editor, so it is never part of a build).
    /// The QA build always keeps its test progress in its own folder. In the Editor that isolation is OFF by default, so normal editor
    /// work keeps using your usual save; switch it on here BEFORE entering Play Mode to try the Chronicle QA panel (F10 in the main menu)
    /// against a separate QA profile, with Steam blocked. Nothing here can change what the CI QA build does.
    /// </summary>
    public static class GildedChronicleQaMenu
    {
        const string Root = "Gilded Fate/Chronicle/QA/";
        const string Toggle = Root + "Isolated QA Profile (Editor)";

        [MenuItem(Toggle)]
        static void FlipIsolation()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Chronicle QA", "Stop Play Mode first. The save location is chosen when the game starts.", "OK"); return; }
            EditorPrefs.SetBool(ChronicleQaMode.EditorKey, !EditorPrefs.GetBool(ChronicleQaMode.EditorKey, false));
        }

        [MenuItem(Toggle, true)]
        static bool FlipIsolationValidate() { Menu.SetChecked(Toggle, EditorPrefs.GetBool(ChronicleQaMode.EditorKey, false)); return !EditorApplication.isPlaying; }

        [MenuItem(Root + "Show QA Save Folder")]
        static void ShowFolder() { Directory.CreateDirectory(ChronicleQaMode.SaveDirectory); EditorUtility.RevealInFinder(ChronicleQaMode.SaveDirectory); }

        [MenuItem(Root + "Delete QA Save Folder")]
        static void DeleteFolder()
        {
            var path = ChronicleQaMode.SaveDirectory;
            if (!Directory.Exists(path)) { Debug.Log("[Chronicle QA] No QA save folder yet: " + path); return; }
            if (!EditorUtility.DisplayDialog("Delete QA save folder", "Delete the Chronicle QA profile and QA run save?\n\n" + path + "\n\nYour real profile is a different folder and is not touched.", "Delete", "Cancel")) return;
            Directory.Delete(path, true);
            Debug.Log("[Chronicle QA] Deleted " + path);
        }
    }
}
