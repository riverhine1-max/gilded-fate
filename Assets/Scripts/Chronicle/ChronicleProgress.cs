using System;
using System.Collections.Generic;

namespace GildedFate.Chronicle
{
    public sealed class ChronicleUnlockResult
    {
        public readonly List<string> newMemories = new List<string>();
        public bool secretUnlocked;
        public bool Any => newMemories.Count > 0 || secretUnlocked;
        public void Merge(ChronicleUnlockResult other) { newMemories.AddRange(other.newMemories); secretUnlocked |= other.secretUnlocked; }
    }

    public sealed class ChronicleCommitResult
    {
        public bool accepted, firstView, secretCompleted;
        public string rejectReason = "";
        public readonly List<string> newFacts = new List<string>();
        public readonly List<string> newCorrections = new List<string>();
    }

    /// <summary>A finished run recorded by the game before the Chronicle existed (used once, to migrate old profiles).</summary>
    public struct ChronicleLegacyRun { public int hero, act, floor; public bool victory; }

    /// <summary>
    /// Everything the Chronicle persists, stored inside the existing PlayerProfile (no second save system).
    /// Plain public fields and lists only, so Unity's JsonUtility reads it; any missing field loads as a fresh Chronicle.
    /// IDs are stable strings (MEM_xx, CORR_xx, FACT_xxx), never display text.
    /// </summary>
    [Serializable]
    public sealed class ChronicleProgress
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;

        public List<string> unlockedMemories = new List<string>();
        public List<string> viewedMemories = new List<string>();
        public List<string> discoveredFacts = new List<string>();
        public List<string> corrections = new List<string>();      // in discovery order
        public List<string> pendingNotices = new List<string>();   // unlocks the player has not been told about yet
        public List<string> countedRunIds = new List<string>();    // guards every run-end milestone against double counting
        public List<string> eventsSeen = new List<string>();

        // Per-hero counters, indexed by ChronicleHero.
        public int[] runsEnded = new int[3], wins = new int[3], bestFloor = new int[3];
        public int[] actBossMask = new int[3];                      // bit0 = Act I boss, bit1 = Act II, bit2 = Act III

        public bool secretUnlocked, secretCompleted, legacyMigrated;
        public int openingViews;
        public int openingMode;                                     // 0 full every launch (default), 1 short after the first viewing, 2 never
        public int lastChapterOpened;
        public bool instantText;                                    // accessibility: passages appear immediately
        public float textSpeed = 1f;                                // 0.5 .. 2

        // ---------- safety ----------
        public void Ensure()
        {
            unlockedMemories = unlockedMemories ?? new List<string>(); viewedMemories = viewedMemories ?? new List<string>();
            discoveredFacts = discoveredFacts ?? new List<string>(); corrections = corrections ?? new List<string>();
            pendingNotices = pendingNotices ?? new List<string>(); countedRunIds = countedRunIds ?? new List<string>(); eventsSeen = eventsSeen ?? new List<string>();
            if (runsEnded == null || runsEnded.Length < 3) runsEnded = Grow(runsEnded);
            if (wins == null || wins.Length < 3) wins = Grow(wins);
            if (bestFloor == null || bestFloor.Length < 3) bestFloor = Grow(bestFloor);
            if (actBossMask == null || actBossMask.Length < 3) actBossMask = Grow(actBossMask);
            if (version <= 0) version = CurrentVersion;
            if (textSpeed < .25f || textSpeed > 4f) textSpeed = 1f;
        }

        /// <summary>How scenes should play for this player: the Chronicle's own preferences plus the game's accessibility settings.</summary>
        public ChronicleSettings ToSettings(bool reduceMotion, bool reduceFlashing) { Ensure(); return new ChronicleSettings { instantText = instantText, textSpeed = textSpeed, reduceMotion = reduceMotion, reduceFlashing = reduceFlashing }; }
        static int[] Grow(int[] old) { var a = new int[3]; if (old != null) Array.Copy(old, a, Math.Min(old.Length, 3)); return a; }

        // ---------- queries ----------
        public bool IsUnlocked(string memoryId) => unlockedMemories.Contains(memoryId);
        public bool IsViewed(string memoryId) => viewedMemories.Contains(memoryId);
        public bool HasFact(string factId) => discoveredFacts.Contains(factId);
        public bool HasCorrection(string correctionId) => corrections.Contains(correctionId);
        public int TotalUnlocked => unlockedMemories.Count;
        public int TotalViewed => viewedMemories.Count;
        public bool AllMemoriesUnlocked => unlockedMemories.Count >= ChronicleCatalog.MemoryCount;
        public int ChapterUnlockedCount(int chapter) { var n = 0; foreach (var m in ChronicleCatalog.MemoriesInChapter(chapter)) if (IsUnlocked(m.id)) n++; return n; }
        public int ChapterViewedCount(int chapter) { var n = 0; foreach (var m in ChronicleCatalog.MemoriesInChapter(chapter)) if (IsViewed(m.id)) n++; return n; }
        public bool IsChapterComplete(int chapter) => ChapterViewedCount(chapter) >= 3;
        public bool IsChapterVisible(int chapter) => ChapterUnlockedCount(chapter) > 0;

