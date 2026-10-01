namespace GildedFate.Core
{
    // The achievement list. Ids double as Steam API names (set the same names in
    // Steamworks > Stats & Achievements). Icon tells the presentation which existing
    // art to frame: "boss:<id>", "hero:<n>", "emblem", "debt", "daily", "gild", "unlock".
    public sealed class AchievementDef
    {
        public readonly string id,name,text,icon;public readonly bool hidden;
        public AchievementDef(string id,string name,string text,string icon,bool hidden=false){this.id=id;this.name=name;this.text=text;this.icon=icon;this.hidden=hidden;}
    }
    public static class AchievementCatalog
    {
        public static readonly AchievementDef[] All=
        {
            new("ACH_FIRST_ASCENT","FIRST ASCENT","Win a run.","emblem"),
            new("ACH_WIN_VANGUARD","STEEL OATH","Win a run as the Vanguard.","hero:0"),
            new("ACH_WIN_HEXER","FORBIDDEN NAME","Win a run as the Hexer.","hero:1"),
            new("ACH_WIN_REAPER","KEEPER OF NAMES","Win a run as the Reaper.","hero:2"),
            new("ACH_BOSS_HOLLOW_KING","CROWN BREAKER","Defeat the Hollow King.","boss:hollow_king"),
            new("ACH_BOSS_VAULT_MOTHER","ROOT AND RUIN","Defeat the Vault Mother.","boss:vault_mother"),
            new("ACH_BOSS_LAST_DEALER","THE HOUSE LOSES","Defeat the Last Dealer.","boss:last_dealer"),
            new("ACH_DEBT_1","IN THE RED","Win a run at Fate Debt I or higher.","debt"),
            new("ACH_DEBT_5","DEEP IN DEBT","Win a run at Fate Debt V or higher.","debt"),
            new("ACH_DEBT_10","DEBT REPAID","Win a run at Fate Debt X.","debt"),
            new("ACH_HIT_50","HEAVY HAND","Deal 50 damage with a single hit.","emblem"),
            new("ACH_HIT_100","CENTURY","Deal 100 damage with a single hit.","emblem"),
            new("ACH_HIT_250","OBLITERATE","Deal 250 damage with a single hit.","emblem"),
            new("ACH_BLOCK_50","IRON WALL","Gain 50 Block in a single combat.","hero:0"),
            new("ACH_GILD_1","GILDED TOUCH","Gild a card.","gild"),
            new("ACH_GILD_25","GOLDEN HABIT","Gild 25 cards across all runs.","gild"),
            new("ACH_ELITES_10","ELITE HUNTER","Defeat 10 elites.","boss:executioner"),
            new("ACH_ENEMIES_100","VAULT SWEEPER","Defeat 100 enemies.","boss:gilded_sentry"),
            new("ACH_CARDS_1000","PLAYMAKER","Play 1,000 cards.","emblem"),
            new("ACH_RELICS_15","COLLECTOR'S EYE","Hold 15 relics in a single run.","boss:collector"),
            new("ACH_SHARDS_3","SHARD BEARER","Carry 3 Fate Shards at once.","emblem"),
            new("ACH_LEAN_DECK","LEAN AND GILDED","Win a run with 15 or fewer cards.","emblem"),
            new("ACH_THICK_DECK","THE FULL ARCHIVE","Win a run with 40 or more cards.","emblem"),
            new("ACH_SPEED","SWIFT FATE","Win a run in under 40 minutes.","emblem"),
            new("ACH_FATEWOVEN","FATEWOVEN","Win a run after three Fateweave pulls.","emblem"),
            new("ACH_DAILY_1","DAILY DEVOTION","Complete a Daily Run.","daily"),
            new("ACH_DAILY_7","SEVEN DAWNS","Complete 7 Daily Runs.","daily"),
            new("ACH_UNLOCK_FIRST","NEW STRANDS","Unlock your first new cards.","unlock"),
            new("ACH_UNLOCK_ALL","THE ARCHIVIST","Unlock every card.","unlock"),
            new("ACH_RUNS_25","PERSISTENT","Play 25 runs.","emblem"),
        };
        public static AchievementDef Find(string id){foreach(var a in All)if(a.id==id)return a;return null;}
    }
}
