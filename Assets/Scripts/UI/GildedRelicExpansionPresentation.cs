using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;
using GildedFate.Combat;
using GildedFate.Map;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private readonly List<(string id,float time)> relicPulseBeats=new();
        private float RelicPulseGrowth(string id,float now)
        {
            var growth=0f;
            foreach(var beat in relicPulseBeats){var age=now-beat.time;if(beat.id==id&&age>=0&&age<.38f)growth=Mathf.Max(growth,Mathf.Sin(age/.38f*Mathf.PI)*(profile.reduceMotion?2:6));}
            return growth;
        }
        private void DrawRelicCounter(Rect r,string id)
        {
            if(screen!=ScreenMode.Combat||combat==null)return;
            var progress=combat.RelicProgress(id);if(string.IsNullOrEmpty(progress)||!progress.Contains("/"))return;
            var text=progress.Split(' ')[0];var bounds=new Rect(r.x,r.yMax-8,r.width,20);
            var style=new GUIStyle(footerStyle){fontSize=12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.black}};
            GUI.Label(new Rect(bounds.x+1,bounds.y+1,bounds.width,bounds.height),text,style);style.normal.textColor=Color.white;GUI.Label(bounds,text,style);
        }
        private string PermanentRelicCardRules(CardDef card)
        {
            var text=card.text??"";
            if(card.perfected&&card.perfectedGrowth>0&&card.kind!=CardKind.Power){
                var growth=card.perfectedGrowth;
                var pattern=card.kind==CardKind.Attack?@"(?i)(Deal\s+)(\d+)(\s+damage)":@"(?i)(Gain\s+)(\d+)(\s+Block)";
                text=System.Text.RegularExpressions.Regex.Replace(text,pattern,m=>m.Groups[1].Value+(int.Parse(m.Groups[2].Value)+growth)+m.Groups[3].Value);
            }
            if(card.perfected&&card.kind!=CardKind.Power&&text.IndexOf("Exhaust",StringComparison.OrdinalIgnoreCase)<0)text+="\nExhaust.";
            return text;
        }
        private static readonly string[] RelicEffectTitles={"RUPTURE","UNWRITTEN","REVERBERATION","ADAPTATION","CHIMERA","CONDEMNED","FORESIGHT","WITHER","AFTERLIFE","FEROCITY","PREPARATION","DEATH KNELL","PHANTOM EDGE","DESTINED","RESOLVE","ECHOED","PERFECTED ATTACK","PERFECTED SKILL","PERFECTED POWER","HIDDEN POTENTIAL ATTACK","HIDDEN POTENTIAL SKILL","HIDDEN POTENTIAL POWER","PARADOX","CONVERGENCE","COST REDUCTION"};
        private bool DrawRelicEffectIcon(Rect r,string title)
        {
            var i=Array.IndexOf(RelicEffectTitles,title);
            if(i>=0){var art=LoadAuthoredArt("Art/Powers/MajorRelicEffects");if(art){DrawAtlasIcon(art,i,5,5,r);return true;}}
            var source=title switch{"RELIC NEXT ATTACK"=>"frayed_cord","CRACKED READY"=>"cracked_hourglass","BLOODTHREAD READY"=>"bloodstained_thread","GAUNTLET READY"=>"dented_gauntlet","HOLLOW CROWN READY"=>"hollow_crown","RIBBON DISCOUNT"=>"war_torn_ribbon","OPPOSITES READY"=>"chain_of_opposites","DESTINED DISCOUNT"=>"broken_compass","MANY PATHS FREE"=>"crown_of_many_paths",_=>title.ToLowerInvariant().Replace("'","").Replace(' ','_')};
            var index=Array.FindIndex(GameContent.Relics,x=>x.id==source);if(index<36)return false;DrawRelicArt(r,index);return true;
        }
        private static string RelicEffectDetail(CombatEffectState e)=>e.id switch
        {
            "rupture"=>"The next incoming Attack deals 50% more damage on each hit, then consumes Rupture.",
            "unwritten"=>$"{e.value} physical cards cost 1 less for this entire combat. Look for their golden-script badge.",
            "reverberation"=>"The next triggered effect caused by a card, Power or buff activates one extra time. Consume 1. Does not replay the card; cannot recursively duplicate itself.",
            "adaptation"=>"Your next applicable card of a different type from the pair that granted this gains 50% damage or Block. Non-applicable cards do not consume it.",
            "chimera"=>$"Your next {e.value} card(s) each count as all origins and gain 25% damage and Block where applicable. Each card consumes exactly 1 stack. More stacks affect more cards, not a larger bonus on one card.",
            "condemned"=>$"Every fifth incoming hit deals 12 additional damage. {Math.Max(0,e.value-1)}/5 hits since the last bonus.",
            "foresight"=>"The next card drawn costs 1 less until played. Consume 1 Foresight when it is drawn.",
            "wither"=>$"Reduce this enemy's next buff gain by {e.value} stack(s), then remove Wither. Does not remove Block or existing buffs.",
            "afterlife"=>"The next non-Temporary card that would Exhaust goes to discard instead. Consume 1. Installed Powers still leave circulation normally.",
            "ferocity"=>"Your next Attack deals 25% more damage on each hit. Consume 1.",
            "preparation"=>"Your next applicable card with printed cost 0 or 1 gains +5 damage or Block. Consume 1.",
            "death_knell"=>"The next non-Attack damage this enemy takes repeats once, then removes Death Knell. The repeat cannot retrigger itself.",
            "phantom_edge"=>"Your next Attack adds one separate 4-damage hit. Consume 1.",
            "destined"=>"Playing the marked physical card this turn makes your next card cost 1 less this turn.",
            "resolve"=>"When you would lose all your Block between turns, retain 50% instead (rounded up). Consume 1 only when this protects Block.",
            "echoed"=>"This card's primary effect resolves one extra time, without another payment or recursive card-play trigger.",
            "paradox"=>"The next DIFFERENT triggered effect activates twice. Once per turn; nested duplication cannot recursively arm it again.",
            "relic_next_attack"=>$"Your next Attack gains +{e.value} damage on each hit.",
            "cracked_ready"=>$"Your next applicable card with printed cost 2+ gains +{e.value} damage or Block.",
            "bloodthread_ready"=>$"Your next applicable card gains +{e.value} damage or Block.",
            "gauntlet_ready"=>$"Your next Block-granting Skill this turn gains +{e.value} Block.",
            "hollow_crown_ready"=>$"Your next card gains +{e.value} damage and Block where applicable. If it does neither, draw {e.value/5} after it resolves.",
            "opposites_ready"=>$"The next debuff you apply this turn gains +{e.value} stack(s).",
            "ribbon_discount"=>$"Your next Power this turn costs {e.value} less.",
            "destined_discount"=>$"Your next card this turn costs {e.value} less.",
            "many_paths_free"=>"Your next card this turn costs 0.",
            "fate_die"=>$"Your first {(CardKind)(e.value-1)} this combat costs 1 less.",
            _=>null
        };
        private void AddRelicProgressChips(List<CombatEffectChip> chips,Color color)
        {
            foreach(var id in new[]{"funeral_bell","grave_lantern","hollow_hourglass","the_golden_cycle","fates_convergence","crown_of_many_paths","soul_of_the_chimera"})
            {
                if(!combat.relics.Contains(id))continue;var relic=GameContent.Relics.First(x=>x.id==id);var detail=combat.RelicProgress(id);var shortText=detail.Split(' ')[0];
                chips.Add(new CombatEffectChip("REL",relic.name,relic.text+"\n\n"+detail,1,color,true,shortText));
            }
        }
        private string RelicCardDetail(CardDef c)
        {
            if(c==null)return "";
            if(screen==ScreenMode.Combat&&combat!=null)return combat.RelicCardModification(c);
            if(!c.perfected)return "";
            var effect=c.kind==CardKind.Power?$"Each play permanently reduces its cost by 1. Current reduction: {c.perfectedCostReduction}.":$"Each play permanently adds +1 {(c.kind==CardKind.Attack?"damage":"Block")} to each applicable instance, then Exhausts. Current growth: +{c.perfectedGrowth}.";
            return $"Perfected {c.kind}. {effect}\nSource: Perfected Thread. Permanent for this run.";
        }
        private void DrawRelicModifierTooltip(ref float y,float x,float width,CardDef c)
        {
            var detail=RelicCardDetail(c);if(string.IsNullOrEmpty(detail))return;var style=new GUIStyle(subtitleStyle){fontSize=13,wordWrap=true,alignment=TextAnchor.UpperLeft};
            var height=style.CalcHeight(new GUIContent(detail),width-28)+24;var r=new Rect(x,y,width,height);Fill(r,new Color(.012f,.016f,.025f,.99f));Outline(r,Gold,1);GUI.Label(new Rect(x+14,y+12,width-28,height-24),detail,style);y+=height+6;
        }
        private bool PerfectedScreenOpen=>run.perfectedSelectionPending&&ShowsPersistentRunHud&&screen!=ScreenMode.Combat&&screen!=ScreenMode.Settings;
        private void ChoosePerfectedCard(RunCard card)
        {
            if(acquisitionActive||!run.ChoosePerfected(card))return;SaveService.Save(run);screenControllerIndex=0;cardChoiceScroll=0;BeginModificationAcquisition(card.BuildDefinition(),()=>{},perfected:true);
        }
        private void DrawPerfectedSelection(float w,float h)
        {
            DrawLocationBackdrop(w,h,0);DrawRunHud(w);Heading(w,"PERFECTED THREAD","CHOOSE ONE "+GameplayTerms.Display(run.PerfectedChoiceKind.ToString().ToUpperInvariant())+" · PERMANENT FOR THIS RUN");
            var choices=run.PerfectedEligible();DrawPhysicalCardChoices(w,h,choices,ChoosePerfectedCard);
            GUI.Label(new Rect(w*.2f,h-78,w*.6f,42),"Attack: +1 damage per play, Dissipate. Skill: +1 Block per play, Dissipate.\nAspect: permanently costs 1 less after each play. Existing Bindings are preserved.",new GUIStyle(subtitleStyle){fontSize=14,wordWrap=true});
            var save=new Rect(28,h-66,190,40);DrawButtonFrame(save,save.Contains(PointerPosition),false);if(GUI.Button(save,"SAVE & MAIN MENU",buttonStyle)){SaveService.Save(run);screen=ScreenMode.Menu;}
            DrawScreenCardKeywordHelp(w,h);DrawAcquisitionPresentation(w,h);
        }
    }
}
