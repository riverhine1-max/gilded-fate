using System.Collections;
using System.Linq;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureMenuPolishCapture(string mode)
        {
            if(mode is not ("polish-menu-input" or "polish-settings-accessibility" or "polish-settings-audio" or "polish-shard-choice" or "polish-collection-focus" or "polish-pause"))return false;
            PrepareCombatCheck("ward",5);combatTestInput=true;AudioListener.pause=true;
            controllerNavigation=true;menuUsesGamepad=true;combatPauseOpen=false;mapPauseOpen=false;
            if(mode.StartsWith("polish-settings-")){screen=ScreenMode.Settings;settingsReturnScreen=ScreenMode.Combat;settingsPage=mode.EndsWith("audio")?1:3;settingsFocusIndex=mode.EndsWith("audio")?1:8;}
            else if(mode=="polish-shard-choice"){screen=ScreenMode.Map;run.shards.Clear();run.AddShard("bloodstone");run.AddShard("emberglass");run.OfferShard("silvermind",45);shardDiscoveryIndex=0;}
            else if(mode=="polish-collection-focus"){OpenRunDeck();for(var i=0;i<20;i++)run.AddCard("ward");screenControllerIndex=20;}
            else if(mode=="polish-pause"){screen=ScreenMode.Combat;combatPauseOpen=true;pauseMenuIndex=2;}
            return true;
        }
        private void DispatchMenuCheck(MenuNavigation input)
        {
            menuInputConsumed=false;
            if(!RouteMenuNavigation(input)&&screen!=ScreenMode.Combat)HandleLegacyMenuNavigation(input);
        }
        private IEnumerator RunMenuPolishChecks()
        {
            PrepareCombatCheck("ward",5);combatTestInput=true;AudioListener.pause=true;
            yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.2f);
            controllerNavigation=true;menuUsesGamepad=true;
            var combatBefore=JsonUtility.ToJson(combat.CaptureCheckpoint());
            combatPauseOpen=true;DispatchMenuCheck(new MenuNavigation{back=true});
            CombatCheck(!combatPauseOpen&&menuInputConsumed,"Closing pause consumes the input instead of reopening or playing a card");
            combatPauseOpen=true;DispatchMenuCheck(new MenuNavigation{y=1});
            CombatCheck(pauseMenuIndex==1,"Pause navigates to settings");
            DispatchMenuCheck(new MenuNavigation{accept=true});
            CombatCheck(screen==ScreenMode.Settings&&settingsReturnScreen==ScreenMode.Combat&&combatPauseOpen,"Settings preserve the paused combat destination");
            DispatchMenuCheck(new MenuNavigation{back=true});
            CombatCheck(screen==ScreenMode.Combat&&combatPauseOpen&&menuInputConsumed,"Settings returns to pause without resuming combat");
            DispatchMenuCheck(new MenuNavigation{y=-1});
            CombatCheck(pauseMenuIndex==3,"Every pause action including quit can be focused without activating it");
            DispatchMenuCheck(new MenuNavigation{back=true});
            CombatCheck(!combatPauseOpen&&JsonUtility.ToJson(combat.CaptureCheckpoint())==combatBefore,"Navigating pause never changes combat rules");

            foreach(var origin in new[]{ScreenMode.Combat,ScreenMode.Map,ScreenMode.Reward,ScreenMode.Merchant,ScreenMode.Event,ScreenMode.Fateweave})
            {
                screen=origin;OpenRunDeck();var saved=JsonUtility.ToJson(run);
                CombatCheck(screen==ScreenMode.Collection&&viewingRunDeck&&screenControllerIndex==0,"Deck opens from "+origin);
                DispatchMenuCheck(new MenuNavigation{category=true,sort=true});
                CombatCheck(!collectionRelics&&collectionSort==0,"Run deck retains acquisition order and never switches to archive relics");
                var cards=CollectionCardEntries(true);CombatCheck(cards.Select(c=>c.id).SequenceEqual(run.cards.Select(c=>c.BuildDefinition().id)),"Visible deck and controller use the same physical order");
                DispatchMenuCheck(new MenuNavigation{y=1});CombatCheck(screenControllerIndex==Mathf.Min(6,cards.Length-1),"Grid moves one six-card row");
                DispatchMenuCheck(new MenuNavigation{inspect=true});CombatCheck(inspectedCard!=null&&JsonUtility.ToJson(inspectedCard)==JsonUtility.ToJson(cards[screenControllerIndex]),"Focused physical card opens inspection");
                PrepareCardInspection(inspectedCard);var wasUpgraded=inspectionShowUpgrade;
                DispatchMenuCheck(new MenuNavigation{x=1});CombatCheck(inspectionShowUpgrade!=wasUpgraded,"Inspection switches base and upgrade without altering deck");
                DispatchMenuCheck(new MenuNavigation{back=true});CombatCheck(inspectedCard==null&&screen==ScreenMode.Collection,"First back closes inspection only");
                yield return new WaitForSecondsRealtime(.04f);
                CombatCheck(IsRunInspectionPaused&&JsonUtility.ToJson(run)==saved&&JsonUtility.ToJson(combat.CaptureCheckpoint())==combatBefore,"Deck and upgrade preview preserve game and timer: "+origin);
                DispatchMenuCheck(new MenuNavigation{back=true});CombatCheck(screen==origin&&!viewingRunDeck,"Second back returns to exact origin: "+origin);
            }
            screen=ScreenMode.Collection;collectionReturnScreen=ScreenMode.Menu;viewingRunDeck=false;collectionRelics=false;collectionFilter=collectionSort=screenControllerIndex=0;
            for(var filter=0;filter<CardArchive.Tabs.Length;filter++)
            {
                var entries=CollectionCardEntries(false);CombatCheck(entries.Length>0,"Archive filter has entries: "+filter);
                for(var sort=0;sort<3;sort++)
                {
                    DispatchMenuCheck(new MenuNavigation{x=1});DispatchMenuCheck(new MenuNavigation{accept=true});
                    CombatCheck(inspectedCard==CollectionCardEntries(false)[screenControllerIndex],"Archive inspection matches current filter and sort");
                    DispatchMenuCheck(new MenuNavigation{back=true});DispatchMenuCheck(new MenuNavigation{sort=true});
                }
                DispatchMenuCheck(new MenuNavigation{page=1});
            }
            CombatCheck(collectionFilter==0&&collectionSort==0,"Archive filter and sorting cycle cleanly");
            DispatchMenuCheck(new MenuNavigation{category=true});DispatchMenuCheck(new MenuNavigation{y=1});
            CombatCheck(collectionRelics&&screenControllerIndex==6,"Relic archive uses the same six-column navigation");
            DispatchMenuCheck(new MenuNavigation{accept=true});CombatCheck(inspectedRelic==GameContent.Relics[CollectionRelicIndices[6]],"Relic inspection matches focused artwork");
            DispatchMenuCheck(new MenuNavigation{back=true});CombatCheck(inspectedRelic==null&&screen==ScreenMode.Collection,"Relic back does not leave archive");
            CombatCheck(CollectionRelicIndices.Length==GameContent.Relics.Length&&CollectionRelicIndices.Distinct().Count()==GameContent.Relics.Length,"Relic gallery contains every catalog entry exactly once");
            for(var position=0;position<CollectionRelicIndices.Length;position++)
            {
                var catalogIndex=CollectionRelicIndices[position];var expected=GameContent.Relics[catalogIndex];
                if(position>0)
                {
                    var previousIndex=CollectionRelicIndices[position-1];var previous=GameContent.Relics[previousIndex];
                    CombatCheck((int)previous.rarity<=(int)expected.rarity,"Global relic rarity order: "+expected.name);
                    if(previous.rarity==expected.rarity)CombatCheck(previousIndex<catalogIndex,"Stable within-rarity artwork order: "+expected.name);
                }
                screenControllerIndex=position;DispatchMenuCheck(new MenuNavigation{accept=true});
                CombatCheck(inspectedRelic==expected,"Sorted relic selection retains catalog identity: "+expected.name);
                DispatchMenuCheck(new MenuNavigation{back=true});
            }
            DispatchMenuCheck(new MenuNavigation{back=true});CombatCheck(screen==ScreenMode.Menu,"Archive returns to menu");

            screen=ScreenMode.Settings;settingsReturnScreen=ScreenMode.Menu;settingsPage=0;settingsFocusIndex=0;
            var profileBefore=JsonUtility.ToJson(profile);var runBefore=JsonUtility.ToJson(run);
            for(var page=0;page<4;page++)
            {
                settingsPage=page;var rows=CurrentSettingsRows();
                for(var i=0;i<rows.Length;i++)
                {
                    var row=rows[i];CombatCheck(!string.IsNullOrEmpty(row.description)&&!string.IsNullOrEmpty(row.Display),"Settings explains its current value: "+row.name);
                    foreach(var width in new[]{1440f,2304f})
                    {
                        var rect=SettingsRowRect(width,i,page==3);
                        CombatCheck(rect.x>100&&rect.xMax<width-100&&rect.y>=246&&rect.yMax<630,"Setting fits below tabs and above help: "+row.name);
                        for(var j=0;j<i;j++)CombatCheck(!rect.Overlaps(SettingsRowRect(width,j,page==3)),"Settings rows never overlap");
                    }
                    var value=row.get();
                    // Never change the QA desktop resolution. All other controls use the production handler.
                    if(page==0&&i==0){row.set(value>.5f?0:1);CombatCheck(row.get()!=value,"Fullscreen field is wired");row.set(value);continue;}
                    ChangeSetting(row,1);CombatCheck(row.get()!=value||row.slider&&value==row.maximum,"Setting changes its actual profile field: "+row.name);
                    if(row.slider){for(var n=0;n<45;n++)ChangeSetting(row,-1);CombatCheck(Mathf.Approximately(row.get(),row.minimum),"Slider clamps to minimum: "+row.name);for(var n=0;n<45;n++)ChangeSetting(row,1);CombatCheck(Mathf.Approximately(row.get(),row.maximum),"Slider clamps to maximum: "+row.name);}
                    row.set(value);
                }
                DispatchMenuCheck(new MenuNavigation{page=1});CombatCheck(settingsPage==(page+1)%4&&settingsFocusIndex==0,"Settings shoulder navigation resets focus");
            }
            CombatCheck(JsonUtility.ToJson(profile)==profileBefore&&JsonUtility.ToJson(run)==runBefore,"Settings modifies no gameplay fields");
            ApplySettings();settingsPage=3;settingsFocusIndex=0;
            for(var i=0;i<CurrentSettingsRows().Length;i++)DispatchMenuCheck(new MenuNavigation{y=1});
            CombatCheck(settingsFocusIndex==CurrentSettingsRows().Length,"Save and return is reachable after all accessibility options");
            DispatchMenuCheck(new MenuNavigation{accept=true});CombatCheck(screen==ScreenMode.Settings&&settingsOverview,"Settings subsection returns to categories with confirm");
            DispatchMenuCheck(new MenuNavigation{back=true});CombatCheck(screen==ScreenMode.Menu,"Settings categories return to the original menu");

            screen=ScreenMode.Map;mapPauseOpen=true;menuNavigationContext=null;DispatchMenuCheck(new MenuNavigation{y=1,accept=true});
            CombatCheck(screen==ScreenMode.Settings&&settingsReturnScreen==ScreenMode.Map&&mapPauseOpen,"Map pause uses the shared settings path");
            DispatchMenuCheck(new MenuNavigation{back=true});DispatchMenuCheck(new MenuNavigation{back=true});
            CombatCheck(screen==ScreenMode.Map&&!mapPauseOpen,"Map settings returns through pause safely");
            DispatchMenuCheck(new MenuNavigation{hud=true,x=1});CombatCheck(hudNavigationIndex==1&&IsRunInspectionPaused,"HUD focus pauses the run timer");
            DispatchMenuCheck(new MenuNavigation{accept=true});CombatCheck(screen==ScreenMode.Collection&&viewingRunDeck,"HUD focus can open Deck");
            DispatchMenuCheck(new MenuNavigation{back=true});DispatchMenuCheck(new MenuNavigation{hud=true});
            DispatchMenuCheck(new MenuNavigation{accept=true});CombatCheck(routeInspectionOpen,"HUD focus can open read-only map");
            var routeState=JsonUtility.ToJson(run);var scroll=routeSavedScroll;
            HandleRouteInspectionNavigation(new MenuNavigation{y=1});CombatCheck(mapScroll>0,"Route inspection scrolls without a mouse");
            HandleRouteInspectionNavigation(new MenuNavigation{back=true});
            CombatCheck(!routeInspectionOpen&&mapScroll==scroll&&JsonUtility.ToJson(run)==routeState,"Route return restores scroll and exact game state");
            DispatchMenuCheck(new MenuNavigation{hud=true,x=-1});DispatchMenuCheck(new MenuNavigation{accept=true});
            CombatCheck(mapPauseOpen,"HUD focus can open Settings / Pause");DispatchMenuCheck(new MenuNavigation{back=true});

            foreach(var width in new[]{1440f,2304f})for(var slot=0;slot<2;slot++)
            {
                var choice=new Rect(width*.5f-330+slot*340,654,320,64);var tooltip=ShardReplacementTooltipRect(choice,width,810,220);
                CombatCheck(!tooltip.Overlaps(choice)&&!tooltip.Overlaps(new Rect(width*.5f-330,439,660,163))&&tooltip.x>=16&&tooltip.xMax<=width-16,"Shard replacement help stays beside the choice, clear of both effects");
            }
            run.shards.Clear();run.DeclineDiscoveredShard();run.gold=100;run.OfferShard("bloodstone",45);var gold=run.gold;
            DispatchMenuCheck(new MenuNavigation{accept=true});
            CombatCheck(run.shards.Count==1&&run.shards[0].id=="bloodstone"&&run.gold==gold-45&&acquisitionActive,"Shard confirm charges once and starts collection flight");
            DispatchMenuCheck(new MenuNavigation{accept=true});CombatCheck(menuInputConsumed&&run.gold==gold-45&&run.shards.Count==1,"Acquisition consumes duplicate confirms");
            yield return new WaitForSecondsRealtime(1.5f);
            CombatCheck(!acquisitionActive,"Shard acquisition releases input when finished");
            run.AddShard("emberglass");run.OfferShard("silvermind");gold=run.gold;
            DispatchMenuCheck(new MenuNavigation{x=1,accept=true});
            CombatCheck(run.shards.Count==2&&run.shards.Any(s=>s.id=="bloodstone")&&run.shards.Any(s=>s.id=="silvermind")&&!run.shards.Any(s=>s.id=="emberglass"),"Replacement changes only the selected shard");
            yield return new WaitForSecondsRealtime(1.5f);
            run.OfferShard("emberglass",45);var shardIds=string.Join(",",run.shards.Select(s=>s.id));
            DispatchMenuCheck(new MenuNavigation{back=true});
            CombatCheck(!ShardDiscoveryOpen&&run.gold==gold&&string.Join(",",run.shards.Select(s=>s.id))==shardIds,"Declining a paid shard leaves gold and inventory untouched");
            run.OfferShard("emberglass",45);run.gold=0;DispatchMenuCheck(new MenuNavigation{accept=true});
            CombatCheck(ShardDiscoveryOpen&&!acquisitionActive&&string.Join(",",run.shards.Select(s=>s.id))==shardIds,"Unaffordable acceptance cannot remove an owned shard");
            DispatchMenuCheck(new MenuNavigation{back=true});

            run.shards.Add(new FateShardState{id="emberglass",slot=2});run.shards[0].active=true;
            DispatchMenuCheck(new MenuNavigation{back=true});CombatCheck(run.shards.Count==3,"Legacy capacity prompt cannot silently discard a shard on Back");
            DispatchMenuCheck(new MenuNavigation{accept=true});CombatCheck(run.shards.Count==3,"An active legacy shard cannot be released");
            DispatchMenuCheck(new MenuNavigation{x=1,accept=true});CombatCheck(run.shards.Count==2&&run.shards[0].active,"Legacy capacity removes only the explicitly chosen inactive shard");

            PrepareCombatCheck("ward",5);combatTestInput=true;yield return WaitForCombatQueue();
            screen=ScreenMode.Reward;run.stage=RunStage.CardReward;run.pendingCombatGold=18;run.combatGoldClaimed=false;
            run.RollEncounterRewards(NodeKind.Combat);run.encounterRewards.relicClaimed=run.encounterRewards.shardClaimed=true;
            gold=run.gold;var deckCount=run.cards.Count;rewardRevealTime=0;
            DispatchMenuCheck(new MenuNavigation{accept=true});
            CombatCheck(run.gold==gold+18&&run.combatGoldClaimed&&run.cards.Count==deckCount&&goldCollectTime>0,"Claiming gold does not also claim a card");
            ClaimCombatGold(Vector2.zero);CombatCheck(run.gold==gold+18,"Gold receipt prevents repeated claims");
            CombatCheck(RunGoldIconRect.center==new Vector2(210,28),"Reward gold lands on the actual shared gold icon");
            rewardRevealTime=0;DispatchMenuCheck(new MenuNavigation{x=1,inspect=true});
            CombatCheck(inspectedCard?.id==RewardCards()[screenControllerIndex].id&&inspectionShowUpgrade,"Reward inspection opens the selected upgrade comparison");
            DispatchMenuCheck(new MenuNavigation{back=true});DispatchMenuCheck(new MenuNavigation{accept=true});
            CombatCheck(acquisitionActive&&run.cards.Count==deckCount+1&&run.encounterRewards.cardClaimed,"Reward selection claims one card and starts its flight");
            DispatchMenuCheck(new MenuNavigation{accept=true});CombatCheck(run.cards.Count==deckCount+1&&screen==ScreenMode.Reward,"Held reward confirm cannot skip or claim again");
            yield return new WaitForSecondsRealtime(1.5f);
            CombatCheck(!acquisitionActive&&screen==ScreenMode.Map,"Reward flight returns to ascent once");

            PrepareCombatCheck("ward",5);combatTestInput=true;yield return WaitForCombatQueue();
            screen=ScreenMode.Combat;combatBefore=JsonUtility.ToJson(combat.CaptureCheckpoint());acquisitionActive=true;
            DispatchMenuCheck(new MenuNavigation{accept=true,back=true});
            CombatCheck(menuInputConsumed&&!combatPauseOpen&&JsonUtility.ToJson(combat.CaptureCheckpoint())==combatBefore,"Acquisition owns cancel and confirm instead of combat");acquisitionActive=false;
            combatPauseOpen=true;menuNavigationContext=null;DispatchMenuCheck(new MenuNavigation{y=1});DispatchMenuCheck(new MenuNavigation{y=1});
            verificationSavesAllowed=true;DispatchMenuCheck(new MenuNavigation{accept=true});verificationSavesAllowed=false;
            CombatCheck(screen==ScreenMode.Menu&&run.hasCombatCheckpoint&&run.combatCheckpoint!=null,"Save and return uses the real exact combat checkpoint");
            screen=ScreenMode.Settings;settingsReturnScreen=ScreenMode.Menu;settingsPage=3;settingsFocusIndex=8;controllerNavigation=true;menuUsesGamepad=true;
            Debug.Log($"[Gilded Fate Menu Navigation] {combatInteractionChecks} checks · {combatInteractionFailures} failures · audio deliberately muted");
        }
    }
}
