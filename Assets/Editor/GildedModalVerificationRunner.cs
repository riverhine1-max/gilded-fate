using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GildedFate.ChronicleBook;
using GildedFate.Saving;
using GildedFate.UI;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    [InitializeOnLoad] public static class GildedModalVerificationRunner
    {
        const string Request="Library/AdminRewardVerification.request",Pending="GildedFate.AdminRewardVerification.Pending";
        const string Output=@"C:\Users\river\Documents\Codex\2026-10-08\use-these-files-from-the-libray-4\outputs\Admin_Reward_Verification.json";
        static double readyAt;static bool running;
        static GildedModalVerificationRunner()
        {
            if(SessionState.GetBool(Pending,false))ProfileService.Suspended=SaveService.Suspended=true;
            EditorApplication.update+=Wait;
            EditorApplication.delayCall+=()=>{if(!EditorApplication.isPlayingOrWillChangePlaymode)Cleanup();};
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode)return;
                Cleanup();
                ProfileService.Suspended=SessionState.GetBool(Pending+".Profile",false);SaveService.Suspended=SessionState.GetBool(Pending+".Run",false);
                readyAt=0;running=false;SessionState.SetBool(Pending,false);
            };
        }
        static void Cleanup()
        {
            foreach(var probe in Resources.FindObjectsOfTypeAll<GildedModalInputVerification>())
                if(probe&&probe.gameObject.scene.IsValid())UnityEngine.Object.DestroyImmediate(probe.gameObject);
        }
        internal static void BackupPlayerSaves()
        {
            var backup=Path.Combine("Library","VerificationSaveBackups",DateTime.UtcNow.Ticks.ToString());
            Directory.CreateDirectory(backup);
            foreach(var name in new[]{"gilded_fate_profile.json","gilded_fate_profile.json.bak","gilded_fate_run.json","gilded_fate_run.json.bak"})
            {
                var source=Path.Combine(SaveService.DirectoryPath,name);
                if(File.Exists(source))File.Copy(source,Path.Combine(backup,name));
            }
        }
        [MenuItem("Gilded Fate/Verify Admin and Reward Input")]
        static void RequestVerification()=>File.WriteAllText(Request,"Native GUI checks on an isolated profile; never reset the player's save.");
        static void Wait()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(SessionState.GetBool(Pending,false))
            {
                if(!EditorApplication.isPlaying||EditorApplication.isPaused||running)return;
                if(readyAt==0){readyAt=EditorApplication.timeSinceStartup+5;return;}
                if(EditorApplication.timeSinceStartup<readyAt)return;
                running=true;
                try
                {
                    foreach(var old in UnityEngine.Object.FindObjectsByType<GildedMainMenu>())
                    {
                        old.enabled=false;old.StopAllCoroutines();
                        (typeof(GildedMainMenu).GetField("chronicleHost",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(old) as ChronicleBookStageHost)?.SetActive(false);
                        old.GetComponent<ChronicleDialogueAudio>()?.Stop();
                    }
                    var host=new GameObject("Admin and reward input verification (isolated)"){hideFlags=HideFlags.HideInHierarchy};
                    var menu=host.AddComponent<GildedMainMenu>();menu.enabled=false;
                    var mode=SessionState.GetString(Pending+".Mode","");
                    if(mode.StartsWith("preview-"))host.AddComponent<GildedModalInputVerification>().BeginPreview(menu,mode);
                    else host.AddComponent<GildedModalInputVerification>().Begin(menu,Output,()=>{SessionState.SetBool(Pending,false);EditorApplication.ExitPlaymode();});
                }
                catch(Exception error){Debug.LogException(error);SessionState.SetBool(Pending,false);EditorApplication.ExitPlaymode();}
                return;
            }
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(new[]{"Assets/Scripts/UI/GildedMainMenu.cs","Assets/Scripts/UI/GildedAcquisitionPresentation.cs","Assets/Scripts/UI/GildedPlayground.cs","Assets/Scripts/UI/GildedMenuNavigation.cs","Assets/Scripts/UI/GildedModalInputVerification.cs"}.Any(path=>File.GetLastWriteTimeUtc(path)>File.GetLastWriteTimeUtc(typeof(GildedMainMenu).Assembly.Location))
                ||File.GetLastWriteTimeUtc("Assets/Editor/GildedModalVerificationRunner.cs")>File.GetLastWriteTimeUtc(typeof(GildedModalVerificationRunner).Assembly.Location))return;
            BackupPlayerSaves();
            SessionState.SetString(Pending+".Mode",File.ReadAllText(Request).Trim());File.Delete(Request);SessionState.SetBool(Pending+".Profile",ProfileService.Suspended);SessionState.SetBool(Pending+".Run",SaveService.Suspended);
            ProfileService.Suspended=SaveService.Suspended=true;SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
    }
}
