using System.Collections.Generic;
using GildedFate.Combat;
using UnityEngine;

namespace GildedFate.UI
{
    // Prompt 12B: the enemy icon set (Art/UI/EnemyIcons/{Intents,Mechanics,Statuses}). The icons are supporting
    // marks drawn beside text; numbers, counters and move names are never replaced by them. Which icon belongs
    // where is decided by EnemyIconRules so real runs, the Playground and the tests agree.
    public sealed partial class GildedMainMenu
    {
        private readonly Dictionary<string,Texture2D> enemyIconArt=new();
        private bool pgIconLegend;

        private Texture2D EnemyIconArt(string icon)
        {
            if(string.IsNullOrEmpty(icon))return null;
            if(enemyIconArt.TryGetValue(icon,out var t))return t;
            t=Resources.Load<Texture2D>(EnemyIconRules.ResourcePath(icon));
            if(t)t.wrapMode=TextureWrapMode.Clamp;
            enemyIconArt[icon]=t;return t;
        }
        // The signature attacks are drawn a little larger than ordinary intent icons.
        private static bool IntentIconProminent(string icon)=>icon==EnemyIconRules.WorldBreak||icon==EnemyIconRules.ThronebreakerCharge||icon==EnemyIconRules.Eruption;
        // Draws an icon inside `box` without stretching it (transparent background is preserved).
        private bool DrawEnemyIconFit(Rect box,string icon,bool prominent)
        {
            var tex=EnemyIconArt(icon);if(!tex)return false;
            var grow=prominent?box.width*.1f:0f;
            var inner=prominent?new Rect(box.x-grow,box.y-grow,box.width+grow*2,box.height+grow*2):new Rect(box.x+box.width*.08f,box.y+box.height*.06f,box.width*.84f,box.height*.88f);
            if(prominent&&!profile.reduceMotion){var pulse=1f+.04f*Mathf.Sin(Time.unscaledTime*4f);inner=new Rect(inner.center.x-inner.width*pulse*.5f,inner.center.y-inner.height*pulse*.5f,inner.width*pulse,inner.height*pulse);}
            GUI.DrawTexture(inner,tex,ScaleMode.ScaleToFit,true);
            return true;
        }

        private static readonly string[] LegendNames={"Command","Auction Lot","Repeat","Thronebreaker Charge","World Break","Eruption","Bonus Gold","Reserve","Growth","Prediction","Orbit Plate","Momentum","Judgment","Sentence","Toll","Echo","Split","Fracture","Dual Possibility","Royal Order","Siege","World Break Charge","Burrow Warning","Form Change","Prepared Attack","Seized Gold"};
        // Playground / debug: every enemy icon with its name and what it means.
        private void DrawEnemyIconLegend()
        {
            if(!playgroundActive||!pgIconLegend||screen!=ScreenMode.Combat)return;
            var icons=new List<string>();icons.AddRange(EnemyIconRules.Intents);icons.AddRange(EnemyIconRules.Mechanics);icons.AddRange(EnemyIconRules.Statuses);
            const int rows=13;const float cell=30f,colW=300f;
            var panel=new Rect(64,94,colW*2+16,rows*cell+34);
            Fill(panel,new Color(.03f,.02f,.04f,.94f));Outline(panel,new Color(.72f,.62f,1f,.9f),1);
            GUI.Label(new Rect(panel.x+8,panel.y+4,panel.width-16,20),"ENEMY ICON LEGEND · hover for meaning",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=12,fontStyle=FontStyle.Bold,normal={textColor=new Color(.84f,.8f,1f)}});
            for(var i=0;i<icons.Count;i++)
            {
                var col=i/rows;var row=i%rows;var r=new Rect(panel.x+8+col*(colW+0),panel.y+28+row*cell,colW-8,cell-2);
                var hot=r.Contains(combatPointer);if(hot)Fill(r,new Color(1,1,1,.07f));
                DrawEnemyIconFit(new Rect(r.x+2,r.y+1,cell-4,cell-4),icons[i],false);
                var name=i<LegendNames.Length?LegendNames[i]:icons[i];
                GUI.Label(new Rect(r.x+cell+4,r.y,r.width-cell-4,r.height),name,new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=13,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.95f,.92f,.84f)}});
                if(hot)SetCombatEffectTooltip(name.ToUpperInvariant(),EnemyIconRules.Tip(icons[i]),r.center);
            }
        }
    }
}
