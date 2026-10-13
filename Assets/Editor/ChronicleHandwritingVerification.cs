using System;
using System.Collections.Generic;
using System.IO;
using GildedFate.ChronicleBook;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    [InitializeOnLoad]
    public static class ChronicleHandwritingVerification
    {
        const string Request = "Library/ChronicleHandwritingVerification.request";
        const string Output = @"C:\Users\river\Documents\Codex\2026-10-08\use-these-files-from-the-libray-4\outputs";
        [Serializable] class Report { public int checks; public List<string> failures = new(); public int fontSize, ascent; }
        static ChronicleHandwritingVerification() => EditorApplication.update += Wait;
        static void Wait()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(Request); Verify();
        }
        [MenuItem("Gilded Fate/Chronicle/Verify Handwriting Strokes")]
        public static void Verify()
        {
            var report = new Report(); Directory.CreateDirectory(Output);
            var font = Resources.Load<Font>("Chronicle/Fonts/ChronicleHandwriting");
            var sheet = new Texture2D(540, 700, TextureFormat.RGB24, false);
            var background = new Color32(235, 221, 187, 255); var pixels = new Color32[540 * 700];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = background;
            try
            {
                report.fontSize = font.fontSize; report.ascent = font.ascent;
                using (var writer = new ChronicleInkStrokeRenderer(font))
                {
                    var letters = "aefgitW"; var phases = new[] { 0f, .15f, .35f, .65f, .85f, 1f };
                    for (var row = 0; row < letters.Length; row++)
                    {
                        var previousInk = -1;
                        for (var col = 0; col < phases.Length; col++)
                        {
                            report.checks++;
                            var texture = writer.Sample(letters[row], phases[col], out var tip);
                            if (!texture) { report.failures.Add("Missing handwritten glyph: " + letters[row]); continue; }
                            var glyph = texture.GetPixels32(); var ink = 0;
                            foreach (var pixel in glyph) ink += pixel.a;
                            if (ink < previousInk) report.failures.Add("Ink disappears while writing " + letters[row]);
                            if (col == 0 && ink != 0 || col == phases.Length - 1 && ink == 0) report.failures.Add("Invalid start or completion for " + letters[row]);
                            if (col > 0 && col < phases.Length - 1 && ink == 0) report.failures.Add("The partial stroke is blank for " + letters[row]);
                            previousInk = ink;
                            var left = col * 90 + (90 - texture.width) / 2; var bottom = 700 - (row + 1) * 100 + 12;
                            for (var y = 0; y < texture.height; y++) for (var x = 0; x < texture.width; x++)
                            {
                                var px = left + x; var py = bottom + y; if (px < 0 || px >= 540 || py < 0 || py >= 700) continue;
                                var a = glyph[y * texture.width + x].a / 255f;
                                pixels[py * 540 + px] = new Color32((byte)Mathf.Lerp(background.r, 51, a), (byte)Mathf.Lerp(background.g, 34, a), (byte)Mathf.Lerp(background.b, 21, a), 255);
                            }
                        }
                        report.checks++;
                        var partial = writer.Sample(letters[row], 1f, out _).GetPixels32();
                        var finished = writer.FinishedSample(letters[row]).GetPixels32();
                        for (var i = 0; i < partial.Length; i++)
                            if (!partial[i].Equals(finished[i])) { report.failures.Add("Completed and dry ink differ for " + letters[row]); break; }
                        // A later partial occurrence must not erase a previously completed occurrence of the same letter.
                        writer.Sample(letters[row], .15f, out _);
                        var preserved = writer.FinishedSample(letters[row]).GetPixels32();
                        report.checks++;
                        for (var i = 0; i < finished.Length; i++)
                            if (!finished[i].Equals(preserved[i])) { report.failures.Add("Earlier occurrences are overwritten for " + letters[row]); break; }
                    }
                }
                sheet.SetPixels32(pixels); sheet.Apply(); File.WriteAllBytes(Path.Combine(Output, "Chronicle_Handwriting_Strokes.png"), sheet.EncodeToPNG());
            }
            catch (Exception error) { report.failures.Add(error.ToString()); }
            finally { UnityEngine.Object.DestroyImmediate(sheet); }
            File.WriteAllText(Path.Combine(Output, "Chronicle_Handwriting_Verification.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[Chronicle] Handwriting strokes: " + report.checks + " checks, " + report.failures.Count + " failures.");
        }
    }
}
