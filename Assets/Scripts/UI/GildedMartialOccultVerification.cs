using System.Collections;
using System.Linq;
using GildedFate.Core;
using GildedFate.Combat;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureExpansionCapture(string mode)
        {
            if(!mode.StartsWith("expansion-"))return false;
            if(mode is "expansion-vanguard" or "expansion-hexer"){screen=ScreenMode.Collection;return true;}
            combatTestInput=true;PrepareCombatCheck(mode=="expansion-input"?"sigil_of_malice":"break_the_line",5);
            if(mode=="expansion-sigils")
            {
                PrepareCombatCheck("hexed_reverberation",5);combat.PlayFree(GameContent.Upgrade(GameContent.Find("beyond_the_veil_hexer")));
                combat.sigils.AddRange(new[]{SigilKind.Ember,SigilKind.Hex,SigilKind.Echo,SigilKind.Ember,SigilKind.Hex,SigilKind.Echo});combat.enemy.marked=2;
                combat.PlayFree(GameContent.Upgrade(GameContent.Find("hexed_reverberation")));
            }
            else if(mode!="expansion-input")
            {
                combat.PlayFree(GameContent.Find("gilded_warlord"));combat.PlayFree(GameContent.Find("unmovable"));combat.PlayFree(GameContent.Find("relentless_conquest"));
                if(mode=="expansion-dormant")combat.PlayFree(GameContent.Find("battle_cry"));
            }
            RestoreCombatPresentation();return true;
        }
        private IEnumerator RunExpansionInteractionChecks()
        {
            yield return WaitForCombatQueue();combat.enemy.weak=1;combat.TakeEvents();
            var start=CardPickPoint(2);var destination=EnemyDropZone.center;
            HandleCombatPointer(start,true,true,false);HandleCombatPointer(destination,false,true,false);HandleCombatPointer(destination,false,false,true);
            yield return new WaitForSecondsRealtime(1.2f);
            CombatCheck(combat.AwaitingChoice&&combat.ChoiceKind==CardChoiceKind.SigilMode,"Targeted Malice drag-release opens Sigil choice");
            HandleCombatPointer(new Vector2((CombatWidth-816)*.5f+130,CombatHeight*.26f+60),false,false,false);yield return new WaitForSecondsRealtime(.08f);
            CombatCheck(combatEffectTooltipTitle=="EMBER SIGIL","Hovering the Ember option shows its matching tooltip");
            HandleCombatPointer(new Vector2(CombatWidth*.5f,CombatHeight-80),false,false,false);yield return new WaitForSecondsRealtime(.08f);
            CombatCheck(string.IsNullOrEmpty(combatEffectTooltipTitle),"Sigil tooltip clears when the pointer leaves the option");
            choiceOptionSelected="HEX SIGIL";yield return new WaitForSecondsRealtime(.8f);yield return WaitForCombatQueue();
            CombatCheck(!combat.AwaitingChoice&&combat.sigils.Count==1&&combat.memory.sigilActivations==1,"Choice resolves and activates the chosen new Sigil");
            PrepareCombatCheck("beyond_the_veil_hexer",5);yield return WaitForCombatQueue();
            start=CardPickPoint(2);destination=SkillDropZone.center;
            HandleCombatPointer(start,true,true,false);HandleCombatPointer(destination,false,true,false);HandleCombatPointer(destination,false,false,true);
            yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.8f);
            CombatCheck(combat.SigilCapacity==5&&visibleSigilSlots>4.95f,"Power drag-release expands existing HUD to five slots");
            CombatCheck(PlayerEffectChips().Any(c=>c.title=="BEYOND THE VEIL"),"Unique Power appears in character HUD");
            var icons=LoadAuthoredArt("Art/Powers/MartialOccult");CombatCheck(icons&&icons.isReadable&&icons.GetPixel(0,0).a<.01f,"Power icon sheet imported with real transparency");
            PrepareCombatCheck("strike",5);yield return WaitForCombatQueue();combat.PlayFree(GameContent.Find("gilded_warlord"));combat.TakeEvents();ResetPlayerStatusPlayback();
            combat.PlayFree(GameContent.Find("battle_cry"));ConsumeCombatEvents(combat.TakeEvents());
            CombatCheck(PresentedPlayerStatuses().strength==0,"Buff counter waits while Warlord pulses first");
            yield return new WaitForSecondsRealtime(.24f);CombatCheck(PresentedPlayerStatuses().strength==4,"Doubled Strength appears after the Power pulse lead-in");
            Debug.Log($"[Gilded Fate Expansion] {combatInteractionChecks} interaction checks · {combatInteractionFailures} failures");
        }
    }
}
