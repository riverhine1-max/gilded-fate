namespace GildedFate.Core
{
    // A card has exactly one archive family. Rarity is not an origin filter.
    public static class CardArchive
    {
        public static readonly string[] Tabs={"ALL","VANGUARD","HEXER","REAPER","WANDERER","STATUS","CURSES"};
        public static int Category(CardDef card)
        {
            if(card.kind==CardKind.Curse||card.origin==CardOrigin.Curse||card.rarity==Rarity.Curse)return 6;
            if(card.kind==CardKind.Status||card.origin==CardOrigin.Status||card.rarity==Rarity.Status)return 5;
            return card.origin switch {CardOrigin.Knight=>1,CardOrigin.Arcane=>2,CardOrigin.Reaper=>3,CardOrigin.Wanderer=>4,_=>0};
        }
        public static bool Matches(CardDef card,int tab)=>tab==0||Category(card)==tab;
    }
}
