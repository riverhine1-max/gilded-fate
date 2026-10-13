using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GildedFate.Chronicle;
using GildedFate.ChronicleBook;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    [InitializeOnLoad]
    public static class ChronicleImportVerification
    {
        const string Request="Library/ChronicleImportVerification.request";
        const string Output=@"C:\Users\river\Documents\Codex\2026-10-08\use-these-files-from-the-libray-4\outputs";
        [Serializable] sealed class Index { public ChronicleDialogueTiming[] lines; }
        [Serializable] sealed class Report { public int voiceBindings,recordings,scenes,animationClips; public bool iconLoaded,fontLoaded; public List<string> failures=new(); }
        static ChronicleImportVerification() => EditorApplication.update += WaitForImport;
        static void WaitForImport()
        {
            if(!File.Exists(Request)){EditorApplication.update-=WaitForImport;return;}
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Chronicle/ChronicleBook.prefab"))return;
            EditorApplication.update-=WaitForImport;Verify();File.Delete(Request);
        }

        [MenuItem("Gilded Fate/Chronicle/Verify Imported Book and Voices")]
        public static void Verify()
        {
            var report=new Report();Directory.CreateDirectory(Output);
            try
            {
                var data=Resources.Load<TextAsset>("Chronicle/Audio/DialogueIndex");
                if(!data)throw new Exception("The dialogue index did not load.");
                var index=JsonUtility.FromJson<Index>(data.text);var recordings=new HashSet<string>();var scenes=new HashSet<string>();
                foreach(var line in index.lines)
                {
                    report.voiceBindings++;recordings.Add(line.audio);scenes.Add(line.sceneId);
                    if(!line.Validate(line.text,out var reason))report.failures.Add(line.lineId+": "+reason);
                    var clip=Resources.Load<AudioClip>(line.audio);
                    if(!clip||Math.Abs(clip.length-line.durationSeconds)>.025f)report.failures.Add(line.lineId+": imported audio duration or resource is invalid.");
                }
                report.recordings=recordings.Count;report.scenes=scenes.Count;
                report.iconLoaded=Resources.Load<Texture2D>("Chronicle/UI/ChronicleArchiveIcon");
                report.fontLoaded=Resources.Load<Font>("Chronicle/Fonts/ChronicleHandwriting");
                if(!report.iconLoaded||!report.fontLoaded)report.failures.Add("Archive artwork or handwriting font did not load.");
                var prefab=Resources.Load<GameObject>("Chronicle/ChronicleBook");
                var config=Resources.Load<ChronicleBookRigConfig>("Chronicle/ChronicleBookRigConfig");
                if(!prefab||!config)throw new Exception("Finished book prefab or rig configuration did not load.");
                var animator=prefab.GetComponentInChildren<Animator>();
                var clips=animator.runtimeAnimatorController.animationClips.Distinct().ToArray();report.animationClips=clips.Length;
                if(clips.Length!=9)report.failures.Add("Expected nine book animations.");
                foreach(var anchor in new[]{config.leftPageAnchorName,config.rightPageAnchorName})
                    if(!prefab.GetComponentsInChildren<Transform>(true).Any(t=>t.name==anchor))report.failures.Add("Missing page anchor "+anchor);
                RenderPreview(prefab,config,clips,"CHR_Pose_Open","Chronicle_Book_Open.png");
                RenderPreview(prefab,config,clips,"CHR_Pose_Closed","Chronicle_Book_Closed.png");
            }
            catch(Exception error){report.failures.Add(error.ToString());}
            File.WriteAllText(Path.Combine(Output,"Chronicle_Unity_Verification.json"),JsonUtility.ToJson(report,true));
            Debug.Log("[Chronicle] Import verification: "+report.voiceBindings+" voice bindings, "+report.recordings+" recordings, "+report.animationClips+" animations, "+report.failures.Count+" failures.");
        }

        static void RenderPreview(GameObject prefab,ChronicleBookRigConfig config,AnimationClip[] clips,string state,string filename)
        {
            var root=new GameObject("Chronicle verification preview"){hideFlags=HideFlags.HideAndDontSave};
            var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                root.transform.position=new Vector3(0,4000,0);
                var instance=UnityEngine.Object.Instantiate(prefab,root.transform,false);
                foreach(var t in instance.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
                var animator=instance.GetComponentInChildren<Animator>();animator.enabled=false;
                clips.First(c=>c.name.EndsWith(state,StringComparison.Ordinal)).SampleAnimation(animator.gameObject,0);
                var leaf=instance.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name=="TurningPage_01");if(leaf)leaf.enabled=false;
                var cameraObject=new GameObject("Preview camera");cameraObject.transform.SetParent(root.transform,false);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.targetTexture=target;camera.cullingMask=1<<29;
                camera.fieldOfView=24;camera.nearClipPlane=.01f;camera.farClipPlane=60;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.015f,.02f);
                var focus=root.transform.position+config.cameraTarget;camera.transform.position=focus+Vector3.up*config.cameraDistance;camera.transform.LookAt(focus,Vector3.forward);
                target.Create();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output,filename),pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
