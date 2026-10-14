#if UNITY_EDITOR || GILDED_CHRONICLE_QA || GF_HARNESS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GildedFate.Chronicle
{
    // CHRONICLE QA: the arithmetic behind the QA build's performance readout, with no engine and no drawing.
    // It turns "how long did each frame take and how much managed memory was there" into frame rate, percentiles, hitch counts and an
    // approximate garbage-per-frame figure, and into one CSV row per second that a tester can send back. Compiled only in the Editor,
    // the QA build and the test harness (never in a normal player build; ci/verify-qa-config.sh and verify-qa-build.sh check that).

    public struct ChronicleQaPerfSecond
    {
        public double time;                 // seconds since the monitor started, at the end of this one-second window
        public int frames;
        public double fps, avgMs, p95Ms, p99Ms, worstMs;
        public double allocKbPerFrame;      // approximate managed allocation per frame (see ChronicleQaPerfStats.Record)
        public int gen0Collections, hitches33;
        public double managedMb;
    }

    public sealed class ChronicleQaPerfStats
    {
        public const int Window = 600;          // frames kept for the rolling figures shown on screen
        public const double HitchMs = 33.4;     // slower than 30 fps
        public const double BadHitchMs = 50.0;  // a stutter the player will see
        public const double SevereHitchMs = 100.0;
        const double BucketMs = 0.5; const int Buckets = 400;   // whole-session histogram, 0..200 ms in 0.5 ms steps

        public const string CsvHeader = "time_s,frames,fps,avg_ms,p95_ms,p99_ms,worst_ms,alloc_kb_per_frame,gen0_collections,hitches_over_33ms,managed_mb,marker";

        readonly float[] ring = new float[Window]; int ringCount, ringHead;
        readonly int[] histogram = new int[Buckets + 1];
        readonly List<float> secondFrames = new List<float>(256);
        double secondElapsed, sessionTime; long secondAllocBytes; int secondAllocFrames, secondGen0, secondHitches;
        long lastMemory = -1; int lastGen0 = -1;

        public int TotalFrames, Hitches33, Hitches50, Hitches100, TotalGen0Collections;
        public double WorstMs, SessionSeconds, TotalAllocBytes; public int TotalAllocFrames;
        public ChronicleQaPerfSecond LastSecond;
        public bool HasSecond;
        readonly Queue<string> csvRows = new Queue<string>();
        public int Marks;

        /// <summary>Records one frame. frameSeconds is the real (unscaled) frame time.
        /// managedBytes is GC.GetTotalMemory(false) and gen0Collections is GC.CollectionCount(0).
        /// Allocation is approximated by the growth in managed memory from one frame to the next; a frame in which a collection ran is
        /// left out of the allocation figure (memory fell, so growth cannot be read), but the collection itself is counted.</summary>
        public void Record(double frameSeconds, long managedBytes, int gen0Collections)
        {
            if (frameSeconds < 0 || double.IsNaN(frameSeconds)) frameSeconds = 0;
            var ms = frameSeconds * 1000.0;
            TotalFrames++; SessionSeconds += frameSeconds; sessionTime += frameSeconds;
            if (ms > WorstMs) WorstMs = ms;
            if (ms > HitchMs) { Hitches33++; secondHitches++; if (ms > BadHitchMs) Hitches50++; if (ms > SevereHitchMs) Hitches100++; }
            histogram[Math.Min(Buckets, (int)(ms / BucketMs))]++;
            ring[ringHead] = (float)ms; ringHead = (ringHead + 1) % Window; if (ringCount < Window) ringCount++;
            secondFrames.Add((float)ms); secondElapsed += frameSeconds;

            if (lastMemory >= 0)
            {
                var collected = gen0Collections != lastGen0;
                if (collected) { var n = gen0Collections - lastGen0; secondGen0 += n; TotalGen0Collections += n; }
                else { var grown = Math.Max(0, managedBytes - lastMemory); secondAllocBytes += grown; secondAllocFrames++; TotalAllocBytes += grown; TotalAllocFrames++; }
            }
            lastMemory = managedBytes; lastGen0 = gen0Collections; ManagedBytes = managedBytes;

            if (secondElapsed >= 1.0) CloseSecond();
        }

        public long ManagedBytes { get; private set; }

        void CloseSecond()
        {
            secondFrames.Sort();
            var n = secondFrames.Count; double sum = 0; foreach (var f in secondFrames) sum += f;
            LastSecond = new ChronicleQaPerfSecond
            {
                time = sessionTime, frames = n, fps = n / secondElapsed, avgMs = sum / n,
                p95Ms = secondFrames[Math.Min(n - 1, (int)(n * 0.95))], p99Ms = secondFrames[Math.Min(n - 1, (int)(n * 0.99))], worstMs = secondFrames[n - 1],
                allocKbPerFrame = secondAllocFrames > 0 ? secondAllocBytes / 1024.0 / secondAllocFrames : 0,
                gen0Collections = secondGen0, hitches33 = secondHitches, managedMb = ManagedBytes / 1048576.0
            };
            HasSecond = true; csvRows.Enqueue(Csv(LastSecond, ""));
            secondFrames.Clear(); secondElapsed = 0; secondAllocBytes = 0; secondAllocFrames = 0; secondGen0 = 0; secondHitches = 0;
        }

        static string F(double v, string fmt = "0.00") => v.ToString(fmt, CultureInfo.InvariantCulture);
        public static string Csv(ChronicleQaPerfSecond s, string marker) =>
            F(s.time, "0.0") + "," + s.frames + "," + F(s.fps, "0.0") + "," + F(s.avgMs) + "," + F(s.p95Ms) + "," + F(s.p99Ms) + "," + F(s.worstMs) + "," + F(s.allocKbPerFrame) + "," + s.gen0Collections + "," + s.hitches33 + "," + F(s.managedMb, "0.0") + "," + marker;

        /// <summary>Queues a marker row so a tester can say "mark 3 is where the page turn stuttered".</summary>
        public int Mark()
        {
            Marks++;
            var row = new ChronicleQaPerfSecond { time = sessionTime, frames = 0, managedMb = ManagedBytes / 1048576.0 };
            csvRows.Enqueue(Csv(row, "MARK " + Marks)); return Marks;
        }

        /// <summary>Returns the CSV rows produced since the last call, oldest first.</summary>
        public List<string> TakeCsvRows() { var list = new List<string>(csvRows); csvRows.Clear(); return list; }

        // ---- rolling figures (the last Window frames) ----
        public int RollingFrames => ringCount;
        public double RollingAvgMs { get { if (ringCount == 0) return 0; double s = 0; for (var i = 0; i < ringCount; i++) s += ring[i]; return s / ringCount; } }
        public double RollingFps { get { var a = RollingAvgMs; return a > 0 ? 1000.0 / a : 0; } }
        public double RollingPercentileMs(double p)
        {
            if (ringCount == 0) return 0;
            var copy = new float[ringCount]; Array.Copy(ring, copy, ringCount); Array.Sort(copy);
            return copy[Math.Min(ringCount - 1, Math.Max(0, (int)Math.Ceiling(ringCount * p) - 1))];
        }
        public double RollingWorstMs { get { double w = 0; for (var i = 0; i < ringCount; i++) if (ring[i] > w) w = ring[i]; return w; } }

        // ---- whole session ----
        public double SessionPercentileMs(double p)
        {
            if (TotalFrames == 0) return 0;
            var target = (long)Math.Ceiling(TotalFrames * p); long seen = 0;
            for (var i = 0; i <= Buckets; i++) { seen += histogram[i]; if (seen >= target) return (i + 1) * BucketMs; }
            return Buckets * BucketMs;
        }
        public double SessionAvgFps => SessionSeconds > 0 ? TotalFrames / SessionSeconds : 0;
        public double SessionAllocKbPerFrame => TotalAllocFrames > 0 ? TotalAllocBytes / 1024.0 / TotalAllocFrames : 0;

        /// <summary>The one-line readout drawn on screen.</summary>
        public string OverlayText()
        {
            return "FPS " + F(RollingFps, "0") + "  frame " + F(RollingAvgMs, "0.0") + " ms  p95 " + F(RollingPercentileMs(.95), "0.0") + "  p99 " + F(RollingPercentileMs(.99), "0.0") + "  worst " + F(RollingWorstMs, "0.0") +
                   "\nGC ~" + F(HasSecond ? LastSecond.allocKbPerFrame : 0, "0.0") + " KB/frame  collections " + TotalGen0Collections + "  heap " + F(ManagedBytes / 1048576.0, "0") + " MB" +
                   "\nhitches >33 ms: " + Hitches33 + "   >50 ms: " + Hitches50 + "   >100 ms: " + Hitches100;
        }

        /// <summary>The summary written when the QA build closes, to send back with the CSV.</summary>
        public string Summary(string machine)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Gilded Fate Chronicle QA performance summary");
            if (!string.IsNullOrEmpty(machine)) sb.AppendLine(machine);
            sb.AppendLine("Session: " + F(SessionSeconds, "0") + " s, " + TotalFrames + " frames, average " + F(SessionAvgFps, "0.0") + " fps");
            sb.AppendLine("Frame time (whole session): p50 " + F(SessionPercentileMs(.50), "0.0") + " ms, p95 " + F(SessionPercentileMs(.95), "0.0") + " ms, p99 " + F(SessionPercentileMs(.99), "0.0") + " ms, worst " + F(WorstMs, "0.0") + " ms");
            sb.AppendLine("Hitches: " + Hitches33 + " frames over 33 ms, " + Hitches50 + " over 50 ms, " + Hitches100 + " over 100 ms");
            sb.AppendLine("Garbage: about " + F(SessionAllocKbPerFrame, "0.0") + " KB allocated per frame, " + TotalGen0Collections + " collections");
            sb.AppendLine("Markers pressed (F12): " + Marks);
            return sb.ToString();
        }
    }
}
#endif
