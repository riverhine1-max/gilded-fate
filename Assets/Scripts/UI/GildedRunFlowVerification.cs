using System.Collections;
using System.Linq;
using GildedFate.Core;
using GildedFate.Map;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private int runFlowChecks,runFlowFailures;

        private bool ConfigureRunFlowCapture(string mode)
        {
            if(mode!="map-input")return false;
            combatTestInput=true;
            PrepareMapFlowNode(NodeKind.Combat,0);
            return true;
        }

        private void RunFlowCheck(bool passed,string message)
        {
            runFlowChecks++;
            if(passed)Debug.Log("[Gilded Fate Run Flow] PASS · "+message);
            else{runFlowFailures++;Debug.LogError("[Gilded Fate Run Flow] FAIL · "+message);}
        }

        private MapNode PrepareMapFlowNode(NodeKind kind,int floor)
        {
            run.NewRun(HeroId.Vanguard,20260903+floor+(int)kind*31);
            run.floor=floor;run.stage=RunStage.Map;run.activeNodeFloor=run.activeNodeLane=-1;
            foreach(var candidate in run.nodes){candidate.available=false;candidate.complete=candidate.floor<floor;}
            var node=run.nodes.Where(n=>n.floor==floor).OrderBy(n=>Mathf.Abs(n.lane-RunModel.LaneCount*.5f)).First();node.kind=kind;node.available=true;
            currentNode=null;currentEnemy=null;currentEvent=null;combat=null;pendingMapNode=null;screen=ScreenMode.Map;mapFocusFloor=-1;mapScroll=0;
            FocusMapToCurrentFloor(CombatWidth,CombatHeight);
            return node;
        }

        private Vector2 MapFlowNodePoint(MapNode node)
        {
            var viewport=MapViewport(CombatWidth,CombatHeight);
            return viewport.position+MapPosition(node,viewport.width)-new Vector2(0,mapScroll);
        }

        private IEnumerator WaitForMapRoom()
        {
            var timeout=Time.realtimeSinceStartup+.65f;
            while(screen==ScreenMode.Map&&Time.realtimeSinceStartup<timeout)yield return null;
        }

        private IEnumerator RunMapFlowChecks()
        {
            profile.fastMode=true;profile.reduceMotion=true;profile.cardAnimationSpeed=1;
            var cases=new[]
            {
                (NodeKind.Event,4,ScreenMode.Event,RunStage.Event),(NodeKind.Merchant,4,ScreenMode.Merchant,RunStage.Merchant),
                (NodeKind.Sanctuary,11,ScreenMode.Sanctuary,RunStage.Sanctuary),(NodeKind.Treasure,7,ScreenMode.Treasure,RunStage.Treasure),
                (NodeKind.Elite,5,ScreenMode.Combat,RunStage.Combat),(NodeKind.Boss,RunModel.FloorCount-1,ScreenMode.Combat,RunStage.Combat)
            };
            foreach(var test in cases)
            {
                var node=PrepareMapFlowNode(test.Item1,test.Item2);var point=MapFlowNodePoint(node);
                RunFlowCheck(new Rect(0,0,CombatWidth,CombatHeight).Contains(point),test.Item1+" node is visible on its focused floor");
                RunFlowCheck(TryActivateMapNodeAt(point),test.Item1+" node accepts the live map hit route");
                RunFlowCheck(pendingMapNode==node&&screen==ScreenMode.Map,test.Item1+" click starts the room-entry transition before changing screens");
                yield return WaitForMapRoom();
                RunFlowCheck(screen==test.Item3&&run.stage==test.Item4,test.Item1+" opens the correct room screen and run stage");
                if(test.Item3==ScreenMode.Combat)RunFlowCheck(combat!=null&&combat.hand.Count==5&&combat.energy==3&&currentEnemy!=null,test.Item1+" initializes a playable battle");
                yield return null;
            }

            var combatNode=PrepareMapFlowNode(NodeKind.Combat,0);var combatPoint=MapFlowNodePoint(combatNode);
            RunFlowCheck(!TryActivateMapNodeAt(new Vector2(18,150))&&screen==ScreenMode.Map,"Clicking outside the map does not enter a room");
            RunFlowCheck(TryActivateMapNodeAt(combatPoint)&&pendingMapNode==combatNode,"Combat node begins travel from the real map route");
            yield return WaitForMapRoom();
            RunFlowCheck(screen==ScreenMode.Combat,"Combat node enters combat after its travel animation");
            RunFlowCheck(combat!=null&&combat.player.hp==run.hp&&combat.hand.Count==5&&combat.phase==GildedFate.Combat.CombatPhase.Player,"Map combat creates the real player state, opening hand, and player turn");
            yield return new WaitForSecondsRealtime(1.1f);

            var index=combat.hand.FindIndex(c=>combat.CanPlay(c));
            RunFlowCheck(index>=0&&handViews.ContainsKey(combat.hand[index].instanceId),"A map-started combat exposes playable hand cards");
            if(index>=0)
            {
                var card=combat.hand[index];var energy=combat.energy;var cost=combat.CostFor(card);var start=CardPickPoint(index);
                var target=combat.RequiresEnemyTarget(card)?EnemyDropZone.center:new Vector2(470,210);
                HandleCombatPointer(start,true,true,false);HandleCombatPointer(target,false,true,false);HandleCombatPointer(target,false,false,true);
                yield return WaitForCombatQueue();
                RunFlowCheck(combat.cardsPlayed==1&&combat.energy==energy-cost&&!combat.hand.Contains(card),"A card can be dragged, targeted, paid, and resolved after entering from the map");
                RunFlowCheck(combat.discard.Contains(card)||combat.exhaust.Contains(card),"The map-started card reaches its real destination pile");
            }
            Debug.Log($"[Gilded Fate Run Flow] {runFlowChecks} checks · {runFlowFailures} failures · map rooms and map-started combat");
        }
    }
}
