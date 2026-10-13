using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GildedFate.Chronicle;
using GildedFate.ChronicleBook;
using GildedFate.Saving;
using GildedFate.UI;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    // Read-only, edit-mode art review: no audio, real profile, run state or persistence.
    public sealed class ChronicleCinematicArtReview : EditorWindow
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        const string Output="Library/ChronicleCinematicArtVerification.json";
        ChronicleBookStageHost stage;GameObject host;GildedMainMenu menu;ChronicleSceneState state;
        readonly string[] views={"Kingdom","Vanguard","Hexer","Reaper","Campfire","Weave"};
        int selected;string error="",sceneId="MEM_01";float sampleTime=24;
        bool reducedMotion,reducedFlashing,verificationDone;
        [Serializable] sealed class Report { public int checks,backdrops,characters,effects,artifacts,storyScenes;public List<string> failures=new(); }
        [InitializeOnLoadMethod]static void Watch()=>EditorApplication.update+=()=>
        {
            const string request="Library/ChronicleCinematicReview.request";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(File.GetLastWriteTimeUtc("Assets/Scripts/UI/GildedChronicleCinematicArt.cs")>File.GetLastWriteTimeUtc(typeof(GildedMainMenu).Assembly.Location)
                ||File.GetLastWriteTimeUtc("Assets/Editor/ChronicleCinematicArtReview.cs")>File.GetLastWriteTimeUtc(typeof(ChronicleCinematicArtReview).Assembly.Location))return;
            var mode=File.ReadAllText(request).Trim();File.Delete(request);
            var window=GetWindow<ChronicleCinematicArtReview>(true,"Chronicle cinematic art review",true);
            var index=Array.IndexOf(window.views,mode);if(index>=0)window.selected=index;
            window.position=new Rect(120,90,1920,1080);window.ShowUtility();window.error="";window.verificationDone=false;
            var sample=mode.Split('@');
            if(sample.Length==2&&ChronicleScripts.Get(sample[0])!=null&&float.TryParse(sample[1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var seconds))
            {window.sceneId=sample[0];window.sampleTime=Mathf.Clamp(seconds,0,600);window.BuildStorySample();}
            else window.BuildShowcase();
        };
        void OnEnable()=>EditorApplication.update+=Repaint;
        [MenuItem("Gilded Fate/Chronicle/Review Cinematic Illustrations")]
        static void OpenReview()=>File.WriteAllText("Library/ChronicleCinematicReview.request","Campfire");
        void Initialize()
        {
            // Remove only disposable stages left by an earlier review-window reload.
            foreach(var old in Resources.FindObjectsOfTypeAll<GameObject>())
                if(old.name=="Gilded Fate · Chronicle book stage"&&old.transform.parent==null&&old.transform.position==new Vector3(3000,4000,0))
                {foreach(var camera in old.GetComponentsInChildren<Camera>(true))if(camera.targetTexture){camera.targetTexture.Release();DestroyImmediate(camera.targetTexture);}DestroyImmediate(old);}
            stage=ChronicleBookStageHost.Create(2880,1620);if(stage==null)throw new InvalidOperationException("Book stage unavailable");
            stage.Camera.transform.parent.position=new Vector3(3000,4000,0);
            foreach(var child in stage.Camera.transform.parent.GetComponentsInChildren<Transform>(true))child.gameObject.hideFlags=HideFlags.HideAndDontSave;
            stage.SetActive(true);stage.Rig.SetPoseImmediate(ChronicleBookPose.Open,0);stage.Tick(0,new ChronicleCameraState());
            host=new GameObject("Chronicle cinematic review · disposable"){hideFlags=HideFlags.HideAndDontSave};menu=host.AddComponent<GildedMainMenu>();menu.enabled=false;
            typeof(GildedMainMenu).GetField("captureMode",Private).SetValue(menu,true);typeof(GildedMainMenu).GetField("white",Private).SetValue(menu,Texture2D.whiteTexture);
            var profile=new PlayerProfile();profile.EnsureMeta();typeof(GildedMainMenu).GetField("profile",Private).SetValue(menu,profile);typeof(GildedMainMenu).GetField("chronicleHost",Private).SetValue(menu,stage);
        }
        void BuildShowcase()
        {
            var backdrop=selected==2?"chamber":selected==3?"souls":selected==4?"campfire":selected==5?"weave":"kingdom";var palette=ChronicleBackdrops.Get(backdrop);
            state=new ChronicleSceneState{pose=ChronicleBookPose.Open,layout=ChronicleSpreadLayout.IllustrationLeftTextRight};
            var ill=state.illustration;ill.visible=true;ill.alpha=1;ill.backdrop=backdrop;ill.top=palette.top;ill.bottom=palette.bottom;
            void Actor(string id,ChronicleShape shape,ChronicleColor color,float x,float y,float w,float h,string label="")=>ill.actors.Add(new ChronicleActor{id=id,shape=shape,color=color,x=x,y=y,w=w,h=h,label=label});
            void Effect(string name,float p,float strength=1)=>ill.animations.Add(new ChronicleAnimation{name=name,duration=4,t=p*4,intensity=strength,persist=true});
            if(selected==0){Actor("castle",ChronicleShape.Castle,new ChronicleColor(.2f,.2f,.2f),.69f,.44f,.35f,.45f);Actor("traveler",ChronicleShape.Figure,ChronicleColor.Being,.32f,.73f,.08f,.23f);}
            else if(selected>=1&&selected<=3)
            {
                var hero=(ChronicleHero)(selected-1);Actor("hero",ChronicleShape.Figure,ChronicleColor.ForHero(hero),.48f,.72f,.11f,.36f,ChronicleCatalog.HeroName(hero));
                if(selected==2)Effect("sigil_glow",.83f,.52f);if(selected==3)Effect("echo_ring",.48f,.55f);
            }
            else
            {
                Actor("v",ChronicleShape.Figure,ChronicleColor.Vanguard,.22f,.75f,.09f,.24f,"Vanguard");Actor("h",ChronicleShape.Figure,ChronicleColor.Hexer,.50f,.68f,.09f,.24f,"Hexer");Actor("r",ChronicleShape.Figure,ChronicleColor.Reaper,.78f,.75f,.09f,.24f,"Reaper");
                if(selected==4)Actor("fire",ChronicleShape.Circle,new ChronicleColor(1,.55f,.15f),.50f,.84f,.08f,.065f);else{Effect("threads",.92f,.67f);Effect("fracture",.75f,.64f);}
            }
            foreach(var text in new[]{"Before the world was broken, there was a time when tomorrow belonged to no one.","A kingdom stood beneath an unbroken sky. Its people built monuments to the past, and dreamed of futures they could not yet see.","Then came a traveler."})state.blocks.Add(new ChronicleTextBlock{text=text,side=ChroniclePageSide.Right,complete=true});
        }
        void BuildStorySample()
        {
            var scene=ChronicleScripts.Get(sceneId);if(scene==null)return;
            var seq=new ChronicleSequencer(scene,new NullChronicleStage(),new ChronicleStaticContext(),null,null,new ChroniclePageMetrics{fontSize=34,uniformTextSize=true,autoPageTurns=true},false,true);
            seq.Start(ChronicleBookPose.Open);for(var t=0f;t<sampleTime;t+=.05f)seq.Update(.05f);state=seq.State;
        }
        void OnGUI()
        {
            try
            {
                if(stage==null||!menu)Initialize();if(state==null)BuildShowcase();
                var next=GUI.Toolbar(new Rect(8,4,820,25),selected,views);if(next!=selected){selected=next;BuildShowcase();}
                reducedMotion=GUI.Toggle(new Rect(850,7,155,20),reducedMotion,"Reduced motion");reducedFlashing=GUI.Toggle(new Rect(1008,7,160,20),reducedFlashing,"Reduced flashing");
                sceneId=GUI.TextField(new Rect(1180,4,110,25),sceneId);sampleTime=EditorGUI.FloatField(new Rect(1294,4,60,25),sampleTime);if(GUI.Button(new Rect(1362,4,115,25),"Sample story"))BuildStorySample();
                var p=(PlayerProfile)typeof(GildedMainMenu).GetField("profile",Private).GetValue(menu);p.reduceMotion=reducedMotion;p.reduceFlashing=reducedFlashing;
                var w=position.width;var h=position.height-32;GUI.BeginGroup(new Rect(0,32,w,h));
                try
                {
                    typeof(GildedMainMenu).GetMethod("DrawChronicleAtmosphere",Private).Invoke(menu,new object[]{w,h});
                    var scale=Mathf.Min(w/1600,h/900);var dest=new Rect((w-1600*scale)*.5f,(h-900*scale)*.5f,1600*scale,900*scale);
                    if(Event.current.type==EventType.Repaint)GUI.DrawTexture(dest,stage.Texture,ScaleMode.StretchToFill,true);typeof(GildedMainMenu).GetMethod("DrawChroniclePages",Private).Invoke(menu,new object[]{dest,state});
                }
                finally{GUI.EndGroup();}
                if(!verificationDone&&Event.current.type==EventType.Repaint){verificationDone=true;Verify();}
                if(error.Length>0)EditorGUI.HelpBox(new Rect(12,40,w-24,95),error,MessageType.Error);
            }
            catch(Exception exception){error=exception.ToString();Debug.LogException(exception);}
        }
        void Verify()
        {
            var report=new Report();void Check(bool pass,string name){report.checks++;if(!pass)report.failures.Add(name);}
            using(var art=new ChronicleLivingArt())
            {
                foreach(var name in new[]{"Kingdom","Sanctum","Campfire","FracturedWeave"}){var t=art.Load("Chronicle/Illustrations/Cinematic/"+name);Check(t&&t.width>=1024&&t.height>=1536,"Native detail: "+name);report.backdrops++;}
                foreach(ChronicleHero hero in Enum.GetValues(typeof(ChronicleHero)))
                {
                    var actor=new ChronicleActor{color=ChronicleColor.ForHero(hero)};var path=(string)typeof(GildedMainMenu).GetMethod("ChronicleCharacterArt",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{actor});
                    var t=art.Load(path);Check(t&&path.EndsWith(hero.ToString().ToLowerInvariant()),"Distinct artwork: "+hero);report.characters++;
                    var moving=Pixels(art.Render(t,2,new Color(.9f,.6f,.3f),1));var later=Pixels(art.Render(t,8,new Color(.9f,.6f,.3f),1));Check(Difference(moving,later)>.00001,"Animated hero: "+hero);Check(Alpha(moving)>.10,"Visible hero: "+hero);
                }
                foreach(var name in new[]{"ChronicleLivingIllustration","ChronicleIllustrationVfx"})
                {var shader=Resources.Load<Shader>("Chronicle/"+name);Check(shader&&shader.isSupported,"Supported shader: "+name);if(shader)foreach(var message in ShaderUtil.GetShaderMessages(shader))Check(message.severity.ToString()!="Error",name+": "+message.message);}
                for(var mode=1;mode<=6;mode++)
                {
                    art.BeginIllustration();var early=Pixels(art.RenderEffect(mode,.35f,2,1,new Vector2(.5f,.5f),new Color(.9f,.7f,.3f)));art.BeginIllustration();var late=Pixels(art.RenderEffect(mode,.75f,8,1,new Vector2(.5f,.5f),new Color(.9f,.7f,.3f)));
                    Check(Alpha(early)>.0005,"Visible VFX: "+mode);Check(Difference(early,late)>.00001,"Progressive VFX: "+mode);report.effects++;
                }
                for(var mode=7;mode<=10;mode++)
                {
                    var early=Pixels(art.RenderArtifact(mode,2,new Color(.25f,.85f,.60f)));var later=Pixels(art.RenderArtifact(mode,8,new Color(.25f,.85f,.60f)));
                    Check(Alpha(early)>.015,"Visible artifact: "+mode);Check(Difference(early,later)>.000001,"Animated artifact: "+mode);
                    var frozen=Pixels(art.RenderArtifact(mode,0,new Color(.25f,.85f,.60f)));var frozenAgain=Pixels(art.RenderArtifact(mode,0,new Color(.25f,.85f,.60f)));
                    Check(Difference(frozen,frozenAgain)==0,"Reduced motion artifact: "+mode);report.artifacts++;
                }
                foreach(var backdrop in ChronicleBackdrops.Ids){var name=(string)typeof(GildedMainMenu).GetMethod("ChronicleEnvironmentArt",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{backdrop});Check(art.Load("Chronicle/Illustrations/Cinematic/"+name),"Backdrop coverage: "+backdrop);}
                var errors=ChronicleValidator.Validate();Check(errors.Count==0,"Story catalog: "+string.Join("; ",errors));foreach(var id in ChronicleScripts.Ids){Check(ChronicleScripts.Get(id)!=null,"Story available: "+id);report.storyScenes++;}
            }
            File.WriteAllText(Output,JsonUtility.ToJson(report,true));Debug.Log("[Chronicle cinematic art] "+report.checks+" checks; "+report.failures.Count+" failures.");
        }
        static Color32[] Pixels(Texture texture)
        {
            var rt=RenderTexture.GetTemporary(96,144,0,RenderTextureFormat.ARGB32);var active=RenderTexture.active;Texture2D copy=null;
            try{Graphics.Blit(texture,rt);RenderTexture.active=rt;copy=new Texture2D(96,144,TextureFormat.RGBA32,false);copy.ReadPixels(new Rect(0,0,96,144),0,0);copy.Apply();return copy.GetPixels32();}
            finally{RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(copy)DestroyImmediate(copy);}
        }
        static double Alpha(Color32[] pixels){double sum=0;foreach(var p in pixels)sum+=p.a/255.0;return sum/pixels.Length;}
        static double Difference(Color32[] a,Color32[] b){double sum=0;for(var i=0;i<a.Length;i++)sum+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)+Math.Abs(a[i].a-b[i].a);return sum/(a.Length*1020.0);}
        void OnDisable(){EditorApplication.update-=Repaint;Cleanup();}void OnDestroy()=>Cleanup();void Cleanup()
        {
            // A disabled, non-ExecuteAlways menu does not receive OnDestroy in edit mode.
            // Release its GPU surfaces explicitly as well as the separately owned book stage.
            if(menu)typeof(GildedMainMenu).GetMethod("OnDestroy",Private)?.Invoke(menu,null);
            if(host)DestroyImmediate(host);stage?.Dispose();stage=null;host=null;menu=null;
        }
    }
}
