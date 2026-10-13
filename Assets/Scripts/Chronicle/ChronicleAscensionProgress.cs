using System;
using System.Collections.Generic;

namespace GildedFate.Chronicle
{
    /// <summary>
    /// The Ten Recollections' saved state, nested inside ChronicleProgress (and therefore inside PlayerProfile: no second save file).
    /// Plain public fields and lists only, so Unity's JsonUtility reads it; a profile saved before this existed loads it empty.
    /// Revelations are stored by stable id, never by index or display text.
    /// </summary>
    [Serializable]
    public sealed class ChronicleAscensionProgress
    {
        public List<string> earned = new List<string>();          // revelation ids, in the order they were earned
        public List<string> presented = new List<string>();       // shown to the player after a victory (or noticed in the Archive)
        public List<string> watched = new List<string>();         // read or watched through at least once
        public List<string> pending = new List<string>();         // earned but not yet presented (waits for authored content)
        public List<string> corrections = new List<string>();     // the new ASC_CORR_* corrections, in discovery order
        public List<string> creditedRunIds = new List<string>();  // guards against a repeated victory callback for one run
        public int[] fateDebtXWins = new int[3];                  // genuine Fate Debt X victories credited by this system, per hero
        /// <summary>Chapter X was already open under the previous rule (all 27 memories) when this system arrived. Informational:
        /// access itself is kept because the saved secretUnlocked flag is never cleared.</summary>
        public bool secretGrandfathered;

        public void Ensure()
        {
            earned = Dedupe(earned); presented = Dedupe(presented); watched = Dedupe(watched); pending = Dedupe(pending);
            corrections = Dedupe(corrections); creditedRunIds = creditedRunIds ?? new List<string>();
            if (fateDebtXWins == null || fateDebtXWins.Length < 3) { var a = new int[3]; if (fateDebtXWins != null) Array.Copy(fateDebtXWins, a, fateDebtXWins.Length); fateDebtXWins = a; }
            // Pending only ever holds earned, unpresented revelations.
            pending.RemoveAll(id => !earned.Contains(id) || presented.Contains(id));
        }

        static List<string> Dedupe(List<string> list)
        {
            if (list == null) return new List<string>();
            var seen = new HashSet<string>(); var clean = new List<string>(list.Count);
            foreach (var id in list) if (!string.IsNullOrEmpty(id) && seen.Add(id)) clean.Add(id);
            return clean;
        }
    }

    /// <summary>What one recorded victory did to the hero's recollections.</summary>
    public sealed class ChronicleAscensionResult
    {
        public ChronicleRevelationDef earned;      // null when this victory earned nothing
        public bool duplicate;                      // this run was already credited
        public bool secretUnlocked;                 // this victory completed the Chapter X requirement
        public string reason = "";                  // why nothing was earned (for logs and tests)
        public ChronicleUnlockResult unlocks = new ChronicleUnlockResult();   // anything the re-evaluated original rules opened
        public bool Earned => earned != null;
    }

    public sealed partial class ChronicleProgress
    {
        /// <summary>The Chapter X rule: every hero has completed their True History (their Fate Debt X revelation, which is only
        /// earned by a genuine Fate Debt X victory after Fate Debt I-IX). All 27 memories are implied by those victories and kept
        /// as a guard so the Archive's "every memory is recovered" stays true.</summary>
        public bool SecretRequirementMet => AllMemoriesUnlocked && AllTrueHistoriesComplete;

        // ---------- queries ----------
        public bool IsRevelationEarned(string id) => ascension.earned.Contains(id);
        public bool IsRevelationPresented(string id) => ascension.presented.Contains(id);
        public bool IsRevelationWatched(string id) => ascension.watched.Contains(id);
        public bool HasAscensionCorrection(string id) => ascension.corrections.Contains(id);

        public int RevelationsEarned(ChronicleHero hero)
        {
            Ensure(); var n = 0;
            foreach (var d in ChronicleAscensionCatalog.For(hero)) if (IsRevelationEarned(d.id)) n++;
            return n;
        }

        public int RevelationsWatched(ChronicleHero hero)
        {
            Ensure(); var n = 0;
            foreach (var d in ChronicleAscensionCatalog.For(hero)) if (IsRevelationWatched(d.id)) n++;
            return n;
        }

        /// <summary>The revelation this hero earns next, or null once their True History is complete.</summary>
        public ChronicleRevelationDef NextRevelation(ChronicleHero hero)
        {
            Ensure();
            foreach (var d in ChronicleAscensionCatalog.For(hero)) if (!IsRevelationEarned(d.id)) return d;
            return null;
        }

