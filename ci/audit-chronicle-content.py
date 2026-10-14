#!/usr/bin/env python3
"""Read-only audit of the authored Chronicle Ascension content (Python 3 standard library only).

    python3 ci/audit-chronicle-content.py                      # print the audit, exit 1 on any FAIL
    python3 ci/audit-chronicle-content.py --checklist OUT.md   # also write the human listen/watch checklist
    python3 ci/audit-chronicle-content.py --require-media      # fail (instead of skipping) when art/audio are unresolved Git LFS pointers

It never changes the story text, the recordings or the art. It checks that the pieces of the content pipeline agree with each other:

  scene source (SourceAssets/ChronicleAscension/Scenes/*.json)
    -> generated runtime script (ChronicleScripts.Ascension.cs)   every journal string verbatim, parts rejoined
    -> DialogueIndex.json                                         narrated text, SHA-256, word timings
    -> recordings (runtime and source copies)                     real WAV files, length, level, clipping, silence
    -> art studies                                                real PNG files, same bytes as the manifest

It cannot judge delivery, pronunciation or how anything looks. That needs a person, which is what --checklist is for.
"""
import argparse, csv, glob, hashlib, json, math, os, re, statistics, struct, sys, wave
from array import array

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SRC = os.path.join(ROOT, "SourceAssets", "ChronicleAscension")
RUNTIME_AUDIO = os.path.join(ROOT, "Assets", "Resources")
INDEX = os.path.join(ROOT, "Assets", "Resources", "Chronicle", "Audio", "DialogueIndex.json")
GENERATED = os.path.join(ROOT, "Assets", "Scripts", "Chronicle", "Scripts", "ChronicleScripts.Ascension.cs")
RUNTIME_ART = os.path.join(ROOT, "Assets", "Resources", "Chronicle", "Illustrations", "Ascension")
ORIGINAL_NARRATOR = os.path.join(ROOT, "Assets", "Resources", "Chronicle", "Audio", "Dialogue", "Narrator")

results = []  # (status, group, message)
skipped = []  # media files that are Git LFS pointers in this checkout (CI checks out without LFS), so their audio/image checks cannot run


def add(status, group, msg): results.append((status, group, msg))
def ok(group, msg): add("PASS", group, msg)
def fail(group, msg): add("FAIL", group, msg)
def warn(group, msg): add("WARN", group, msg)
def info(group, msg): add("INFO", group, msg)


def sha(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""): h.update(chunk)
    return h.hexdigest()


def sha_text(text): return hashlib.sha256(text.encode("utf-8")).hexdigest()
def is_lfs_pointer(path):
    with open(path, "rb") as f: return f.read(40).startswith(b"version https://git-lfs")
def load_json(path):
    with open(path, encoding="utf-8-sig") as f: return json.load(f)


# ---------------------------------------------------------------- C# string literals
ESCAPES = {'"': '"', "\\": "\\", "'": "'", "n": "\n", "t": "\t", "r": "\r", "0": "\0"}
def unescape(s):
    out, i = [], 0
    while i < len(s):
        c = s[i]
        if c != "\\": out.append(c); i += 1; continue
        n = s[i + 1]
        if n == "u": out.append(chr(int(s[i + 2:i + 6], 16))); i += 6
        elif n in ESCAPES: out.append(ESCAPES[n]); i += 2
        else: out.append(n); i += 2
    return "".join(out)


def generated_blocks(source):
    """{sceneId: {blockKey: text}} from the generated script's w.Write("key", "text", ...) calls."""
    scenes = {}
    for m in re.finditer(r"static ChronicleScene (ASC_[A-Z]+_\d+)\(\)\s*\{(.*?)\n        \}", source, re.S):
        blocks = {}
        for w in re.finditer(r'w\.Write\("([^"]+)",\s*"((?:[^"\\]|\\.)*)"', m.group(2)): blocks[w.group(1)] = unescape(w.group(2))
        scenes[m.group(1)] = blocks
    return scenes


def rejoin(blocks, key):
    """The generated script splits long silent passages into key, key_2, key_3 ...; the source keeps them as one block."""
    parts, n = [blocks[key]] if key in blocks else [], 2
    while f"{key}_{n}" in blocks: parts.append(blocks[f"{key}_{n}"]); n += 1
    return " ".join(parts) if parts else None


