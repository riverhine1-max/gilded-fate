using System;
using System.Collections;
using System.Linq;
using GildedFate.Combat;
using GildedFate.Core;
using GildedFate.Map;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureQuickPlaytestCapture(string mode)
        {
            if(!mode.StartsWith("quick-"))return false;
            combatTestInput=true;AudioListener.pause=true;profile.fastMode=true;
            if(mode.StartsWith("quick-boss"))
            {
                for(var seed=9123;seed<9323;seed++)
                {
                    run.NewRun(HeroId.Reaper,seed);currentNode=run.nodes.First(n=>n.kind==NodeKind.Boss);
                    run.floor=run.activeNodeFloor=currentNode.floor;run.activeNodeLane=currentNode.lane;run.RollEncounterRewards(NodeKind.Boss);
                    if(mode=="quick-boss"||run.BossRelicOffers().Any(r=>r.id=="perfected_thread"))break;
                }
                run.encounterRewards.cardClaimed=true;run.stage=RunStage.RelicReward;screen=ScreenMode.RelicReward;return true;
            }
            if(mode.StartsWith("quick-unbound"))
            {
                run.NewRun(HeroId.Reaper,9123);run.AcquireRelic("unbound_deck");currentNode=run.nodes.First(n=>n.floor==0&&n.available);
                run.activeNodeFloor=currentNode.floor;run.activeNodeLane=currentNode.lane;run.RollEncounterRewards(NodeKind.Combat);
                if(mode.EndsWith("many"))run.AddEncounterCardChoices(4);
                run.combatGoldClaimed=true;run.encounterRewards.relicClaimed=run.encounterRewards.shardClaimed=true;
                run.stage=RunStage.CardReward;screen=ScreenMode.Reward;rewardRevealTime=0;return true;
            }
            if(mode=="quick-cards")
            {
                run.NewRun(HeroId.Hexer,9123);screen=ScreenMode.Collection;
                bindingCaptureCards=new[]{GameContent.Find("doom"),GameContent.Find("hex"),GameContent.Find("soul"),GameContent.Find("decay"),GameContent.Find("flame_wave"),GameContent.Find("defend")}.Where(c=>c!=null).ToArray();return true;
            }
            if(mode=="quick-map")
            {
                run.NewRun(HeroId.Reaper,9123);
                for(var i=0;i<3;i++)run.AdvanceFrom(run.nodes.First(n=>n.floor==run.floor&&n.available));
                mapFocusFloor=-1;screen=ScreenMode.Map;return true;
            }
            if(mode=="quick-event"){ConfigureFinalPolishCapture("polish-final-event");run.AddShard("bloodstone");return true;}
            var count=mode.EndsWith("two")?2:mode.EndsWith("three")?3:mode.EndsWith("four")?4:1;
            PrepareQuickFormation(count);return true;
        }
        private void PrepareQuickFormation(int count)
        {
            run.NewRun(HeroId.Reaper,9123);var ids=new[]{"rune_mage","vault_rat","chained_brute","masked_acolyte"}.Take(count).ToArray();
            currentEnemy=WorldContent.Enemies.First(e=>e.id==ids[0]);currentNode=run.nodes.First(n=>n.floor==0&&n.available);
            run.activeEncounterEnemies=ids.ToList();run.activeEnemyId=ids[0];run.stage=RunStage.Combat;
            combat=new CombatState();combat.Begin(run.hero,BuildCombatDeck(),currentEnemy.hp,0,72,72,null,ids[0],currentEnemy.baseDamage,9123,false,"",false,ids);
            for(var i=0;i<count;i++){combat.EnemyAt(i).strength=2;combat.EnemyAt(i).burn=i+1;}
            screen=ScreenMode.Combat;bossIntroTime=0;ResetCombatPresentation();
        }
        private IEnumerator RunQuickPlaytestChecks()
        {
            foreach(var word in new[]{"Burn","burn","BURN"})
            {
                var formatted=FormatCardRules("Apply 3 "+word+".");
                CombatCheck(formatted.Contains("<color=#FF8A45>3 "+word+"</color>"),"Burn word and unchanged value use canonical color: "+word);
                CombatCheck(StripRichTags.Replace(formatted,"")=="Apply 3 "+word+".","Formatting preserves authored wording");
                CombatCheck(FormatCardRules(formatted)==formatted,"Mechanic formatting is idempotent");
            }
            foreach(var key in RuleKeywords)
                CombatCheck(FormatCardRules("2 "+key.term).Contains("<color=#"+key.hex+">2 "+GameplayTerms.Display(key.term)+"</color>"),"Shared canonical word/value: "+key.term);
            var dynamic="Apply <color=#64E884>5</color> Burn. Gain <color=#FF7568>2</color> Block.";
            var rich=FormatCardRules(dynamic);
            CombatCheck(rich.Contains("<color=#64E884>5</color>")&&rich.Contains("<color=#FF7568>2</color>"),"Dynamic green/red numbers remain untouched");
            CombatCheck(FormatCardRules("Soulbound").Contains("#70E6DA")&&!FormatCardRules("Soulbound").Contains("#65E6D2"),"Longest mechanic matches once without Soul inside Soulbound");
            CombatCheck(FormatCardRules("Add 2 Souls.").Contains("#65E6D2>2 Souls"),"Plural Souls retain Reaper color");
            var curse=GameContent.Find("doom");var normal=GameContent.Find("strike");
            CombatCheck(CardCharacterColor(curse).maxColorComponent<.12f&&CardRulesPanelColor(curse).maxColorComponent<.08f&&CardTitleRarityColor(curse).maxColorComponent<.14f,"Curse frame, body and header are distinctly black");
            CombatCheck(Mathf.Min(ReadableCardTitleColor(curse).r,ReadableCardTitleColor(curse).g,ReadableCardTitleColor(curse).b)>.85f&&CardRulesPanelColor(normal).r>.20f,"Curse titles stay readable and normal cards are unchanged");
            CombatCheck(RouteMapButton(CombatWidth).x<RunDeckControlRect(CombatWidth).x&&RunDeckControlRect(CombatWidth).x<CombatWidth-67,"Map / Deck / Settings is the visible order");
            foreach(var mode in new[]{ScreenMode.Event,ScreenMode.EventSelection,ScreenMode.EventResult})
            {screen=mode;run.eventSelectionKind=EventSelectionKind.Card;CombatCheck(!ShowsNormalShardShrine,"Normal event surface owns shard space: "+mode);}
            screen=ScreenMode.EventSelection;run.eventSelectionKind=EventSelectionKind.ShardReward;
            CombatCheck(ShowsNormalShardShrine,"Dedicated shard choice restores its shard UI");
            screen=ScreenMode.Map;
            for(var i=0;i<2;i++)
            {
                CombatCheck(ShrineSocket(i).xMin>=2&&ShrineStateLabel(i).yMax<CombatHeight-120,"Complete shard socket and EMPTY label fit: "+i);
                if(i==0)CombatCheck(ShrineStateLabel(i).yMax<ShrineSocket(1).y-5,"Shard labels and socket outlines have breathing room");
            }
            ConfigureQuickPlaytestCapture("quick-map");var here=CurrentMapLocation;
            CombatCheck(here!=null&&here.complete&&here.floor==run.floor-1,"Map marks actual visited node, not all reachable choices");
            FocusMapToCurrentFloor(CombatWidth,CombatHeight);var viewport=MapViewport(CombatWidth,CombatHeight);var p=MapPosition(here,viewport.width)-Vector2.up*mapScroll;
            CombatCheck(p.y>50&&p.y<viewport.height-60,"Map opens with complete current-location marker visible");
            foreach(var count in new[]{1,2,3,4})
            {
                PrepareQuickFormation(count);yield return WaitForCombatQueue();
                var center=CombatWidth*.695f;
                if(count>1)CombatCheck(Mathf.Abs((GroupCell(0).center.x+GroupCell(count-1).center.x)*.5f-center)<.1f,"Formation centered for "+count+" enemies");
                if(count==2)CombatCheck(GroupCell(1).center.x-GroupCell(0).center.x<=244.1f,"Two enemies use close spacing, not four-enemy endpoints");
                for(var i=0;i<count;i++)
                {
                    var body=count>1?GroupPortrait(i):EnemyPortraitRect;var hit=count>1?GroupDropZone(i):EnemyDropZone;
                    CombatCheck(hit.Contains(body.center)&&TargetAt(body.center)==i,"Visible enemy body selects its own target: "+count+" / "+i);
                    CombatCheck(hit.width<=body.width+24.1f&&hit.height<=body.height+20.1f,"Target has only a comfortable body margin: "+count+" / "+i);
                    for(var j=i+1;j<count;j++)CombatCheck(!hit.Overlaps(GroupDropZone(j)),"Neighbor targets never overlap");
                    WithEnemyPresentation(i,()=>{var chips=EnemyEffectChips(i);CombatCheck(chips.Any(c=>c.title=="STRENGTH")&&chips.Any(c=>c.title=="BURN"),"Active enemy buffs/debuffs remain visible");CombatCheck(!chips.Any(c=>c.title==combat.mechanicTitle),"Passive descriptor is inspection-only");});
                    CombatCheck(EnemyIntents(i).Count>0,"Existing compound intent plan remains present");
                }
            }
            foreach(var id in new[]{"vault_rat","vault_spider","golden_wisp"})CombatCheck(EnemyBodyScale(WorldContent.Enemies.First(e=>e.id==id))<.7f,"Small creature identity: "+id);
            CombatCheck(EnemyBodyScale(WorldContent.Enemies.First(e=>e.id=="vault_mother"))>EnemyBodyScale(WorldContent.Enemies.First(e=>e.id=="chained_brute")),"Boss is larger than brute");
            ConfigureQuickPlaytestCapture("quick-boss");var bossOffers=run.BossRelicOffers();
            CombatCheck(bossOffers.Length==3,"Boss reward screen offers three existing Boss relics");
            foreach(var relic in GameContent.Relics.Where(r=>r.rarity==Rarity.Boss))
            {
                var area=new Rect(0,0,262,132);var layout=FitReadableText(FormatCardRules(relic.text),area,17,14);
                CombatCheck(layout.Height<=area.height-2&&layout.widest<=area.width-4,"Boss relic description fits its reward tile: "+relic.id);
            }
            var bossSave=JsonUtility.ToJson(run);var reloaded=JsonUtility.FromJson<RunModel>(bossSave);
            CombatCheck(reloaded.BossRelicOffers().Select(r=>r.id).SequenceEqual(bossOffers.Select(r=>r.id)),"Unity JSON preserves exact Boss relic offers");
            var chosen=bossOffers.First(r=>r.id!="perfected_thread");var beforeRelics=run.relics.Count;var beforeCollected=profile.relicsCollected;
            ClaimBossRelicChoice(chosen);ClaimBossRelicChoice(chosen);
            CombatCheck(acquisitionActive&&run.relics.Count==beforeRelics+1&&run.encounterRewards.bossRelicClaimed,"Boss click claims once and uses existing relic acquisition");
            CombatCheck(profile.relicsCollected==beforeCollected+1,"Boss relic profile counter increments exactly once");
            yield return new WaitForSecondsRealtime(1.5f);
            CombatCheck(run.act==2&&screen==ScreenMode.Fateweave,"Boss relic acquisition continues to existing next-act Fateweave");
            ConfigureQuickPlaytestCapture("quick-unbound");var mixed=RewardCards();
            CombatCheck(mixed.Length==4&&mixed.Any(c=>c.hero!=run.hero),"Unbound reward screen exposes its fourth choice and foreign/Wanderer sources");
            var mixedSave=JsonUtility.FromJson<RunModel>(JsonUtility.ToJson(run));
            CombatCheck(mixedSave.HasValidRewardState()&&mixedSave.encounterRewards.cards.SequenceEqual(run.encounterRewards.cards),"Unity JSON keeps valid mixed-origin Unbound choices");
            var foreign=mixed.First(c=>c.hero!=run.hero);var deckCount=run.cards.Count;
            CombatCheck(run.ClaimEncounterCard(foreign.id)&&run.cards.Count==deckCount+1,"An offered cross-origin card can actually be collected");
            BeginCardAcquisition(foreign,Advance);yield return new WaitForSecondsRealtime(1.5f);
            CombatCheck(!acquisitionActive&&screen==ScreenMode.Map,"Unbound card collection returns to the map normally");
            ConfigureQuickPlaytestCapture("quick-boss-perfect");run.AddCard("endless_harvest");
            ClaimBossRelicChoice(run.BossRelicOffers().First(r=>r.id=="perfected_thread"));yield return new WaitForSecondsRealtime(1.5f);
            CombatCheck(run.act==1&&PerfectedScreenOpen,"Perfected Thread holds the boss exit for its existing choices");
            for(var i=0;i<3;i++)
            {
                ChoosePerfectedCard(run.PerfectedEligible().First());var deadline=Time.unscaledTime+5;
                while(acquisitionActive&&Time.unscaledTime<deadline)yield return null;
                yield return new WaitForEndOfFrame();yield return null;
            }
            // Screen transitions happen during GUI rendering. Wait for the state,
            // not an arbitrary wall-clock delay on an inactive wide QA desktop.
            var exitDeadline=Time.unscaledTime+5;
            while(run.act==1&&!run.perfectedSelectionPending&&Time.unscaledTime<exitDeadline)yield return null;
            CombatCheck(run.act==2&&!run.perfectedSelectionPending&&screen==ScreenMode.Fateweave,$"Completing Perfected choices resumes the boss reward exit (act {run.act}, pending {run.perfectedSelectionPending}, screen {screen})");
            ConfigureQuickPlaytestCapture("quick-event");
            currentEvent=EventContent.Find("shardfall");EventSystem.BeginEvent(run,currentEvent);screen=ScreenMode.Event;
            BeginEventChoice(EventContent.FindChoice("shardfall","careful"));
            CombatCheck(screen==ScreenMode.EventSelection&&EventShardPresentation,"Event choice transitions into dedicated shard selection");
            ChooseEventOfferWithAcquisition(run.pendingEventOfferIds[0]);
            CombatCheck(screen==ScreenMode.EventResult&&acquisitionActive&&acquisitionKind==AcquisitionKind.Shard,"Resolved event opens the existing shard acquisition");
            yield return new WaitForSecondsRealtime(1.5f);
            CombatCheck(!acquisitionActive&&!ShowsNormalShardShrine,"Normal event result no longer has floating shard slots");
            PrepareQuickFormation(2);yield return WaitForCombatQueue();
            Debug.Log("[Gilded Fate Quick Fix] Focused presentation checks complete.");
        }
    }
}