        /// <summary>The Fate Debt level this hero must defeat to earn their next revelation; 0 once all ten are earned.</summary>
        public int NextRequiredFateDebt(ChronicleHero hero) => NextRevelation(hero)?.tier ?? 0;
        public bool TrueHistoryComplete(ChronicleHero hero) => RevelationsEarned(hero) >= ChronicleAscensionCatalog.TierCount;
        public bool AllTrueHistoriesComplete => TrueHistoryComplete(ChronicleHero.Vanguard) && TrueHistoryComplete(ChronicleHero.Hexer) && TrueHistoryComplete(ChronicleHero.Reaper);
        public bool HasWonFateDebtX(ChronicleHero hero) { Ensure(); var h = (int)hero; return h >= 0 && h < 3 && ascension.fateDebtXWins[h] > 0; }

        /// <summary>Earned revelations for this hero that are still waiting to be presented, in chain order.</summary>
        public List<ChronicleRevelationDef> PendingRevelations(ChronicleHero hero)
        {
            Ensure(); var list = new List<ChronicleRevelationDef>();
            foreach (var d in ChronicleAscensionCatalog.For(hero)) if (ascension.pending.Contains(d.id)) list.Add(d);
            return list;
        }

        // ---------- the one progression hook ----------
        /// <summary>
        /// A run was genuinely won and recorded. fateDebt is the run's Fate Debt level (0 = Normal).
        /// Earns at most ONE revelation: the hero's next one, and only when this victory's level is exactly the level it requires.
        /// Normal victories, repeated levels, skipped levels and repeated callbacks for the same run earn nothing.
        /// Call only for real, non-daily, non-sandbox victories (the game's existing Fate Debt rules decide that).
        /// </summary>
        public ChronicleAscensionResult RegisterAscensionVictory(ChronicleHero hero, int fateDebt, string runId)
        {
            Ensure();
            var result = new ChronicleAscensionResult();
            var h = (int)hero;
            if (h < 0 || h > 2) { result.reason = "unknown hero"; return result; }
            if (fateDebt < 0 || fateDebt > ChronicleAscensionCatalog.TierCount) { result.reason = "Fate Debt " + fateDebt + " is out of range"; return result; }
            if (fateDebt == 0) { result.reason = "a Normal victory is the original Chronicle's foundation"; return result; }
            if (!string.IsNullOrEmpty(runId))
            {
                if (ascension.creditedRunIds.Contains(runId)) { result.duplicate = true; result.reason = "run " + runId + " was already credited"; return result; }
                ascension.creditedRunIds.Add(runId);
                if (ascension.creditedRunIds.Count > 512) ascension.creditedRunIds.RemoveRange(0, 256);
            }
            if (fateDebt == ChronicleAscensionCatalog.TierCount) ascension.fateDebtXWins[h]++;

            var next = NextRevelation(hero);
            if (next == null) result.reason = ChronicleCatalog.HeroName(hero) + "'s True History is already complete";
            else if (next.tier != fateDebt) result.reason = "the next recollection requires Fate Debt " + next.tier;
            else
            {
                ascension.earned.Add(next.id);
                if (!ascension.pending.Contains(next.id)) ascension.pending.Add(next.id);
                result.earned = next;
            }
            result.unlocks = RefreshUnlocks(); result.secretUnlocked = result.unlocks.secretUnlocked;
            return result;
        }

        // ---------- presentation commits ----------
        /// <summary>Records that an earned revelation was shown to the player. Unearned revelations are refused.</summary>
        public bool MarkRevelationPresented(string id)
        {
            Ensure();
            if (ChronicleAscensionCatalog.Find(id) == null || !IsRevelationEarned(id)) return false;
            ascension.pending.Remove(id);
            if (ascension.presented.Contains(id)) return false;
            ascension.presented.Add(id); return true;
        }

        /// <summary>Records that a revelation was read or watched through. Returns true on the first viewing only.
        /// Viewing never earns: an unearned revelation is refused unless allowUnearned (the developer sandbox).</summary>
        public bool CommitRevelationWatched(string id, bool allowUnearned = false)
        {
            Ensure();
            if (ChronicleAscensionCatalog.Find(id) == null) return false;
            if (!allowUnearned && !IsRevelationEarned(id)) return false;
            if (IsRevelationEarned(id)) MarkRevelationPresented(id);
            if (ascension.watched.Contains(id)) return false;
            ascension.watched.Add(id); return true;
        }

        /// <summary>Permanently records one of the new ASC_CORR_* corrections, only once its revelation is earned. Idempotent.</summary>
        public bool CommitAscensionCorrection(string correctionId, bool allowUnearned = false)
        {
            Ensure();
            var owner = ChronicleAscensionCatalog.FindByCorrection(correctionId);
            if (owner == null || (!allowUnearned && !IsRevelationEarned(owner.id)) || ascension.corrections.Contains(correctionId)) return false;
            ascension.corrections.Add(correctionId); return true;
        }

        // ---------- save migration ----------
        /// <summary>Version 1 -> 2: introduces the recollections. Deterministic and non-destructive: nothing is earned, nothing is
        /// removed, and Chapter X access that already existed is recorded (and, as before, never revoked).</summary>
        void MigrateToAscension()
        {
            ascension.secretGrandfathered |= secretUnlocked;
        }
    }
}
