using System.Linq;
using NUnit.Framework;

namespace GildedFate.Chronicle.Tests
{
    /// <summary>The QA build's frame-time and garbage readout: the arithmetic, the hitch counts and the CSV a tester sends back.</summary>
    [TestFixture]
    public class ChronicleQaPerfTests
    {
        static ChronicleQaPerfStats Steady(double ms, int frames, long bytesPerFrame = 0)
        {
            var s = new ChronicleQaPerfStats(); long memory = 10_000_000;
            for (var i = 0; i < frames; i++) { s.Record(ms / 1000.0, memory, 0); memory += bytesPerFrame; }
            return s;
        }

        [Test] public void ASteadySixtyFramesPerSecondReadsAsSixtyWithNoHitches()
        {
            var s = Steady(1000.0 / 60, 600);
            Assert.AreEqual(60.0, s.RollingFps, .1); Assert.AreEqual(1000.0 / 60, s.RollingPercentileMs(.99), .01);
            Assert.AreEqual(0, s.Hitches33); Assert.AreEqual(0, s.Hitches50); Assert.AreEqual(0, s.Hitches100);
            Assert.AreEqual(60.0, s.SessionAvgFps, .1);
        }

        [Test] public void OneLongFrameIsCountedAsAHitchOfTheRightSeverityAndRemembered()
        {
            var s = Steady(16.7, 200); s.Record(0.080, 10_000_000, 0); for (var i = 0; i < 100; i++) s.Record(.0167, 10_000_000, 0);
            Assert.AreEqual(1, s.Hitches33); Assert.AreEqual(1, s.Hitches50); Assert.AreEqual(0, s.Hitches100);
            Assert.AreEqual(80.0, s.WorstMs, .01); Assert.AreEqual(80.0, s.RollingWorstMs, .01);
            s.Record(0.150, 10_000_000, 0); Assert.AreEqual(1, s.Hitches100);
            Assert.AreEqual(16.7, s.RollingPercentileMs(.50), .01, "one stutter does not move the median");
        }

        [Test] public void PercentilesAreCorrectForAKnownDistribution()
        {
            var s = new ChronicleQaPerfStats();
            for (var i = 1; i <= 100; i++) s.Record(i / 1000.0, 0, 0);   // 1 ms .. 100 ms, one of each
            Assert.AreEqual(50.0, s.RollingPercentileMs(.50), .001); Assert.AreEqual(95.0, s.RollingPercentileMs(.95), .001); Assert.AreEqual(99.0, s.RollingPercentileMs(.99), .001);
            Assert.AreEqual(50.0, s.SessionPercentileMs(.50), 1.0, "the whole-session histogram agrees to within its 0.5 ms buckets");
            Assert.AreEqual(95.0, s.SessionPercentileMs(.95), 1.0); Assert.AreEqual(99.0, s.SessionPercentileMs(.99), 1.0);
        }

        [Test] public void TheRollingWindowForgetsOldFramesButTheSessionKeepsThem()
        {
            var s = Steady(100, 50);                                   // a slow start
            for (var i = 0; i < ChronicleQaPerfStats.Window; i++) s.Record(.010, 0, 0);
            Assert.AreEqual(ChronicleQaPerfStats.Window, s.RollingFrames);
            Assert.AreEqual(10.0, s.RollingWorstMs, .001, "the slow frames have rolled out");
            Assert.AreEqual(100.0, s.WorstMs, .001, "but the session still knows its worst frame");
            Assert.AreEqual(50, s.Hitches50, "all 50 slow frames were counted when they happened");
        }

        [Test] public void AllocationIsTheGrowthInManagedMemoryAndACollectionFrameIsNotMisread()
        {
            var s = Steady(10, 100, 2048);
            Assert.AreEqual(2.0, s.SessionAllocKbPerFrame, .01);
            var before = s.TotalAllocBytes;
            s.Record(.010, 5_000_000, 1);                              // memory fell and gen 0 ran: a collection, not negative garbage
            Assert.AreEqual(before, s.TotalAllocBytes); Assert.AreEqual(1, s.TotalGen0Collections);
            s.Record(.010, 5_002_048, 1); Assert.AreEqual(before + 2048, s.TotalAllocBytes, "allocation counting resumes straight after");
        }

        [Test] public void OneCsvRowIsProducedPerSecondWithMatchingColumns()
        {
            var s = Steady(10, 250, 1024);                             // 2.5 s of 10 ms frames
            var rows = s.TakeCsvRows();
            Assert.AreEqual(2, rows.Count); Assert.AreEqual(0, s.TakeCsvRows().Count, "rows are handed out once");
            var columns = ChronicleQaPerfStats.CsvHeader.Split(',').Length;
            foreach (var r in rows) Assert.AreEqual(columns, r.Split(',').Length, r);
            Assert.AreEqual(100.0, s.LastSecond.fps, 1.0); Assert.AreEqual(1.0, s.LastSecond.allocKbPerFrame, .01);
        }

        [Test] public void MarkersLandInTheCsvInOrderAndNumbersUseTheInvariantCulture()
        {
            var s = Steady(10, 120, 0); s.TakeCsvRows();
            Assert.AreEqual(1, s.Mark()); s.Record(.010, 10_000_000, 0); Assert.AreEqual(2, s.Mark());
            var rows = s.TakeCsvRows();
            Assert.IsTrue(rows[0].EndsWith(",MARK 1")); Assert.IsTrue(rows[1].EndsWith(",MARK 2"));
            Assert.IsFalse(rows.Any(r => r.Contains(";")), "no culture-dependent separators");
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            try { var t = Steady(10, 120, 0).TakeCsvRows(); Assert.IsTrue(t[0].Contains("100.0"), t[0]); Assert.AreEqual(ChronicleQaPerfStats.CsvHeader.Split(',').Length, t[0].Split(',').Length); }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture; }
        }

        [Test] public void ReadoutAndSummaryDescribeTheSession()
        {
            var s = Steady(1000.0 / 60, 300, 4096); s.Record(.120, 10_500_000, 0);
            var o = s.OverlayText(); Assert.IsTrue(o.Contains("FPS") && o.Contains("hitches") && o.Contains("KB/frame"), o);
            var summary = s.Summary("GPU test / 16 GB");
            Assert.IsTrue(summary.Contains("GPU test / 16 GB")); Assert.IsTrue(summary.Contains("301 frames")); Assert.IsTrue(summary.Contains("1 over 100 ms"), summary);
        }

        [Test] public void NegativeOrBrokenFrameTimesCannotCorruptTheNumbers()
        {
            var s = new ChronicleQaPerfStats(); s.Record(-1, 0, 0); s.Record(double.NaN, 0, 0); s.Record(.016, 0, 0);
            Assert.AreEqual(3, s.TotalFrames); Assert.IsTrue(s.RollingAvgMs >= 0); Assert.IsFalse(double.IsNaN(s.RollingFps));
        }
    }
}