# ---------------------------------------------------------------- audio
def analyse_wav(path):
    with wave.open(path, "rb") as w:
        ch, width, rate, frames = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        data = w.readframes(frames)
    if width == 3:  # keep the top 16 bits of each 24-bit sample: plenty for level, clipping and silence
        out = bytearray(2 * (len(data) // 3)); out[0::2] = data[1::3]; out[1::2] = data[2::3]
        samples = array("h"); samples.frombytes(bytes(out))
    elif width == 2:
        samples = array("h"); samples.frombytes(data)
    else:
        raise ValueError(f"unsupported sample width {width}")
    if ch > 1: samples = samples[::ch]
    n = len(samples)
    if n == 0: return dict(duration=0, rate=rate, channels=ch, bits=width * 8, peak_db=-120, rms_db=-120, lead=0, trail=0)
    peak = max(max(samples), -min(samples)) / 32768.0
    step = 3; sq = sum(x * x for x in samples[::step]) / len(samples[::step])
    rms = math.sqrt(sq) / 32768.0
    gate = 32768 * 10 ** (-50 / 20)
    first = next((i for i, x in enumerate(samples) if abs(x) >= gate), n)
    last = next((n - 1 - i for i, x in enumerate(reversed(samples)) if abs(x) >= gate), 0)
    db = lambda v: 20 * math.log10(v) if v > 0 else -120.0
    return dict(duration=frames / rate, rate=rate, channels=ch, bits=width * 8, peak_db=db(peak), rms_db=db(rms), lead=first / rate, trail=(n - 1 - last) / rate)


def png_size(path):
    with open(path, "rb") as f: head = f.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n": return None
    return struct.unpack(">II", head[16:24])


# ---------------------------------------------------------------- the audit
def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--checklist", metavar="OUT.md", help="also write the human listen/watch checklist")
    ap.add_argument("--require-media", action="store_true", help="treat unresolved Git LFS pointers as failures instead of skipping their checks")
    args = ap.parse_args()

    for p in (SRC, INDEX, GENERATED):
        if not os.path.exists(p): print(f"missing: {os.path.relpath(p, ROOT)}"); return 2

    # --- scenes
    G = "scenes"
    scene_files = sorted(glob.glob(os.path.join(SRC, "Scenes", "*.json")))
    scenes = {}
    for f in scene_files:
        d = load_json(f); scenes[d["sceneId"]] = d
    (ok if len(scenes) == 30 else fail)(G, f"{len(scenes)} of 30 scene source files")
    expected = [f"ASC_{c}_{t:02d}" for c in ("VAN", "HEX", "REA") for t in range(1, 11)]
    missing = [e for e in expected if e not in scenes]
    (ok if not missing else fail)(G, "every ASC_VAN/HEX/REA_01..10 id has a scene" if not missing else "missing scene ids: " + ", ".join(missing))
    majors = [s for s, d in scenes.items() if d.get("kind") == "MAJOR"]
    wrong_kind = [s for s, d in scenes.items() if (d.get("kind") == "MAJOR") != (d["fateDebt"] in (1, 4, 7, 10))]
    (ok if len(majors) == 12 and not wrong_kind else fail)(G, f"{len(majors)} major corrections, exactly at Fate Debt I, IV, VII, X" if not wrong_kind else "wrong kind for: " + ", ".join(wrong_kind))
    bad_hash = []
    for sid, d in scenes.items():
        for b in d["journalBlocks"]:
            if sha_text(b["text"]) != b["textSHA256"]: bad_hash.append(f"{sid}/{b['blockKey']}")
    (ok if not bad_hash else fail)(G, f"every journal block matches its recorded SHA-256 ({sum(len(d['journalBlocks']) for d in scenes.values())} blocks)" if not bad_hash else "SHA-256 mismatch: " + ", ".join(bad_hash[:8]))

    # --- generated script
    G = "generated script"
    gen_src = open(GENERATED, encoding="utf-8-sig").read()
    gen = generated_blocks(gen_src)
    registered = set(re.findall(r'factories\["(ASC_[A-Z]+_\d+)"\]', gen_src))
    (ok if registered == set(expected) else fail)(G, f"{len(registered)} of 30 scenes registered" if registered == set(expected) else f"registered ids differ from the catalog: {sorted(set(expected) ^ registered)}")
    diffs = []
    for sid, d in scenes.items():
        blocks = gen.get(sid)
        if blocks is None: diffs.append(f"{sid}: no generated scene"); continue
        for b in d["journalBlocks"]:
            text = rejoin(blocks, b["blockKey"])
            if text is None: diffs.append(f"{sid}/{b['blockKey']}: not written")
            elif text != b["text"]: diffs.append(f"{sid}/{b['blockKey']}: text differs from the source")
        source_keys = {b["blockKey"] for b in d["journalBlocks"]}
        for k in sorted(blocks):
            base = re.sub(r"_\d+$", "", k)
            if k not in source_keys and base not in source_keys: diffs.append(f"{sid}/{k}: written in the script but not in the source")
    (ok if not diffs else fail)(G, "every journal string in the script is verbatim from the source scenes" if not diffs else f"{len(diffs)} differences, first: " + "; ".join(diffs[:5]))

    # --- dialogue index
    G = "dialogue index"
    index = load_json(INDEX)
    asc = {l["lineId"]: l for l in index["lines"] if str(l.get("sceneId", "")).startswith("ASC_")}
    narrated = [(sid, b) for sid, d in scenes.items() for b in d["journalBlocks"] if b.get("mode") == "NARRATED"]
    (ok if len(asc) == 45 and len(narrated) == 45 else fail)(G, f"{len(asc)} indexed recordings, {len(narrated)} narrated blocks in the source (both expected 45)")
    problems = []
    for sid, b in narrated:
        line = asc.get(f"{sid}__{b['blockKey']}")
        if line is None: problems.append(f"{sid}/{b['blockKey']}: no index line"); continue
        if line["text"] != b["text"] or line["scriptText"] != b["text"]: problems.append(f"{line['lineId']}: index text differs from the source")
        if sha_text(line["text"]) != line["textSHA256"]: problems.append(f"{line['lineId']}: index SHA-256 does not match its text")
        if not line.get("ready"): problems.append(f"{line['lineId']}: not marked ready")
    (ok if not problems else fail)(G, "every narrated block is bound to exactly one index line with identical text and a valid SHA-256" if not problems else f"{len(problems)} problems, first: " + "; ".join(problems[:5]))
    wprob = []
    for lid, l in asc.items():
        ws = l.get("words", []); prev = 0.0
        for w in ws:
            if l["text"][w["charStart"]:w["charStart"] + w["charLength"]] != w["text"]: wprob.append(f"{lid}: word '{w['text']}' does not sit at its character range")
            if w["startSeconds"] < prev - 1e-6 or w["endSeconds"] < w["startSeconds"]: wprob.append(f"{lid}: word '{w['text']}' timing out of order")
            prev = w["startSeconds"]
        if ws and ws[-1]["endSeconds"] > l["durationSeconds"] + 0.05: wprob.append(f"{lid}: last word ends after the clip does")
        if len(ws) != len(l["text"].split()): wprob.append(f"{lid}: {len(ws)} timed words for {len(l['text'].split())} written words")
    (ok if not wprob else fail)(G, "word timings sit on their characters, run in order and finish inside each clip" if not wprob else f"{len(wprob)} timing problems, first: " + "; ".join(wprob[:5]))

    # --- recordings
    G = "recordings"
    rows = {r["assetLineKey"]: r for r in csv.DictReader(open(os.path.join(SRC, "Audio", "Manifests", "Listening_Review.csv"), encoding="utf-8-sig"))}
    stats, bad = {}, []
    for lid, l in asc.items():
        path = os.path.join(RUNTIME_AUDIO, l["audio"] + ".wav")
        if not os.path.exists(path): bad.append(f"{lid}: runtime file missing"); continue
        if is_lfs_pointer(path): skipped.append(os.path.relpath(path, ROOT)); continue
        try: a = analyse_wav(path)
        except Exception as e: bad.append(f"{lid}: not a readable WAV ({e})"); continue
        stats[lid] = a
        if abs(a["duration"] - l["durationSeconds"]) > 0.05: bad.append(f"{lid}: recording is {a['duration']:.2f}s but the index says {l['durationSeconds']:.2f}s")
    (ok if not bad else fail)(G, f"{len(stats)} of {len(asc)} runtime recordings exist, are real WAV files and match their indexed length (+/-0.05 s)" if not bad else f"{len(bad)} problems, first: " + "; ".join(bad[:5]))
    formats = {(a["rate"], a["bits"], a["channels"]) for a in stats.values()}
    (ok if len(formats) <= 1 else fail)(G, f"one audio format throughout: {sorted(formats)}")
    # the runtime copy must be the same bytes as the source recording
    mism, unmatched = [], 0
    for sid, b in narrated:
        r = rows.get(b.get("assetLineKey", ""))
        line = asc.get(f"{sid}__{b['blockKey']}")
        if not r or not line: unmatched += 1; continue
        s_path, r_path = os.path.join(ROOT, r["file"]), os.path.join(RUNTIME_AUDIO, line["audio"] + ".wav")
        if not (os.path.exists(s_path) and os.path.exists(r_path)): mism.append(f"{line['lineId']}: a copy is missing"); continue
        if is_lfs_pointer(s_path) or is_lfs_pointer(r_path): continue
        if sha(s_path) != sha(r_path): mism.append(f"{line['lineId']}: runtime recording differs from the source recording")
    (ok if not mism and not unmatched else fail)(G, "runtime recordings are byte-identical to the source recordings" if not mism and not unmatched else f"{len(mism)} differ, {unmatched} unmatched: " + "; ".join(mism[:4]))

    # level, clipping and silence against the original narrator
    originals = sorted(glob.glob(os.path.join(ORIGINAL_NARRATOR, "*.wav")))
    originals = [p for p in originals if not os.path.basename(p).startswith("ASC_")][::8]
    base_rms, base_peak = [], []
    for p in originals:
        if is_lfs_pointer(p): continue
        try: a = analyse_wav(p)
        except Exception: continue
        base_rms.append(a["rms_db"]); base_peak.append(a["peak_db"])
    if base_rms and stats:
        med_rms, med_peak = statistics.median(base_rms), statistics.median(base_peak)
        new_rms = [a["rms_db"] for a in stats.values()]; new_peak = [a["peak_db"] for a in stats.values()]
        info(G, f"level (RMS): new recordings median {statistics.median(new_rms):.1f} dBFS, range {min(new_rms):.1f} to {max(new_rms):.1f}; original narrator median {med_rms:.1f} dBFS ({len(base_rms)} sampled)")
        info(G, f"peak: new recordings highest {max(new_peak):.1f} dBFS; original narrator median {med_peak:.1f} dBFS")
        loud = [f"{lid} ({a['rms_db']:.1f} dBFS)" for lid, a in stats.items() if abs(a["rms_db"] - med_rms) > 3.0]
        (ok if not loud else warn)(G, "every new recording is within 3 dB of the original narrator's level" if not loud else f"{len(loud)} recordings are more than 3 dB from the original narrator's level (they will sound louder or quieter): " + ", ".join(loud[:8]))
        hot = [f"{lid} ({a['peak_db']:.2f} dBFS)" for lid, a in stats.items() if a["peak_db"] > -0.3]
        (ok if not hot else warn)(G, "no recording peaks within 0.3 dB of full scale (no clipping risk)" if not hot else "possible clipping: " + ", ".join(hot))
        long_lead = [f"{lid} ({a['lead']:.2f}s)" for lid, a in stats.items() if a["lead"] > 0.6]
        long_tail = [f"{lid} ({a['trail']:.2f}s)" for lid, a in stats.items() if a["trail"] > 1.5]
        (ok if not long_lead else warn)(G, "no recording starts with a long silence" if not long_lead else "long lead-in silence (the writing will wait for the voice): " + ", ".join(long_lead[:8]))
        (ok if not long_tail else warn)(G, "no recording ends with a long silence" if not long_tail else "long trailing silence: " + ", ".join(long_tail[:8]))

    # --- art
    G = "art"
    art = load_json(os.path.join(SRC, "Art", "Art_Manifest.json"))["assets"]
    aprob, in_runtime = [], 0
    for a in art:
        p = os.path.join(ROOT, a["file"])
        if not os.path.exists(p): aprob.append(f"{a['visualKey']}: file missing"); continue
        if is_lfs_pointer(p): skipped.append(os.path.relpath(p, ROOT)); continue
        if png_size(p) != (a["width"], a["height"]): aprob.append(f"{a['visualKey']}: not a PNG of {a['width']}x{a['height']}")
        if sha(p) != a["sha256"]: aprob.append(f"{a['visualKey']}: bytes differ from the manifest")
        rp = os.path.join(RUNTIME_ART, os.path.basename(p))
        if os.path.exists(rp):
            in_runtime += 1
            if is_lfs_pointer(rp): skipped.append(os.path.relpath(rp, ROOT)); continue
            if sha(rp) != a["sha256"]: aprob.append(f"{a['visualKey']}: runtime copy differs from the manifest")
    (ok if not aprob else fail)(G, f"all {len(art)} art studies are real PNGs matching the manifest" if not aprob else "; ".join(aprob[:5]))
    (ok if in_runtime == len(art) else warn)(G, f"{in_runtime} of {len(art)} studies are also in the runtime Resources folder")

    # --- human review still outstanding
    G = "human review"
    pending = sum(1 for r in rows.values() if "PENDING" in (r.get("breathsAndCuts", ""), r.get("voiceConsistency", ""), r.get("emotionalDelivery", "")))
    info(G, f"{pending} of {len(rows)} recordings still have a PENDING listening review (breaths and cuts, voice consistency, emotional delivery). Only a person can finish that.")

    # --- media that could not be checked
    G = "lfs"
    pointers = sorted(set(skipped + [os.path.relpath(p, ROOT) for pat in ("SourceAssets/ChronicleAscension/**/*.wav", "SourceAssets/ChronicleAscension/**/*.png") for p in glob.glob(os.path.join(ROOT, pat), recursive=True) if is_lfs_pointer(p)]))
    if not pointers: ok(G, "every Chronicle recording and image that was checked is a real file, not an LFS pointer")
    elif args.require_media: fail(G, f"{len(pointers)} media files are unresolved Git LFS pointers (run 'git lfs pull'): " + ", ".join(pointers[:4]))
    else: info(G, f"{len(pointers)} media files are Git LFS pointers in this checkout, so their audio/image checks were skipped. Run 'git lfs pull' and use --require-media to check them.")

    # ---------------------------------------------------------------- report
    width = max(len(g) for _, g, _ in results)
    for status, group, msg in results: print(f"{status:<4}  {group:<{width}}  {msg}")
    fails = sum(1 for s, _, _ in results if s == "FAIL"); warns = sum(1 for s, _, _ in results if s == "WARN")
    print(f"\n{sum(1 for s, _, _ in results if s == 'PASS')} passed, {warns} warnings, {fails} failed")

    if args.checklist:
        write_checklist(args.checklist, scenes, asc, stats, rows)
        print("wrote " + args.checklist)
    return 1 if fails else 0


def write_checklist(path, scenes, asc, stats, rows):
    heroes = {"VAN": "Vanguard", "HEX": "Hexer", "REA": "Reaper"}
    out = ["# Chronicle QA: listen-and-watch checklist", "",
           "Generated by `ci/audit-chronicle-content.py --checklist`. Use it with the Chronicle QA build (`GildedFate-Windows-QA.zip`, F10 at the main menu).",
           "The machine audit already confirmed the text, recordings, timings and art agree with each other. This list is only what a person has to judge.", "",
           "## How to play each scene", "",
           "Panel > **Recollections** tab > pick the hero and Fate Debt > **PREVIEW**. Preview saves nothing, so you can repeat a scene as often as you like.", "",
           "## For every scene, check", "",
           "- Handwriting appears in step with the narrator, not ahead of or behind the voice.",
           "- Each narrated line is heard once, and silent passages stay silent.",
           "- A major correction first shows the original sentence, strikes it in red, then writes the new one.",
           "- Page turns look right and go the right way; the cover stays open between sheets.",
           "- Skipping (any key) lands on the finished page, and the scene still counts once in Record mode.",
           "- Nothing overlaps, runs off the page, or flickers. Try Reduce Motion and Reduce Flashing too.", "",
           "## For every recording, check", "",
           "- Wording matches the page exactly. Voice sounds like the existing narrator. No clicks, cuts or breaths in the wrong place. Emotion fits the moment.", "",
           "| Done | Scene | Hero | Fate Debt | Kind | Line | Length | Level (RMS) | Text |", "| --- | --- | --- | --- | --- | --- | ---: | ---: | --- |"]
    for sid in sorted(scenes, key=lambda s: (["VAN", "HEX", "REA"].index(s.split("_")[1]), int(s.split("_")[2]))):
        d = scenes[sid]; code = sid.split("_")[1]
        lines = [l for l in asc.values() if l["sceneId"] == sid]
        if not lines:
            out.append(f"| [ ] | {sid} | {heroes[code]} | {d['fateDebt']} | {d['kind'].title()} | _(silent scene)_ |  |  | {d['title']} |")
        for l in sorted(lines, key=lambda x: x["lineId"]):
            a = stats.get(l["lineId"]); text = l["text"].replace("|", "\\|")
            out.append(f"| [ ] | {sid} | {heroes[code]} | {d['fateDebt']} | {d['kind'].title()} | {l['commandId']} | {a['duration']:.1f}s | {a['rms_db']:.1f} dB | {text} |" if a else f"| [ ] | {sid} | {heroes[code]} | {d['fateDebt']} | {d['kind'].title()} | {l['commandId']} |  |  | {text} |")
    out += ["", "## Art studies", "", "Six small marginal illustrations. Check each looks right beside its text, is not cropped, and is not blurry at 1080p and at your largest window size.", ""]
    out += [f"- [ ] {os.path.basename(a)}" for a in sorted(glob.glob(os.path.join(SRC, "Art", "*.png")))]
    out += ["", "## Frame rate", "", "Watch for stutter during page turns, scene starts, and when many strokes are being written. Note which scene and what your PC is."]
    with open(path, "w", encoding="utf-8", newline="\n") as f: f.write("\n".join(out) + "\n")


if __name__ == "__main__":
    sys.exit(main())
