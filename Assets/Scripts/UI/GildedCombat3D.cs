using System.Collections.Generic;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private GameObject combat3DRoot, vanguard3D, hexer3D, gildedSentry3D, mirrorWitch3D;
        private Camera combat3DCamera;
        private RenderTexture combat3DTexture;
        private Animation vanguardAnimation,hexerAnimation,gildedSentryAnimation,mirrorWitchAnimation;
        private readonly Dictionary<string,AnimationClip> vanguardClips=new();
        private readonly Dictionary<string,AnimationClip> hexerClips=new();
        private readonly Dictionary<string,AnimationClip> gildedSentryClips=new();
        private readonly Dictionary<string,AnimationClip> mirrorWitchClips=new();
        private float previous3DAction, previous3DHit, previous3DBuff, previous3DDeath, previous3DVictory;
        private float previous3DEnemyAction,previous3DFoeHit,previous3DFoeBuff,previous3DFoeDeath;
        private const int Combat3DLayer=30;
        // The final combat direction is a unified illustrated stage for every
        // encounter. Keep the legacy renderer dormant so imported models can
        // never replace the readable portrait composition at runtime.
        private static readonly bool IllustratedCombatOnly=true;

        private bool HasCombat3DStage=>!IllustratedCombatOnly&&combat3DRoot&&combat3DTexture&&currentEnemy?.boss!=true;
        private Animation ActiveHeroAnimation=>run.hero==HeroId.Vanguard?vanguardAnimation:run.hero==HeroId.Hexer?hexerAnimation:null;
        private Dictionary<string,AnimationClip> ActiveHeroClips=>run.hero==HeroId.Vanguard?vanguardClips:run.hero==HeroId.Hexer?hexerClips:null;
        private bool HasHero3D=>HasCombat3DStage&&(run.hero==HeroId.Vanguard?vanguard3D&&vanguardAnimation:run.hero==HeroId.Hexer&&hexer3D&&hexerAnimation);
        private Animation ActiveEnemyAnimation=>currentEnemy?.id=="gilded_sentry"?gildedSentryAnimation:currentEnemy?.id=="mirror_witch"?mirrorWitchAnimation:null;
        private Dictionary<string,AnimationClip> ActiveEnemyClips=>currentEnemy?.id=="gilded_sentry"?gildedSentryClips:currentEnemy?.id=="mirror_witch"?mirrorWitchClips:null;
        private bool HasEnemy3D=>HasCombat3DStage&&ActiveEnemyAnimation&&ActiveEnemyClips!=null;

        private void InitializeCombat3D()
        {
            if(IllustratedCombatOnly)return;
            var stageAsset=Resources.Load<GameObject>("Models/LowerVaultStage");
            var vanguardAsset=Resources.Load<GameObject>("Models/Vanguard");
            var hexerAsset=Resources.Load<GameObject>("Models/Hexer");
            var gildedSentryAsset=Resources.Load<GameObject>("Models/Enemies/GildedSentry");
            var mirrorWitchAsset=Resources.Load<GameObject>("Models/Enemies/MirrorWitch");
            if(!stageAsset){Debug.LogWarning("[Gilded Fate 3D] Battlefield asset was not imported; illustrated fallback remains active.");return;}

            combat3DRoot=new GameObject("Gilded Fate · 3D Combat Stage");
            DontDestroyOnLoad(combat3DRoot);
            var stage=Instantiate(stageAsset,combat3DRoot.transform);stage.name="Lower Vault · authored geometry";
            if(vanguardAsset){vanguard3D=Instantiate(vanguardAsset,combat3DRoot.transform);vanguard3D.name="Vanguard · rigged combat model";}
            if(hexerAsset){hexer3D=Instantiate(hexerAsset,combat3DRoot.transform);hexer3D.name="Hexer · rigged combat model and fate cards";}
            if(gildedSentryAsset){gildedSentry3D=Instantiate(gildedSentryAsset,combat3DRoot.transform);gildedSentry3D.name="Gilded Sentry · rigged clockwork enemy";}
            if(mirrorWitchAsset){mirrorWitch3D=Instantiate(mirrorWitchAsset,combat3DRoot.transform);mirrorWitch3D.name="Mirror Witch · rigged fractured-glass enemy";}
            // Blender's -Y authored camera direction becomes +Z on Unity's FBX axis
            // conversion. Turn both assets together so the wall stays behind the actors.
            stage.transform.localRotation=Quaternion.Euler(0,180,0);
            if(vanguard3D){vanguard3D.transform.localRotation=Quaternion.Euler(0,180,0);vanguard3D.transform.localPosition=new Vector3(-4.82f,.02f,0);vanguard3D.transform.localScale=Vector3.one*1.18f;}
            if(hexer3D){hexer3D.transform.localRotation=Quaternion.Euler(0,180,0);hexer3D.transform.localPosition=new Vector3(-4.82f,.02f,0);hexer3D.transform.localScale=Vector3.one*1.17f;}
            if(gildedSentry3D){gildedSentry3D.transform.localRotation=Quaternion.Euler(0,180,0);gildedSentry3D.transform.localPosition=new Vector3(3.55f,.02f,.04f);gildedSentry3D.transform.localScale=Vector3.one*1.08f;}
            if(mirrorWitch3D){mirrorWitch3D.transform.localRotation=Quaternion.Euler(0,180,0);mirrorWitch3D.transform.localPosition=new Vector3(3.55f,.02f,.04f);mirrorWitch3D.transform.localScale=Vector3.one*1.08f;}
            SetCombatLayer(combat3DRoot.transform);
            PrepareImportedMaterials(stage);
            if(vanguard3D)PrepareImportedMaterials(vanguard3D);
            if(hexer3D)PrepareImportedMaterials(hexer3D);
            if(gildedSentry3D)PrepareImportedMaterials(gildedSentry3D);
            if(mirrorWitch3D)PrepareImportedMaterials(mirrorWitch3D);

            if(vanguard3D)vanguardAnimation=PrepareAnimatedModel(vanguard3D,"Models/Vanguard",vanguardClips,"Vanguard");
            if(hexer3D)hexerAnimation=PrepareAnimatedModel(hexer3D,"Models/Hexer",hexerClips,"Hexer");
            if(gildedSentry3D)gildedSentryAnimation=PrepareAnimatedModel(gildedSentry3D,"Models/Enemies/GildedSentry",gildedSentryClips,"Gilded Sentry");
            if(mirrorWitch3D)mirrorWitchAnimation=PrepareAnimatedModel(mirrorWitch3D,"Models/Enemies/MirrorWitch",mirrorWitchClips,"Mirror Witch");

            combat3DTexture=new RenderTexture(1440,390,24,RenderTextureFormat.ARGB32)
            {name="Gilded Fate · Lower Vault Battlefield",antiAliasing=4,useMipMap=false,autoGenerateMips=false};
            combat3DTexture.Create();
            var cameraHost=new GameObject("Lower Vault camera");cameraHost.transform.SetParent(combat3DRoot.transform,false);
            cameraHost.transform.position=new Vector3(0,2.65f,-9.4f);
            cameraHost.transform.LookAt(new Vector3(0,1.58f,.42f));
            combat3DCamera=cameraHost.AddComponent<Camera>();
            combat3DCamera.targetTexture=combat3DTexture;combat3DCamera.fieldOfView=28.5f;
            combat3DCamera.clearFlags=CameraClearFlags.SolidColor;combat3DCamera.backgroundColor=new Color(.006f,.009f,.016f,1);
            combat3DCamera.cullingMask=1<<Combat3DLayer;combat3DCamera.allowHDR=true;combat3DCamera.allowMSAA=true;
            combat3DCamera.nearClipPlane=.08f;combat3DCamera.farClipPlane=40;
            // Render explicitly in UpdateCombat3D. This also makes isolated captures
            // deterministic instead of depending on secondary-camera scheduling.
            combat3DCamera.enabled=false;

            AddCombatLight("Warm vault key",LightType.Directional,new Vector3(-35,28,-18),new Color(1f,.76f,.48f),1.52f,0);
            AddCombatLight("Cold vault fill",LightType.Directional,new Vector3(28,-34,12),new Color(.35f,.55f,1f),.82f,0);
            AddCombatLight("Left brazier light",LightType.Point,new Vector3(-1.92f,1.0f,-.25f),new Color(1f,.40f,.08f),7.2f,5.4f);
            AddCombatLight("Right brazier light",LightType.Point,new Vector3(1.92f,1.0f,-.25f),new Color(1f,.40f,.08f),7.2f,5.4f);
            combat3DRoot.SetActive(false);
        }

        private static Animation PrepareAnimatedModel(GameObject model,string resource,Dictionary<string,AnimationClip> clips,string label)
        {
            var animation=model.GetComponentInChildren<Animation>();if(!animation)animation=model.AddComponent<Animation>();
            foreach(var clip in Resources.LoadAll<AnimationClip>(resource))
            {
                if(!clip||clip.name.StartsWith("__preview__"))continue;
                clips[clip.name]=clip;if(animation.GetClip(clip.name)==null)animation.AddClip(clip,clip.name);
            }
            if(clips.TryGetValue("Idle",out var idle)){animation.clip=idle;animation.Play("Idle");}
            Debug.Log($"[Gilded Fate 3D] {label} ready · {clips.Count} named clips · {model.GetComponentsInChildren<Renderer>(true).Length} renderers");
            return animation;
        }

        private void AddCombatLight(string name,LightType type,Vector3 positionOrEuler,Color color,float intensity,float range)
        {
            var host=new GameObject(name);host.transform.SetParent(combat3DRoot.transform,false);
            if(type==LightType.Directional)host.transform.eulerAngles=positionOrEuler;else host.transform.position=positionOrEuler;
            var light=host.AddComponent<Light>();light.type=type;light.color=color;light.intensity=intensity;light.range=range;
            light.cullingMask=1<<Combat3DLayer;light.shadows=LightShadows.Soft;light.shadowStrength=.78f;
        }

        private static void SetCombatLayer(Transform root)
        {
            root.gameObject.layer=Combat3DLayer;
            for(var i=0;i<root.childCount;i++)SetCombatLayer(root.GetChild(i));
        }

        private static void PrepareImportedMaterials(GameObject root)
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;renderer.receiveShadows=true;
                var materials=renderer.materials;
                for(var i=0;i<materials.Length;i++)
                {
                    var mat=materials[i];if(!mat)continue;
                    if(mat.HasProperty("_Color"))
                    {
                        var color=mat.name.Contains("Hexer_Coat")?new Color(.008f,.010f,.028f):
                            mat.name.Contains("Hexer_Violet")?new Color(.075f,.006f,.13f):
                            mat.name.Contains("Hexer_Mask")?new Color(.72f,.76f,.82f):
                            mat.name.Contains("Hexer_Obsidian")?new Color(.002f,.004f,.012f):
                            mat.name.Contains("Hexer_Cyan")?new Color(.015f,.62f,1f):
                            mat.name.Contains("Hexer_Magenta")?new Color(.62f,.025f,1f):
                            mat.name.Contains("Hexer_Silver")?new Color(.32f,.40f,.50f):
                            mat.name.Contains("Hexer_Card")?new Color(.008f,.005f,.025f):
                            mat.name.Contains("Sentry_Black_Steel")?new Color(.018f,.028f,.046f):
                            mat.name.Contains("Sentry_Aged_Brass")?new Color(.42f,.21f,.045f):
                            mat.name.Contains("Sentry_Clockwork_Glow")?new Color(1f,.34f,.035f):
                            mat.name.Contains("Sentry_Honed_Edge")?new Color(.31f,.39f,.48f):
                            mat.name.Contains("Sentry_Leather")?new Color(.055f,.026f,.018f):
                            mat.name.Contains("Sentry_Void")?new Color(.001f,.002f,.005f):
                            mat.name.Contains("MirrorWitch_Obsidian")?new Color(.006f,.008f,.018f):
                            mat.name.Contains("MirrorWitch_Violet")?new Color(.09f,.012f,.15f):
                            mat.name.Contains("MirrorWitch_Antique_Gold")?new Color(.42f,.24f,.07f):
                            mat.name.Contains("MirrorWitch_Mirror_Silver")?new Color(.16f,.21f,.29f):
                            mat.name.Contains("MirrorWitch_Living_Glass")?new Color(.38f,.06f,.72f):
                            mat.name.Contains("MirrorWitch_Porcelain")?new Color(.62f,.66f,.72f):
                            mat.name.Contains("MirrorWitch_Void")?new Color(.001f,.001f,.004f):
                            mat.name.Contains("Basalt")?new Color(.026f,.038f,.058f):
                            mat.name.Contains("Blackened_Steel")?new Color(.035f,.055f,.082f):
                            mat.name.Contains("Honed_Edge")?new Color(.25f,.32f,.39f):
                            mat.name.Contains("Old_Gold")?new Color(.56f,.31f,.075f):
                            mat.name.Contains("Oxblood")?new Color(.18f,.014f,.027f):
                            mat.name.Contains("Leather")?new Color(.032f,.020f,.018f):
                            mat.name.Contains("Visor")?new Color(.002f,.004f,.008f):mat.color;
                        mat.color=color;
                    }
                    if(mat.name.Contains("Living_Gold")||mat.name.Contains("Hexer_Cyan")||mat.name.Contains("Hexer_Magenta")||mat.name.Contains("Sentry_Clockwork_Glow")||mat.name.Contains("MirrorWitch_Living_Glass"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        if(mat.HasProperty("_EmissionColor"))mat.SetColor("_EmissionColor",mat.name.Contains("Hexer_Cyan")?new Color(.02f,1.5f,2.8f):mat.name.Contains("Hexer_Magenta")?new Color(1.6f,.05f,2.5f):mat.name.Contains("Sentry_Clockwork_Glow")?new Color(3.2f,.54f,.035f):mat.name.Contains("MirrorWitch_Living_Glass")?new Color(.82f,.08f,2.3f):new Color(2.4f,.78f,.12f));
                    }
                    if(mat.HasProperty("_Glossiness"))
                    {
                        var metal=mat.name.Contains("Steel")||mat.name.Contains("Gold")||mat.name.Contains("Edge");
                        mat.SetFloat("_Glossiness",metal?.72f:.25f);
                    }
                }
                renderer.materials=materials;
            }
        }

        private void UpdateCombat3D()
        {
            if(!combat3DRoot)return;
            var active=screen==ScreenMode.Combat&&combat!=null&&currentEnemy?.boss!=true;
            if(combat3DRoot.activeSelf!=active)combat3DRoot.SetActive(active);
            if(!active)return;
            if(vanguard3D)vanguard3D.SetActive(run.hero==HeroId.Vanguard);
            if(hexer3D)hexer3D.SetActive(run.hero==HeroId.Hexer);
            if(gildedSentry3D)gildedSentry3D.SetActive(currentEnemy?.id=="gilded_sentry");
            if(mirrorWitch3D)mirrorWitch3D.SetActive(currentEnemy?.id=="mirror_witch");

            if(HasHero3D)
            {
                if(heroDeath>0&&previous3DDeath<=0)PlayHeroClip("Death",.08f);
                else if(heroVictory>0&&previous3DVictory<=0)PlayHeroClip("Victory",.12f);
                else if(heroHit>0&&previous3DHit<=0)PlayHeroClip("Hit",.045f);
                else if(heroBuff>0&&previous3DBuff<=0)PlayHeroClip(playerActionKind==EffectKind.Block?"Guard":"Buff",.07f);
                else if(playerAction>0&&previous3DAction<=0)
                {
                    var clip=playerActionKind==EffectKind.Damage?"Attack":
                        playerActionKind==EffectKind.Block?"Guard":
                        playerActionKind==EffectKind.Strength||playerActionKind==EffectKind.Heal?"Buff":"Cast";
                    PlayHeroClip(clip,.06f);
                }
                if(!ActiveHeroAnimation.isPlaying&&heroDeath<=0)PlayHeroClip(heroVictory>0?"Victory":"Idle",.12f);
            }
            if(HasEnemy3D)
            {
                if(foeDeath>0&&previous3DFoeDeath<=0)PlayEnemyClip("Death",.08f);
                else if(foeHit>0&&previous3DFoeHit<=0)PlayEnemyClip("Hit",.04f);
                else if(enemyAction>0&&previous3DEnemyAction<=0)
                {
                    var clip=combat.intent==IntentKind.Attack?"Attack":combat.intent==IntentKind.Defend?"Defend":
                        combat.intent==IntentKind.Buff?"Buff":combat.intent==IntentKind.Debuff?"Debuff":"Special";
                    PlayEnemyClip(clip,.06f);
                }
                else if(foeBuff>0&&previous3DFoeBuff<=0)PlayEnemyClip(combat.intent==IntentKind.Defend?"Defend":"Buff",.06f);
                if(!ActiveEnemyAnimation.isPlaying&&foeDeath<=0)PlayEnemyClip("Idle",.12f);
            }
            previous3DAction=playerAction;previous3DHit=heroHit;previous3DBuff=heroBuff;previous3DDeath=heroDeath;previous3DVictory=heroVictory;
            previous3DEnemyAction=enemyAction;previous3DFoeHit=foeHit;previous3DFoeBuff=foeBuff;previous3DFoeDeath=foeDeath;
            combat3DCamera.Render();
        }

        private void PlayHeroClip(string clip,float fade)
        {
            var animation=ActiveHeroAnimation;var clips=ActiveHeroClips;
            if(!animation||!clips.ContainsKey(clip))return;animation.CrossFade(clip,fade);
        }

        private void PlayEnemyClip(string clip,float fade)
        {
            var animation=ActiveEnemyAnimation;var clips=ActiveEnemyClips;
            if(!animation||clips==null||!clips.ContainsKey(clip))return;
            animation.CrossFade(clip,fade);
        }

        private void DrawCombat3DStage(float w,float h)
        {
            if(!HasCombat3DStage)return;
            var old=GUI.color;GUI.color=new Color(1,1,1,.99f);
            GUI.DrawTexture(new Rect(0,126,w,370),combat3DTexture,ScaleMode.StretchToFill,false);
            GUI.color=old;
            Fill(new Rect(0,126,w,370),new Color(.008f,.012f,.022f,.07f));
        }

        private void DisposeCombat3D()
        {
            if(combat3DTexture){combat3DTexture.Release();Destroy(combat3DTexture);}
            if(combat3DRoot)Destroy(combat3DRoot);
        }
    }
}
