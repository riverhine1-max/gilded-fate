using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GildedFate.Combat;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Saving;
using GildedFate.UI;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    // Exercises the actual combat texture cache on an inactive, isolated menu.
    // The scene, running game, profile and saved run are never modified.
    [InitializeOnLoad]
    public static class GildedEnemyArtworkVerification
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string Request = "Temp/gf-enemy-artwork-check.request";

        static GildedEnemyArtworkVerification()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(Request)) return;
                File.Delete(Request);
                Verify();
            };
        }

        [Serializable] private sealed class Result
        {
            public int enemies, encounters, combatSetups, textureChecks, distinctTextures;
            public List<string> failures = new();
        }

        [MenuItem("Gilded Fate/Verify Enemy Artwork")]
        public static void Verify()
        {
            var result = new Result { enemies = WorldContent.Enemies.Length };
            var host = new GameObject("Enemy artwork verification") { hideFlags = HideFlags.HideAndDontSave };
            host.SetActive(false);
            var menu = host.AddComponent<GildedMainMenu>();
            var type = typeof(GildedMainMenu);
            var run = (RunModel)type.GetField("run", Private).GetValue(menu);
            type.GetField("profile", Private).SetValue(menu, new PlayerProfile());
            var screenField = type.GetField("screen", Private);
            screenField.SetValue(menu, Enum.Parse(screenField.FieldType, "Combat"));
            var checkedTextures = new HashSet<Texture2D>();

            void Inspect(string label, string[] ids, int act, string theme, int phase = 1, string summonedId = null)
            {
                try
                {
                    run.NewRun(HeroId.Vanguard, 58201);
                    run.sandbox = true;
                    run.act = act;
                    run.actThemes[act - 1] = theme;
                    var first = WorldContent.Enemies.First(e => e.id == ids[0]);
                    var combat = new CombatState();
                    combat.Begin(HeroId.Vanguard, Enumerable.Repeat(GameContent.Find("strike"), 10),
                        first.hp, 0, 80, 80, null, first.id, first.baseDamage, 58201,
                        encounterEnemyIds: ids.Length > 1 ? ids : null);
                    if (summonedId == FracturedRealmContent.WardFragment)
                    {
                        // This minion is summoned after a threshold and cannot be in an opening lineup.
                        combat.MindAt(0).state = "WARD";
                        typeof(CombatState).GetMethod("WildSummon", Private).Invoke(combat, new object[] { 0 });
                        if (!Enumerable.Range(0, combat.EnemyCount).Any(i => combat.EnemyIdAt(i) == summonedId))
                            result.failures.Add(label + ": expected summoned minion did not spawn");
                    }
                    type.GetField("combat", Private).SetValue(menu, combat);
                    type.GetField("currentEnemy", Private).SetValue(menu, first);
                    if (phase > 1 && first.boss)
                    {
                        var boss = combat.WildBossIndex;
                        var fighter = boss >= 0 ? combat.EnemyAt(boss) : combat.enemy;
                        fighter.hp = Mathf.Max(1, Mathf.RoundToInt(fighter.maxHp * (phase == 2 ? .6f : .25f)));
                        combat.RefreshEnemyState(true);
                    }
                    type.GetMethod("ResetFinalCombatVfx", Private).Invoke(menu, null);
                    var textures = (Texture2D[])type.GetField("finalEnemyTextures", Private).GetValue(menu);
                    var definitions = (EnemyDef[])type.GetField("finalEnemyDefs", Private).GetValue(menu);
                    result.combatSetups++;
                    for (var i = 0; i < combat.EnemyCount; i++)
                    {
                        result.textureChecks++;
                        var id = combat.EnemyIdAt(i);
                        if (definitions[i] == null || definitions[i].id != id || !textures[i])
                        {
                            result.failures.Add(label + ": missing illustration for " + id);
                            continue;
                        }
                        if (checkedTextures.Add(textures[i])) CheckVisiblePixels(textures[i], result.failures);
                        if (phase > 1 && i == combat.WildBossIndex && !textures[i].name.EndsWith("_phase" + phase))
                            result.failures.Add(label + ": incorrect boss phase artwork " + textures[i].name);
                    }
                }
                catch (Exception error)
                {
                    result.failures.Add(label + ": " + error.GetBaseException().Message);
                }
            }

            try
            {
                foreach (var enemy in WorldContent.ActiveEnemies)
                {
                    var act = NeutralContent.IsNeutral(enemy.id) ? NeutralContent.ActOf(enemy.id)
                        : Math.Max(1, NeutralContent.ActOfTheme(enemy.theme));
                    var themes = NeutralContent.IsNeutral(enemy.id)
                        ? ActThemes.ForAct(act).Where(t => t != ActThemes.Vault).ToArray()
                        : new[] { enemy.theme ?? ActThemes.Vault };
                    var ids = LegalLineup(enemy);
                    var summoned = enemy.id == FracturedRealmContent.WardFragment ? enemy.id : null;
                    foreach (var theme in themes) Inspect("regular/" + enemy.id + "/" + theme, ids, act, theme, summonedId: summoned);
                    // The playground's act override and a legacy Vault roll must still resolve shared art.
                    Inspect("playground/" + enemy.id, ids, 1,
                        NeutralContent.IsNeutral(enemy.id) ? ActThemes.Vault : enemy.theme ?? ActThemes.Vault, summonedId: summoned);
                    if (enemy.boss && !WorldContent.IsLegacy(enemy))
                        for (var phase = 2; phase <= 3; phase++)
                            Inspect("phase/" + enemy.id + "/" + phase, ids, act, enemy.theme, phase);
                }
                foreach (var encounter in EncounterContent.All.Concat(ThemeRosters.Formations))
                {
                    result.encounters++;
                    var act = Math.Max(1, encounter.minimumAct);
                    var themes = encounter.theme == EncounterContent.Neutral
                        ? ActThemes.ForAct(act).Where(t => t != ActThemes.Vault).ToArray()
                        : new[] { encounter.theme ?? ActThemes.Vault };
                    foreach (var theme in themes) Inspect("formation/" + encounter.id + "/" + theme, encounter.enemies, act, theme);
                }
                // Exposed Parasite art is selected by the creature's own state, rather than a boss phase.
                Inspect("parasite/base", LegalLineup(ThemeRosters.Find(HollowwoodContent.Parasite)), 2, ActThemes.Hollowwood);
                var parasiteCombat = (CombatState)type.GetField("combat", Private).GetValue(menu);
                parasiteCombat.MindAt(0).state = "EXPOSED";
                type.GetMethod("RefreshEnemyArt", Private).Invoke(menu, new object[] { 0 });
                var exposed = ((Texture2D[])type.GetField("finalEnemyTextures", Private).GetValue(menu))[0];
                var exposedVariant=Resources.Load<Texture2D>(GildedArtCatalog.EnemyResource(HollowwoodContent.Parasite+"_phase2"));
                var expected=exposedVariant?exposedVariant:Resources.Load<Texture2D>(GildedArtCatalog.EnemyResource(HollowwoodContent.Parasite));
                if (!exposed || exposed!=expected) result.failures.Add("Parasite exposed artwork missing");
                else if (checkedTextures.Add(exposed)) CheckVisiblePixels(exposed, result.failures);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
            result.distinctTextures = checkedTextures.Count;
            result.failures = result.failures.Distinct().ToList();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/EnemyArtworkVerification.json", JsonUtility.ToJson(result, true));
            var summary = $"[Enemy Artwork Verification] {result.enemies} enemies, {result.encounters} formations, " +
                $"{result.combatSetups} setups, {result.textureChecks} texture checks, {result.distinctTextures} distinct images, {result.failures.Count} failures.";
            if (result.failures.Count == 0) Debug.Log(summary);
            else Debug.LogWarning(summary + "\n" + string.Join("\n", result.failures));
        }

        private static string[] LegalLineup(EnemyDef enemy)
        {
            if (enemy.minion)
            {
                if (enemy.id == FracturedRealmContent.WardFragment) return new[] { FracturedRealmContent.Sovereign };
                var owner = ThemeRosters.AllEnemies.First(e => ThemeRosters.CanOwn(e.id, enemy.id));
                return ThemeRosters.StartingMinions(owner.id).Contains(enemy.id)
                    ? new[] { owner.id } : new[] { owner.id, enemy.id };
            }
            if (enemy.support)
            {
                var ally = ThemeRosters.AllEnemies.First(e => e.theme == enemy.theme && !e.support && !e.minion && !e.boss && !e.elite);
                return new[] { ally.id, enemy.id };
            }
            return new[] { enemy.id };
        }

        private static void CheckVisiblePixels(Texture2D texture, List<string> failures)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(32, 32, 0, RenderTextureFormat.ARGB32);
            var sample = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                sample.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
                sample.Apply();
                var pixels = sample.GetPixels32();
                if (!pixels.Any(p => p.a > 16)) failures.Add("Completely transparent enemy image: " + texture.name);
                if (!pixels.Any(p => p.a < 16)) failures.Add("Enemy image has no transparent cutout: " + texture.name);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(sample);
            }
        }
    }
}
