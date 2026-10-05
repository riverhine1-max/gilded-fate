using System.Collections.Generic;
using System.Linq;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    // Presentation for themed enemies (Ashen Wilds): the Minion badge, Summon / Command
    // intent art, enemy-specific state text, short callouts for mechanic hooks, the
    // Cinder Alpha's phase art and motion profiles that reuse the existing animations.
    // Rules live in Combat/WildCombat.cs; nothing here changes combat state.
    public sealed partial class GildedMainMenu
    {
        private sealed class WildCallout{public int index;public string text;public float at;public Color color;}
        private readonly List<WildCallout> wildCallouts=new();

        // Cinder Alpha art follows its phase: Art/Enemies/ashen_wilds_cinder_alpha(_phase2|_phase3).
        private string EnemyArtId(string id)
        {
            if(id!=AshenWildsContent.Alpha||combat==null||screen!=ScreenMode.Combat)return id;
            var boss=combat.WildBossIndex;var phase=boss>=0?combat.MindAt(boss)?.phase??1:1;
            return phase>=3?id+"_phase3":phase==2?id+"_phase2":id;
        }
        private void RefreshEnemyArt(int index)
        {
            if(combat==null||index<0||index>=finalEnemyDefs.Length)return;
            var def=WorldContent.Enemies.FirstOrDefault(e=>e.id==combat.EnemyIdAt(index));
            finalEnemyDefs[index]=def;finalEnemyTextures[index]=def==null?null:LoadAuthoredArt(GildedArtCatalog.EnemyResource(EnemyArtId(def.id)));
        }

        // Combat hook receipts (CombatEventKind.Hook). Final animation work attaches here later.
        private void ScheduleWildHook(CombatEvent fact,float at)
        {
            var name=fact.label!=null&&fact.label.StartsWith("HOOK:")?fact.label.Substring(5):fact.label??"";
            string text=null;var color=new Color(1f,.72f,.36f);
            switch(name)
            {
                case "cinder_alpha_phase2":case "cinder_alpha_phase3":RefreshEnemyArt(fact.enemyIndex);return; // the boss phase cinematic owns the banner
                case "cinder_alpha_minion_flees":text="FLEES";color=new Color(.8f,.8f,.78f);break;
                case "burned_hart_splintered":RefreshEnemyArt(fact.enemyIndex);text="THE CROWN SPLINTERS";color=new Color(1f,.55f,.3f);break;
                case "burned_hart_bare":text="THE CROWN BREAKS · BARE";color=new Color(1f,.4f,.25f);break;
                case "mourning_cry":text="MOURNING CRY";color=new Color(.75f,.82f,1f);break;
                case "rootcaller_summon":text="SUMMON";color=new Color(.6f,1f,.6f);break;
                case "rootcaller_command":case "packmother_command":case "cinder_alpha_command":text="COMMAND";color=new Color(1f,.85f,.45f);break;
                case "command_answer":text="ANSWERS THE COMMAND";color=new Color(1f,.85f,.45f);break;
                case "packmother_bereaved_fury":text="BEREAVED FURY";color=new Color(1f,.35f,.3f);break;
                case "minion_withers":case "sapling_death":text=name=="sapling_death"?"WITHERS":"WITHERS WITH ITS OWNER";color=new Color(.7f,.7f,.66f);break;
                case "support_flees":text="FLEES";color=new Color(.8f,.8f,.78f);break;
                case "thornjaw_bramble_coil":text="COILING · THORNBURST NEXT";color=new Color(.7f,1f,.5f);break;
                case "root_titan_uproot":text="UPROOTED";color=new Color(1f,.6f,.3f);break;
                case "root_titan_root":text="ROOTED";color=new Color(.6f,.9f,.55f);break;
                default:
                    if(name.StartsWith("hollow_maw_state:")){text=name.Substring(17);color=text=="BURNING"?new Color(1f,.42f,.2f):text=="FED"?new Color(.6f,.9f,.6f):new Color(1f,.85f,.6f);}
                    break;
            }
            if(text!=null)wildCallouts.Add(new WildCallout{index=fact.enemyIndex,text=text,at=at,color=color});
        }
        private void DrawWildCallouts()
        {
            if(combat==null||wildCallouts.Count==0)return;var now=Time.unscaledTime;
            wildCallouts.RemoveAll(c=>now>c.at+1.6f);
            foreach(var c in wildCallouts)
            {
                var age=now-c.at;if(age<0||c.index<0||c.index>=combat.EnemyCount)continue;
                var body=GroupCombat?GroupPortrait(c.index):EnemyPortraitRect;
                var alpha=Mathf.Clamp01(age/.15f)*Mathf.Clamp01((1.6f-age)/.4f);var rise=profile.reduceMotion?0:age*14;
                var r=new Rect(body.center.x-150,body.y+body.height*.35f-rise,300,30);
                ShadowLabel(r,c.text,new GUIStyle(titleStyle){font=headingFont?headingFont:labelFont,fontSize=GroupCombat?17:21,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(c.color.r,c.color.g,c.color.b,alpha)}});
            }
        }

        // Minion badge: drawn at the top-left of a Minion's body.
        private void DrawMinionBadge(int index,Rect portrait)
        {
            if(combat==null||!combat.IsMinionAt(index))return;
            var art=MetaArt("MinionIcon");var size=Mathf.Clamp(portrait.width*.32f,28,40);
            var r=new Rect(portrait.x-4,portrait.y+2,size,size);
            Fill(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),new Color(.04f,.03f,.02f,.72f));Outline(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),new Color(.95f,.75f,.36f,.85f),1);
            if(art)GUI.DrawTexture(new Rect(r.x+4,r.y+4,r.width-8,r.height-8),art,ScaleMode.ScaleToFit,true);
            else GUI.Label(r,"M",new GUIStyle(titleStyle){fontSize=16,alignment=TextAnchor.MiddleCenter});
            if(CombatInspectionAllowed&&r.Contains(combatPointer))SetCombatEffectTooltip("MINION","Owned creature. Dies when its Owner dies. Gives no reward of its own.",r.center);
        }
        // Summon and Command intents use their own artwork instead of the intent atlas.
        private bool DrawWildIntentIcon(EnemyIntentAction action,Rect icon)
        {
            if(action.Icon<EnemyIntentAction.IconSummon)return false;
            var art=MetaArt(action.Icon==EnemyIntentAction.IconSummon?"SummonIcon":"MinionIcon");
            if(art)GUI.DrawTexture(new Rect(icon.x+icon.width*.1f,icon.y+icon.height*.08f,icon.width*.8f,icon.height*.84f),art,ScaleMode.ScaleToFit,true);
            else GUI.Label(icon,action.Icon==EnemyIntentAction.IconSummon?"+":"⟳",new GUIStyle(titleStyle){fontSize=22,alignment=TextAnchor.MiddleCenter});
            return true;
        }
        private string WildTooltipSuffix(int index)
        {
            if(combat==null||!combat.wildCombat)return "";var text=combat.WildStateText(index);
            return string.IsNullOrEmpty(text)?"":"\n\n"+text;
        }

        // Ashen Wilds motion reuses the existing animation archetypes until bespoke
        // animation sheets arrive.
        private static EnemyMotionProfile WildMotionFor(string id)=>id switch
        {
            AshenWildsContent.Cinderfang=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Bite,.75f,0,1.5f,76,new Color(1f,.48f,.16f)),
            AshenWildsContent.Grazer=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Chomp,1.15f,0,1f,60,new Color(.86f,.62f,.32f)),
            AshenWildsContent.Emberwing=>new(EnemyMotionKind.Spirit,EnemyStrikeStyle.Bolt,.6f,10,1.2f,40,new Color(1f,.62f,.26f)),
            AshenWildsContent.Thornjaw=>new(EnemyMotionKind.Crawler,EnemyStrikeStyle.Chomp,.95f,0,1.1f,58,new Color(.72f,.9f,.36f)),
            AshenWildsContent.Stalker=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,.65f,0,1.6f,82,new Color(.82f,.78f,.7f)),
            AshenWildsContent.Rootcaller=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.9f,4,1f,20,new Color(1f,.58f,.22f)),
            AshenWildsContent.Sapling=>new(EnemyMotionKind.Crawler,EnemyStrikeStyle.Fang,.5f,0,1.5f,44,new Color(.7f,.95f,.4f)),
            AshenWildsContent.Elk=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,1.05f,0,.9f,64,new Color(.72f,.82f,1f)),
            AshenWildsContent.Maw=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Chomp,1.2f,0,.9f,48,new Color(1f,.42f,.14f)),
            AshenWildsContent.Packmother=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,1.25f,0,1f,76,new Color(1f,.5f,.2f)),
            AshenWildsContent.FangPup=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Bite,.5f,0,1.8f,58,new Color(.9f,.8f,.7f)),
            AshenWildsContent.AshbackCub=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Slam,.7f,0,1.2f,52,new Color(1f,.62f,.3f)),
            AshenWildsContent.Hart=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Cleave,1.2f,0,.95f,70,new Color(1f,.66f,.26f)),
            AshenWildsContent.Titan=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Crush,1.6f,0,.65f,36,new Color(1f,.55f,.2f)),
            AshenWildsContent.Alpha=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,1.45f,0,.95f,84,new Color(1f,.45f,.12f)),
            AshenWildsContent.Whelp=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Bite,.55f,0,1.7f,62,new Color(1f,.5f,.2f)),
            AshenWildsContent.Runner=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,.5f,0,1.9f,70,new Color(1f,.6f,.25f)),
            _=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1f,0,1f,50,new Color(1f,.62f,.36f))
        };
        private static float WildBodyScale(EnemyDef enemy)=>enemy?.id switch
        {
            AshenWildsContent.Cinderfang=>.82f,AshenWildsContent.Grazer=>.98f,AshenWildsContent.Emberwing=>.95f,AshenWildsContent.Thornjaw=>.98f,
            AshenWildsContent.Stalker=>.8f,AshenWildsContent.Rootcaller=>1.02f,AshenWildsContent.Sapling=>.55f,AshenWildsContent.Elk=>1.05f,
            AshenWildsContent.Maw=>1.12f,AshenWildsContent.Packmother=>1.25f,AshenWildsContent.FangPup=>.58f,AshenWildsContent.AshbackCub=>.64f,
            AshenWildsContent.Hart=>1.25f,AshenWildsContent.Titan=>1.4f,AshenWildsContent.Alpha=>1.45f,AshenWildsContent.Whelp=>.62f,AshenWildsContent.Runner=>.58f,
            _=>enemy?.boss==true?1.4f:enemy?.elite==true?1.2f:1f
        };
    }
}
