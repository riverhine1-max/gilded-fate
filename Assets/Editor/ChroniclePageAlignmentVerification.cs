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
    public static class ChroniclePageAlignmentVerification
    {
        const string Request="Library/ChroniclePageAlignment.request";
        const string Output=@"C:\Users\river\Documents\Codex\2026-10-08\use-these-files-from-the-libray-4\outputs";
        [Serializable] class Report { public int checks; public bool bookmarkAtBottom; public float readingTiltDegrees; public Rect left,right; public List<string> failures=new(); }
        static ChroniclePageAlignmentVerification() => EditorApplication.update += Wait;
        static double nextCheck;
        static void Wait()
        {
            if(EditorApplication.timeSinceStartup < nextCheck)return;
            nextCheck=EditorApplication.timeSinceStartup+1;
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            var editorLoaded=File.GetLastWriteTimeUtc(typeof(ChroniclePageAlignmentVerification).Assembly.Location);
            var runtimeLoaded=File.GetLastWriteTimeUtc(typeof(ChronicleAnimatorBookRig).Assembly.Location);
            var storyLoaded=File.GetLastWriteTimeUtc(typeof(ChronicleTextBlock).Assembly.Location);
            if(new[]{"Assets/Editor/ChroniclePageAlignmentVerification.cs","Assets/Editor/ChronicleHandwritingVerification.cs","Assets/Editor/ChronicleArtPreview.cs","Assets/Scripts/ChronicleBook/ChronicleArtLineRenderer.cs","Assets/Scripts/UI/GildedChronicleArt.cs","Assets/Scripts/ChronicleBook/ChronicleInkStrokeRenderer.cs","Assets/Scripts/UI/GildedChroniclePresentation.cs","Assets/Scripts/Chronicle/ChronicleWriter.cs","Assets/Scripts/ChronicleBook/ChronicleBookStageHost.cs","Assets/Scripts/ChronicleBook/ChronicleAnimatorBookRig.cs"}.Any(p=>File.GetLastWriteTimeUtc(p)>(p.StartsWith("Assets/Editor/")?editorLoaded:p.StartsWith("Assets/Scripts/Chronicle/")?storyLoaded:runtimeLoaded)))
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                return;
            }
            File.Delete(Request);Verify();
        }
        [MenuItem("Gilded Fate/Chronicle/Verify Page Alignment")]
        public static void Verify()
        {
            var report=new Report();ChronicleBookStageHost stage=null;Directory.CreateDirectory(Output);
            if (!Application.isPlaying)
                foreach(var old in Resources.FindObjectsOfTypeAll<GameObject>())
                    if(old.scene.IsValid() && old.name == "Gilded Fate · Chronicle book stage" && (old.transform.position == new Vector3(0,4000,0) || (old.transform.position == new Vector3(3000,4000,0) && Resources.FindObjectsOfTypeAll<ChronicleArtPreview>().Length == 0)))
                        UnityEngine.Object.DestroyImmediate(old);
            try
            {
                stage=ChronicleBookStageHost.Create(1600,900);
                if(stage==null||!stage.UsingFinishedModel)throw new Exception("The finished book is unavailable.");
                stage.Camera.transform.parent.position=new Vector3(2000,4000,0);
                stage.SetActive(true);
                var rig=(ChronicleAnimatorBookRig)stage.Rig;
                var animator=rig.GetComponentInChildren<Animator>();
                const string vellumPath="Assets/Resources/Chronicle/Textures/Parchment/CHR_Weathered_BaseColor.png";
                var importer=(TextureImporter)AssetImporter.GetAtPath(vellumPath);
                if(importer.npotScale!=TextureImporterNPOTScale.None || importer.textureCompression!=TextureImporterCompression.Uncompressed)
                {
                    importer.npotScale=TextureImporterNPOTScale.None;
                    importer.textureCompression=TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
                var vellum=AssetDatabase.LoadAssetAtPath<Texture2D>(vellumPath);report.checks+=2;
                if(!vellum || vellum.width!=1254 || vellum.height!=1254)report.failures.Add("Parchment import has reduced the source resolution.");
                if(importer.textureCompression!=TextureImporterCompression.Uncompressed)report.failures.Add("Parchment detail is compressed.");
                var shader=Shader.Find("GildedFate/ChronicleSurface");report.checks++;
                if(!shader || ShaderUtil.ShaderHasError(shader))report.failures.Add("The book surface shader has a compile error.");
                foreach(var name in new[]{"Parchment_Front","Parchment_Back"})
                {
                    var material=Resources.Load<Material>("Chronicle/Materials/"+name);
                    report.checks+=2;
                    if(!material || AssetDatabase.GetAssetPath(material.mainTexture)!="Assets/Resources/Chronicle/Textures/Parchment/CHR_Weathered_BaseColor.png")report.failures.Add(name+": weathered parchment is not bound.");
                    if(!material || material.GetFloat("_PaperRelief")<.5f)report.failures.Add(name+": paper relief is missing.");
                }
                stage.SetActive(false);stage.SetActive(true);report.checks++;
                if(!animator.HasState(rig.Config.animatorLayer,Animator.StringToHash(rig.Config.openState)))report.failures.Add("The book loses its animation states after reactivation.");
                foreach(var name in new[]{rig.Config.openState,rig.Config.closeState,rig.Config.idleState})
                {
                    report.checks++;
                    if(!animator.HasState(rig.Config.animatorLayer,Animator.StringToHash(name)))report.failures.Add("Initial Animator state is unavailable: "+name);
                }
                rig.SetPoseImmediate(ChronicleBookPose.Open,0);
                if(rig.TryGetReadingNormal(out var normal))
                {
                    report.readingTiltDegrees=Vector3.Angle(normal,Vector3.up);report.checks++;
                    if(report.readingTiltDegrees>.3f)report.failures.Add("Open paper is slanted: "+report.readingTiltDegrees+" degrees.");
                }
                else report.failures.Add("Cannot measure the open paper plane.");
                foreach(var zoom in new[]{.85f,1f,1.15f})
                {
                    stage.Tick(0,new ChronicleCameraState{zoom=zoom});
                    rig.Config.openClip.SampleAnimation(animator.gameObject,rig.Config.openClip.length);
                    stage.Camera.Render();
                    foreach(var side in new[]{ChroniclePageSide.Left,ChroniclePageSide.Right})
                    {
                        report.checks++;
                        if(!stage.TryGetPageViewportRect(side,out var area))throw new Exception("Missing page area: "+side);
                        var world=new Vector3[4];stage.Surface.TryGetPageCorners(side,world);
                        var points=world.Select(p=>(Vector2)stage.Camera.WorldToViewportPoint(p)).ToArray();
                        var opposite=new Vector3[4];
                        stage.Surface.TryGetPageCorners(side==ChroniclePageSide.Left?ChroniclePageSide.Right:ChroniclePageSide.Left,opposite);
                        var other=opposite.Select(p=>(Vector2)stage.Camera.WorldToViewportPoint(p)).ToArray();
                        if(Math.Abs(other.Average(p=>p.x)-area.center.x)<Math.Abs(points.Average(p=>p.x)-area.center.x))points=other;
                        var center=points.Aggregate(Vector2.zero,(sum,p)=>sum+p)/4f;
                        Array.Sort(points,(a,b)=>Mathf.Atan2(a.y-center.y,a.x-center.x).CompareTo(Mathf.Atan2(b.y-center.y,b.x-center.x)));
                        foreach(var corner in new[]{new Vector2(area.xMin,area.yMin),new Vector2(area.xMin,area.yMax),new Vector2(area.xMax,area.yMin),new Vector2(area.xMax,area.yMax)})
                            for(var i=0;i<4;i++)
                            {
                                var edge=points[(i+1)%4]-points[i];var offset=corner-points[i];
                                if(edge.x*offset.y-edge.y*offset.x < -.00001f)report.failures.Add(side+" at zoom "+zoom+": overlay leaves the paper margin.");
                            }
                        if(area.width<.1f||area.height<.2f)report.failures.Add(side+": reading area is too small.");
                        if(zoom==1f){if(side==ChroniclePageSide.Left)report.left=area;else report.right=area;}
                    }
                    report.checks++;
                    stage.TryGetPageViewportRect(ChroniclePageSide.Left,out var left);
                    stage.TryGetPageViewportRect(ChroniclePageSide.Right,out var right);
                    if(left.xMax>=right.xMin)report.failures.Add("The reader's left and right page areas are reversed or overlapping at zoom "+zoom);
                    if(zoom==1f)
                    {
                        var bookmark=rig.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="Bookmark_Ribbon");
                        var mesh=new Mesh();bookmark.BakeMesh(mesh);
                        var ribbon=stage.Camera.WorldToViewportPoint(bookmark.transform.TransformPoint(mesh.bounds.center));
                        UnityEngine.Object.DestroyImmediate(mesh);
                        report.checks++;report.bookmarkAtBottom=ribbon.y<(left.center.y+right.center.y)*.5f;
                        if(!report.bookmarkAtBottom)report.failures.Add("The bookmark is at the top: the book is upside down.");
                        SavePreview(stage,report);
                    }
                }
            }
            catch(Exception error){report.failures.Add(error.ToString());}
            finally{if(stage!=null)stage.Dispose();}
            File.WriteAllText(Path.Combine(Output,"Chronicle_Page_Alignment.json"),JsonUtility.ToJson(report,true));
            Debug.Log("[Chronicle] Page alignment: "+report.checks+" checks, "+report.failures.Count+" failures.");
        }
        static void SavePreview(ChronicleBookStageHost stage,Report report)
        {
            var previous=RenderTexture.active;var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
            var material=new Material(Shader.Find("Hidden/Internal-Colored"));
            try
            {
                RenderTexture.active=stage.Texture;
                material.SetInt("_ZWrite",0);material.SetInt("_ZTest",(int)UnityEngine.Rendering.CompareFunction.Always);
                material.SetInt("_Cull",(int)UnityEngine.Rendering.CullMode.Off);
                material.SetPass(0);GL.PushMatrix();GL.LoadPixelMatrix(0,1600,0,900);GL.Begin(GL.LINES);GL.Color(new Color(.25f,1f,.6f));
                foreach(var rect in new[]{report.left,report.right})
                {
                    var x0=rect.xMin*1600;var x1=rect.xMax*1600;var y0=rect.yMin*900;var y1=rect.yMax*900;
                    GL.Vertex3(x0,y0,0);GL.Vertex3(x1,y0,0);GL.Vertex3(x1,y0,0);GL.Vertex3(x1,y1,0);
                    GL.Vertex3(x1,y1,0);GL.Vertex3(x0,y1,0);GL.Vertex3(x0,y1,0);GL.Vertex3(x0,y0,0);
                }
                GL.End();GL.PopMatrix();pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output,"Chronicle_Page_Alignment.png"),pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(material);}
        }
    }
}
