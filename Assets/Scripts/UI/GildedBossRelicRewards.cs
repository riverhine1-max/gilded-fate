using System;
using GildedFate.Core;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private void ClaimBossRelicChoice(RelicDef relic)
        {
            if(relic==null||acquisitionActive||screen!=ScreenMode.RelicReward||!run.ClaimBossRelic(relic.id))return;
            profile.relicsCollected++;
            ProfileService.Save(profile);SaveService.Save(run);
            BeginRelicAcquisition(relic,Advance);
        }
        private void DrawBossRelicReward(float w,float h)
        {
            if(run.encounterRewards.bossRelicClaimed&&!acquisitionActive&&!run.perfectedSelectionPending){Advance();return;}
            DrawFullBackdrop(rewardBackground,w,h,.66f);DrawRunDock(w);
            Heading(w,"A KEEPER'S LEGACY","CHOOSE ONE BOSS RELIC · THE OTHER TWO REMAIN WITH THE VAULT");
            var offers=run.BossRelicOffers();if(offers.Length==0){Advance();return;}
            var width=Mathf.Min(308,(w-250-(offers.Length-1)*28)/offers.Length);var height=430f;
            var start=(w-(offers.Length*width+(offers.Length-1)*28))*.5f;
            for(var i=0;i<offers.Length;i++)
            {
                var relic=offers[i];var r=new Rect(start+i*(width+28),h*.24f,width,height);
                var hot=r.Contains(PointerPosition)||controllerNavigation&&screenControllerIndex==i;
                Fill(r,new Color(.015f,.018f,.025f,.96f));Outline(r,hot?Gold:new Color(.56f,.44f,.25f),hot?3:1);
                var art=new Rect(r.center.x-76,r.y+19,152,152);DrawRelicArt(art,Array.IndexOf(GameContent.Relics,relic));
                DrawReadableText(new Rect(r.x+16,r.y+180,r.width-32,58),FitReadableText(relic.name,new Rect(0,0,r.width-32,58),21,17,true));
                var rules=new Rect(r.x+23,r.y+247,r.width-46,132);
                DrawReadableText(rules,FitReadableText(FormatCardRules(relic.text),rules,17,14));
                GUI.Label(new Rect(r.x+10,r.yMax-36,r.width-20,24),run.encounterRewards.bossRelicClaimed?"CLAIMED":"CLAIM RELIC",footerStyle);
                if(hot)SetRunHudTooltip(r,relic.name,relic.text);
                if(!acquisitionActive&&!run.encounterRewards.bossRelicClaimed&&GUI.Button(r,"",GUIStyle.none))ClaimBossRelicChoice(relic);
            }
        }
    }
}
