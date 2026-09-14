using GildedFate.Audio;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private CardDef inspectionSource,inspectionBase,inspectionUpgrade;
        private bool inspectionShowUpgrade,inspectionComparison;
        private CardDef comparisonBaseRendering;

        private void PrepareCardInspection(CardDef card)
        {
            if(ReferenceEquals(inspectionSource,card))return;
            inspectionSource=card;inspectionShowUpgrade=card.upgraded;inspectionComparison=false;
            inspectionBase=GameContent.InspectionVariant(card,false);
            inspectionUpgrade=GameContent.InspectionVariant(card,true);
        }
        private void SelectInspectionVersion(bool upgraded){inspectionShowUpgrade=upgraded&&GameContent.HasUpgradePreview(inspectionSource);inspectionComparison=inspectionShowUpgrade;}
        private void InspectUpgrade(CardDef card){inspectedCard=card;PrepareCardInspection(card);SelectInspectionVersion(true);}
        private void RegisterInspectionInput(Rect area,CardDef card)
        {
            if(inspectedCard!=null||acquisitionActive||ShardDiscoveryOpen||card==null)return;
            if(Event.current.type==EventType.MouseDown&&Event.current.button==1&&area.Contains(PointerPosition)){InspectUpgrade(card);Event.current.Use();}
        }
        private void DrawInspectableCardModal(float w,float h,CardDef card)
        {
            PrepareCardInspection(card);var wasEnabled=GUI.enabled;GUI.enabled=true;
            var preview=inspectionShowUpgrade?inspectionUpgrade:inspectionBase;
            Fill(new Rect(0,0,w,h),new Color(0,0,0,.84f));
            var height=Mathf.Min(590,h-220);var width=height*.70f;var r=new Rect(w*.5f-width*.5f,65,width,height);
            comparisonBaseRendering=inspectionComparison?inspectionBase:null;
            try{DrawCard(r,preview);}finally{comparisonBaseRendering=null;}RegisterCardKeywordHelp(r,preview);
            if(inspectionComparison)
            {
                var removed=UpgradeComparison.Removed(inspectionBase.text,inspectionUpgrade.text);
                var details="GREEN · UPGRADE CHANGES";
                if(inspectionBase.cost!=inspectionUpgrade.cost)details+="\n\nEnergy: "+inspectionBase.cost+" → <color=#83EEA1>"+inspectionUpgrade.cost+"</color>";
                if(removed.Length>0)details+="\n\nRemoved:\n<color=#83EEA1>"+removed+"</color>";
                var side=new Rect(Mathf.Max(20,r.x-260),r.y+80,235,220);
                GUI.Label(side,details,new GUIStyle(footerStyle){fontSize=18,richText=true,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=new Color(.94f,.91f,.82f)}});
            }
            var meta=preview.origin.ToString().ToUpperInvariant()+"  ·  "+preview.rarity.ToString().ToUpperInvariant()+(preview.IsModified?"  ·  "+preview.specialModification.ToUpperInvariant():"");
            GUI.Label(new Rect(w*.5f-310,r.yMax+9,620,22),meta,new GUIStyle(footerStyle){fontSize=12});
            var normal=new Rect(w*.5f-156,r.yMax+37,150,36);var upgraded=new Rect(w*.5f+6,r.yMax+37,150,36);
            if(GameContent.HasUpgradePreview(card))
            {
                DrawButtonFrame(normal,normal.Contains(PointerPosition),!inspectionShowUpgrade);
                DrawButtonFrame(upgraded,upgraded.Contains(PointerPosition),inspectionShowUpgrade);
                if(GUI.Button(normal,"NORMAL",buttonStyle)){SelectInspectionVersion(false);Sfx(SoundCue.UiHover);}
                var upgradeStyle=new GUIStyle(buttonStyle);upgradeStyle.normal.textColor=new Color(.54f,1f,.65f);
                if(GUI.Button(upgraded,"UPGRADE PREVIEW",new GUIStyle(upgradeStyle){fontSize=12})){SelectInspectionVersion(true);Sfx(SoundCue.UiHover);}
            }
            else GUI.Label(new Rect(w*.5f-200,r.yMax+37,400,36),"THIS CARD HAS NO UPGRADE",footerStyle);
            var close=new Rect(w*.5f-115,r.yMax+85,230,38);DrawButtonFrame(close,close.Contains(PointerPosition),false);
            if(GUI.Button(close,"BACK TO CARDS",buttonStyle)){Sfx(SoundCue.UiHover);inspectedCard=inspectionSource=null;}
            GUI.enabled=wasEnabled;
        }
    }
}
