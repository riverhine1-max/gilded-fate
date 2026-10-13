using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GildedFate.Core;
using GildedFate.Saving;
using GildedFate.UI;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    // Font measurements must run inside OnGUI, using the game's actual fitting code.
    public sealed class GildedCleanupTypography : EditorWindow
    {
        [Serializable] sealed class Report { public int checks,failures;public List<string> errors=new(); }
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        bool started;
        public static void Begin()
        {
            var window=CreateInstance<GildedCleanupTypography>();window.titleContent=new GUIContent("Card text verification");
            window.position=new Rect(300,250,420,140);window.ShowUtility();
        }
        void OnGUI()
        {
            GUILayout.Label("Checking card wording at all game layouts…");
            if(started||Event.current.type!=EventType.Repaint)return;started=true;
            var report=new Report();var host=new GameObject("Card text verification"){hideFlags=HideFlags.HideAndDontSave};host.SetActive(false);
            try
            {
                var menu=host.AddComponent<GildedMainMenu>();var type=typeof(GildedMainMenu);
                void Set(string field,object value)=>type.GetField(field,Private).SetValue(menu,value);
                Set("bodyFont",Resources.Load<Font>("Fonts/SourceSans3-Regular"));
                Set("labelFont",Resources.Load<Font>("Fonts/SourceSans3-Semibold"));
                Set("headingFont",Resources.Load<Font>("Fonts/SourceSerif4Display-Semibold"));
                type.GetMethod("BuildStyles",Private).Invoke(menu,null);
                foreach(var large in new[]{false,true})
                {
                    Set("profile",new PlayerProfile{largeCardText=large});Set("typographyFailures",0);
                    type.GetMethod("AuditCardTypography",Private).Invoke(menu,null);
                    report.failures+=(int)type.GetField("typographyFailures",Private).GetValue(menu);
                    report.checks+=GameContent.Cards.Sum(c=>GameContent.HasUpgradePreview(c)?2:1)*5*3;
                }
            }
            catch(Exception error){report.failures++;report.errors.Add(error.GetBaseException().Message);}
            finally{UnityEngine.Object.DestroyImmediate(host);}
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CleanupTypographyVerification.json",JsonUtility.ToJson(report,true));
            EditorApplication.delayCall+=Close;
        }
    }
}
