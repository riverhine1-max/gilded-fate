namespace GildedFate.Core
{
    // Fate Debt: ten stacking challenge modifiers. Level N enables modifiers 1..N.
    // Daily Runs pick two of the same modifiers by date.
    public static class FateDebt
    {
        public const int Count=10;
        public static readonly string[] Names=
        {
            "TARNISHED BLADES","LEAN PURSES","HEAVIER CROWNS","CURSED INHERITANCE","COLD HEARTH",
            "COSTLY GILDING","HARDENED VAULT","GREEDY ELITES","FRAYED LIFE","THE DEBT COMES DUE"
        };
        public static readonly string[] Texts=
        {
            "Elites start each fight with extra Strength (+20% of their base damage).",
            "Gold from combat is reduced by 25%.",
            "Bosses have 15% more health.",
            "Begin each run with a random Curse in your deck.",
            "Resting at a Sanctuary heals 20% instead of 30%.",
            "Gilding a card costs 10 more Gold.",
            "Normal enemies have 10% more health and +1 Strength.",
            "Elites have 15% more health.",
            "Begin each run with 10% less maximum health.",
            "The Act III boss has 20% more health and +3 Strength."
        };
        public static int MaskForLevel(int level){var mask=0;for(var i=0;i<System.Math.Min(Count,level);i++)mask|=1<<i;return mask;}
        public static string Roman(int n)=>n switch{1=>"I",2=>"II",3=>"III",4=>"IV",5=>"V",6=>"VI",7=>"VII",8=>"VIII",9=>"IX",10=>"X",_=>"0"};
    }
}
