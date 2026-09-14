using System.Collections;
using System.Linq;
using GildedFate.Core;
using GildedFate.Combat;
using GildedFate.Map;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureRelicCapture(string mode)
        {
            if(!mode.StartsWith("relic-new-"))return false;
            run.NewRun(HeroId.Vanguard,20260908);
            if(mode=="relic-new-choice"||mode=="relic-new-input"){
                run.AddCard("living_armor");run.AcquireRelic("perfected_thread");screen=ScreenMode.Map;return true;
            }
            if(mode=="relic-new-effects"){
                PrepareCombatCheck("strike",5);
                combat.relics.AddRange(new[]{"funeral_bell","grave_lantern","the_golden_cycle"});
                run.relics.AddRange(combat.relics.Where(id=>!run.relics.Contains(id)));
                var positive=new[]{"reverberation","adaptation","chimera","afterlife","foresight","resolve","ferocity","preparation","phantom_edge"};
                foreach(var id in positive)combat.player.effects.Add(new CombatEffectState{id=id,source="crown_of_echoes",value=1});
                foreach(var id in new[]{"rupture","condemned","wither","death_knell"})combat.enemy.effects.Add(new CombatEffectState{id=id,source="black_star_of_ruin",value=1});
                combat.hand[2].perfected=true;combat.hand[2].perfectedGrowth=3;
                combat.TakeEvents();RestoreCombatPresentation();return true;
            }
            screen=ScreenMode.Collection;collectionRelics=true;
            var page=mode=="relic-new-2"?9:mode=="relic-new-3"?12:6;
            var w=Screen.width/Mathf.Max(.35f,Mathf.Min(Screen.width/1440f,Screen.height/810f));
            var tile=Mathf.Min(184f,(w-210-36-13*5)/6);
            collectionScroll=page*(tile*1.17f+14);
            if(mode=="relic-new-inspect")inspectedRelic=GameContent.Relics.First(r=>r.id=="perfected_thread");
            return true;
        }
        private IEnumerator RunRelicInteractionChecks()
        {
            CombatCheck(PerfectedScreenOpen,"Perfected acquisition opens required selection");
            for(var i=0;i<3;i++){
                CombatCheck((int)run.PerfectedChoiceKind==i,"Perfected selects Attack, Skill, then Power");
                var selected=run.PerfectedEligible().First();ChoosePerfectedCard(selected);
                CombatCheck(selected.perfected,"Selected exact physical card receives permanent badge");
                yield return new WaitForSecondsRealtime(1.8f);
            }
            CombatCheck(!run.perfectedSelectionPending&&run.cards.Count(c=>c.perfected)==3,"All three choices complete without duplicate modifiers");
            var loaded=JsonUtility.FromJson<RunModel>(JsonUtility.ToJson(run));
            CombatCheck(loaded.cards.Count(c=>c.perfected)==3&&!loaded.perfectedSelectionPending,"Unity run serialization preserves selected modifiers");
            PrepareCombatCheck("strike",5);combat.relics.Add("sovereign_seal");combat.hand[2].perfected=true;
            var hp=combat.enemy.hp;yield return WaitForCombatQueue();MajorDrag(2,EnemyDropZone.center);yield return WaitForCombatQueue();
            CombatCheck(hp-combat.enemy.hp==15,"Drag-release combines Perfected growth with Sovereign Seal");
            CombatCheck(combat.exhaust.Any(c=>c.id=="strike"&&c.perfectedGrowth==1),"Perfected played Attack Exhausts with retained growth");
            var checkpoint=JsonUtility.FromJson<CombatCheckpoint>(JsonUtility.ToJson(combat.CaptureCheckpoint()));
            CombatCheck(checkpoint.TryRestore(out var restored,out var reason),"Unity exact relic checkpoint restores: "+reason);
            CombatCheck(restored?.exhaust.Any(c=>c.perfected&&c.perfectedGrowth==1)==true,"Unity checkpoint retains growth and ownership");
            Debug.Log($"[Gilded Fate Relics] {combatInteractionChecks} checks · {combatInteractionFailures} failures");
        }
    }
}
