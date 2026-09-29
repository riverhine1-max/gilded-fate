using System;
using GildedFate.Core;

namespace GildedFate.Combat
{
    // GILDING: once per player turn, spend run Gold to make the next playable card
    // resolve one extra time. The extra play is a finite copy on the pending play,
    // exactly like Echo and Golden Echo, so replays never re-enter Play() and can
    // never schedule another gild. Gold itself lives on RunModel: the UI deducts it
    // after ArmGild() succeeds and saves the run and this checkpoint together.
    public sealed partial class CombatState
    {
        public const int GildBaseCost=15,GildCostStep=10;
        public const string GildReceiptLabel="GILDED";
        // Plain value fields: copied by CopyForCheckpoint's MemberwiseClone and written
        // by JsonUtility. Saves from before Gilding load as unarmed with 0 gilds.
        public bool gildArmed,gildUsedThisTurn;
        public int gildsThisCombat;

        public int GildCost=>GildBaseCost+GildCostStep*Math.Max(0,gildsThisCombat);
        public bool GildReady=>phase==CombatPhase.Player&&pendingPlay==null&&!IsOver&&!gildArmed&&!gildUsedThisTurn;
        public bool CanGild(int gold)=>GildReady&&gold>=GildCost;
        // Curses, Statuses and unplayable cards never spend the gild; it waits for the next real play.
        public static bool GildEligible(CardDef card)=>card!=null&&!card.unplayable&&card.kind is not (CardKind.Curse or CardKind.Status)&&card.origin is not (CardOrigin.Curse or CardOrigin.Status)&&card.rarity is not (Rarity.Curse or Rarity.Status);
        public bool WillGild(CardDef card)=>gildArmed&&GildEligible(card)&&CanPlay(card);
        // Rules-side arming only; the caller has already checked CanGild(gold) and pays the Gold.
        public bool ArmGild(){if(!GildReady)return false;gildArmed=gildUsedThisTurn=true;gildsThisCombat++;return true;}
        // Called once by Play() before the pending play exists. Returns the extra copies to add.
        private int ConsumeGild(CardDef card)
        {
            if(!gildArmed||!GildEligible(card))return 0;
            gildArmed=false;Emit(CombatEventKind.Status,2,true,card,GildReceiptLabel);return 1;
        }
        private void ResetGildTurn()=>gildUsedThisTurn=false;
        private void ResetGildCombat(){gildArmed=gildUsedThisTurn=false;gildsThisCombat=0;}
    }
}