        public bool RuleMet(ChronicleUnlockRule rule, ChronicleHero hero)
        {
            var h = (int)hero;
            switch (rule.metric)
            {
                case ChronicleMetric.RunsEnded: return runsEnded[h] >= rule.threshold;
                case ChronicleMetric.BestFloor: return bestFloor[h] >= rule.threshold;
                case ChronicleMetric.ActBossDefeated: return (actBossMask[h] & (1 << (rule.act - 1))) != 0;
                case ChronicleMetric.Wins: return wins[h] >= rule.threshold;
            }
            return false;
        }

        /// <summary>How the narrator presents right now. Derived from the corrections the player has uncovered.</summary>
        public ChronicleNarratorState NarratorState
        {
            get
            {
                if (secretCompleted) return ChronicleNarratorState.RememberingObserver;
                var late = 0; for (var c = 7; c <= 9; c++) late += ChapterViewedCount(c);
                if (corrections.Count >= 6 || late >= 3) return ChronicleNarratorState.RememberingObserver;
                if (corrections.Count >= 3) return ChronicleNarratorState.DoubtingHistorian;
                return ChronicleNarratorState.ConfidentHistorian;
            }
        }

        // ---------- unlocks ----------
        /// <summary>Evaluates every rule and unlocks what is earned. Safe to call repeatedly; each memory unlocks once.</summary>
        public ChronicleUnlockResult RefreshUnlocks()
        {
            Ensure();
            var result = new ChronicleUnlockResult();
            foreach (var memory in ChronicleCatalog.Memories)
            {
                if (IsUnlocked(memory.id) || !RuleMet(memory.rule, memory.hero)) continue;
                unlockedMemories.Add(memory.id); pendingNotices.Add(memory.id); result.newMemories.Add(memory.id);
            }
            if (!secretUnlocked && AllMemoriesUnlocked)
            {
                secretUnlocked = true; pendingNotices.Add(ChronicleCatalog.SecretChapterId); result.secretUnlocked = true;
            }
            return result;
        }

        // ---------- milestones (called by the game's real progression hooks) ----------
        /// <summary>A run ended. act = the act the player was in, floor = overall floor 1-54. Counted once per runId.</summary>
        public ChronicleUnlockResult RegisterRunEnded(ChronicleHero hero, bool victory, int act, int overallFloor, string runId)
        {
            Ensure();
            if (!string.IsNullOrEmpty(runId))
            {
                if (countedRunIds.Contains(runId)) return new ChronicleUnlockResult();
                countedRunIds.Add(runId); if (countedRunIds.Count > 256) countedRunIds.RemoveRange(0, 128);
            }
            var h = (int)hero;
            runsEnded[h]++;
            if (victory) { wins[h]++; actBossMask[h] |= 7; overallFloor = Math.Max(overallFloor, 54); }
            else
            {
                // Entering an act means its predecessor's boss fell.
                if (act >= 2) actBossMask[h] |= 1;
                if (act >= 3) actBossMask[h] |= 2;
            }
            bestFloor[h] = Math.Max(bestFloor[h], Math.Min(54, overallFloor));
            return RefreshUnlocks();
        }

        public ChronicleUnlockResult RegisterBossDefeated(ChronicleHero hero, int act, int overallFloor)
        {
            Ensure();
            if (act < 1 || act > 3) return new ChronicleUnlockResult();
            var h = (int)hero;
            actBossMask[h] |= 1 << (act - 1);
            bestFloor[h] = Math.Max(bestFloor[h], Math.Min(54, overallFloor));
            return RefreshUnlocks();
        }

        public ChronicleUnlockResult RegisterFloorReached(ChronicleHero hero, int overallFloor)
        {
            Ensure();
            var h = (int)hero;
            if (overallFloor <= bestFloor[h]) return new ChronicleUnlockResult();
            bestFloor[h] = Math.Min(54, overallFloor);
            return RefreshUnlocks();
        }

        /// <summary>Records that a story-relevant event was met. Events never gate memories (they are random); this exists for future lore hooks.</summary>
        public void RegisterEventEncountered(string eventId)
        {
            Ensure();
            if (string.IsNullOrEmpty(eventId) || eventsSeen.Contains(eventId)) return;
            eventsSeen.Add(eventId); if (eventsSeen.Count > 128) eventsSeen.RemoveAt(0);
        }

        /// <summary>One-time seed from the old profile so players who already won runs keep the progress they earned.</summary>
        public ChronicleUnlockResult SeedFromLegacy(IEnumerable<ChronicleLegacyRun> history, int[] winsByHero)
        {
            Ensure();
            if (legacyMigrated) return new ChronicleUnlockResult();
            legacyMigrated = true;
            if (history != null)
                foreach (var r in history)
                {
                    if (r.hero < 0 || r.hero > 2) continue;
                    runsEnded[r.hero]++;
                    bestFloor[r.hero] = Math.Max(bestFloor[r.hero], Math.Min(54, r.floor));
                    if (r.act >= 2) actBossMask[r.hero] |= 1;
                    if (r.act >= 3) actBossMask[r.hero] |= 2;
                    if (r.victory) actBossMask[r.hero] |= 7;
                }
            if (winsByHero != null)
                for (var h = 0; h < 3 && h < winsByHero.Length; h++)
                {
                    wins[h] = Math.Max(wins[h], winsByHero[h]);
                    runsEnded[h] = Math.Max(runsEnded[h], wins[h]);
                    if (wins[h] > 0) { actBossMask[h] |= 7; bestFloor[h] = 54; }
                }
            return RefreshUnlocks();
        }

        // ---------- commits (scene playback and the archive) ----------
        public bool DiscoverFact(string factId)
        {
            Ensure();
            if (string.IsNullOrEmpty(factId) || discoveredFacts.Contains(factId)) return false;
            discoveredFacts.Add(factId); return true;
        }

        /// <summary>Permanently records a correction. Idempotent: a repeated discovery changes nothing.</summary>
        public bool CommitCorrection(string correctionId)
        {
            Ensure();
            if (ChronicleCatalog.FindCorrection(correctionId) == null || corrections.Contains(correctionId)) return false;
            corrections.Add(correctionId); return true;
        }

        /// <summary>Marks a memory as watched and records its facts and corrections. Locked memories are refused unless allowLocked (developer sandbox).</summary>
        public ChronicleCommitResult CommitMemoryViewed(string memoryId, bool allowLocked = false)
        {
            Ensure();
            var result = new ChronicleCommitResult();
            var memory = ChronicleCatalog.FindMemory(memoryId);
            if (memory == null) { result.rejectReason = "Unknown memory " + memoryId; return result; }
            if (!allowLocked && !IsUnlocked(memoryId)) { result.rejectReason = memoryId + " is still locked"; return result; }
            result.accepted = true;
            if (!viewedMemories.Contains(memoryId)) { viewedMemories.Add(memoryId); result.firstView = true; }
            foreach (var fact in memory.facts) if (DiscoverFact(fact)) result.newFacts.Add(fact);
            foreach (var correction in memory.corrections) if (CommitCorrection(correction)) result.newCorrections.Add(correction);
            return result;
        }

        public ChronicleCommitResult CommitSecretCompleted(bool allowLocked = false)
        {
            Ensure();
            var result = new ChronicleCommitResult();
            if (!allowLocked && !secretUnlocked) { result.rejectReason = "Chapter X is still locked"; return result; }
            result.accepted = true;
            if (!secretCompleted) { secretCompleted = true; result.firstView = true; result.secretCompleted = true; }
            if (DiscoverFact("FACT_NARRATOR_IS_OBSERVER")) result.newFacts.Add("FACT_NARRATOR_IS_OBSERVER");
            if (CommitCorrection("CORR_10")) result.newCorrections.Add("CORR_10");
            return result;
        }

        public void CommitOpeningViewed() { Ensure(); openingViews++; }

        /// <summary>Which opening to play at launch: 0 none, 1 full, 2 short.</summary>
        public int OpeningToPlay => openingMode == 2 ? 0 : openingMode == 1 && openingViews > 0 ? 2 : 1;

        /// <summary>The unlock notices waiting to be shown. Returns them and clears the queue.</summary>
        public List<string> TakePendingNotices() { Ensure(); var copy = new List<string>(pendingNotices); pendingNotices.Clear(); return copy; }

        /// <summary>Clears Chronicle progress. Used only by the developer sandbox and explicit profile resets.</summary>
        public void ResetAll()
        {
            var keepMode = openingMode; var keepInstant = instantText; var keepSpeed = textSpeed;
            unlockedMemories.Clear(); viewedMemories.Clear(); discoveredFacts.Clear(); corrections.Clear(); pendingNotices.Clear(); countedRunIds.Clear(); eventsSeen.Clear();
            runsEnded = new int[3]; wins = new int[3]; bestFloor = new int[3]; actBossMask = new int[3];
            secretUnlocked = secretCompleted = legacyMigrated = false; openingViews = 0; lastChapterOpened = 0; openingMode = keepMode; instantText = keepInstant; textSpeed = keepSpeed;
        }
    }
}
