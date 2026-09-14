using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private sealed class CardAttachment
        {
            public string title,detail,resource;
            public int tile,columns,rows;
            public bool binding;
        }
        private readonly Dictionary<CardDef,List<CardAttachment>> cardAttachmentCache=new();
        private int cardAttachmentFrame=-1;
        private List<CardAttachment> CardAttachments(CardDef card)
        {
            if(cardAttachmentFrame!=Time.frameCount){cardAttachmentFrame=Time.frameCount;cardAttachmentCache.Clear();}
            if(cardAttachmentCache.TryGetValue(card,out var known))return known;
            var result=new List<CardAttachment>();cardAttachmentCache[card]=result;
            void Add(string title,string effect,string source,string duration,string resource,int tile,int columns,int rows,bool binding=false)
                =>result.Add(new CardAttachment{title=title,detail=effect+"\nSource: "+source+"\nDuration: "+duration,resource=resource,tile=tile,columns=columns,rows=rows,binding=binding});
            const string relic="Art/Powers/MajorRelicEffects",remaining="Art/Powers/RemainingExpansion";
            if(card.IsModified)
            {
                CardModificationInfo(card,out var title,out var detail);
                Add(title,detail,card.specialModificationKind==SpecialModificationKind.Binding?"Binding":"Fateweave","this run",null,ModificationIconIndex(card),4,4,true);
            }
            if(card.perfected)
            {
                var text=card.kind==CardKind.Power?$"Each play permanently reduces its cost by 1, to a minimum of 0. Current reduction: {card.perfectedCostReduction}.":$"Each play permanently adds +1 {(card.kind==CardKind.Attack?"damage":"Block")}, then Exhausts. Current growth: +{card.perfectedGrowth}.";
                Add("PERFECTED "+card.kind.ToString().ToUpperInvariant(),text,"Perfected Thread","this run",relic,16+(int)card.kind,5,5);
            }
            // Only physical combat copies receive combat-owned attachments. A
            // collection card with the same definition must not inherit them.
            if(screen!=ScreenMode.Combat||combat==null||card.instanceId<=0)return result;
            var m=combat.memory.remaining;var rm=combat.memory.relicExpansion;
            var bound=m?.boundIds.IndexOf(card.instanceId)??-1;
            if(bound>=0)Add("SOULBOUND",combat.CombatCardModification(card),"Soul Infusion","this combat",remaining,10,4,3);
            if(rm?.unwritten.Contains(card.instanceId)==true)Add("UNWRITTEN","Costs 1 less.","Tome of the Unwritten","this combat",relic,1,5,5);
            if(rm?.destined==card.instanceId)Add("DESTINED","Playing this card makes your next card cost 1 less this turn.","Broken Compass","this turn",relic,13,5,5);
            if(rm?.foresightCards.Contains(card.instanceId)==true)Add("FORESIGHT","Costs 1 less.","Foresight","until this card is played",relic,6,5,5);
            if(m?.reflectedIds.Contains(card.instanceId)==true)Add("ECHOED","This card resolves one additional time without another Energy payment.","Fate's Reflection","this turn, until played",relic,15,5,5);
            if(m!=null)
            {
                var authored=GameContent.Find(card.id);var printed=authored==null?card.cost:(card.upgraded?GameContent.Upgrade(authored):authored).cost;
                var pulses=new List<int>();for(var i=0;i<m.imprintCosts.Count;i++)if(m.imprintCosts[i]==printed)pulses.Add(m.imprintDamage[i]);
                if(pulses.Count>0)Add("IMPRINTED COST "+printed,"Playing any card with this printed cost triggers "+string.Join(" + ",pulses)+" damage to random living enemies. Each imprint resolves separately.","Gilded Imprint","this combat",remaining,8,4,3);
                var discounted=m.discountIds.IndexOf(card.instanceId);
                if(discounted>=0&&m.discounts[discounted]>0)Add("COST REDUCTION",$"Costs {m.discounts[discounted]} less this turn.","card effect","this turn",relic,24,5,5);
            }
            return result;
        }
        private static Rect CardAttachmentRect(Rect card,int index)
        {
            var r=CardBindingClaspRect(card);
            r.y+=index*(r.height+card.height*.008f);return r;
        }
        private void DrawCardAttachments(Rect card,CardDef definition)
        {
            var attachments=CardAttachments(definition);var count=Mathf.Min(6,attachments.Count);
            for(var i=0;i<count;i++)
            {
                var attachment=attachments[i];var r=CardAttachmentRect(card,i);
                if(attachment.binding){DrawCardBindingClasp(card,definition);continue;}
                var atlas=LoadAuthoredArt(attachment.resource);if(atlas)DrawAtlasIcon(atlas,attachment.tile,attachment.columns,attachment.rows,r);
                if(i==5&&attachments.Count>6)ShadowLabel(new Rect(r.x,r.yMax-7,r.width,16),"+"+(attachments.Count-5),new GUIStyle(footerStyle){fontSize=11,fontStyle=FontStyle.Bold});
            }
        }
        private bool CardAttachmentHelp(Rect card,CardDef definition,Vector2 point,out string title,out string detail)
        {
            title=detail=null;var attachments=CardAttachments(definition);
            for(var i=0;i<Mathf.Min(6,attachments.Count);i++)
            {
                if(!CardAttachmentRect(card,i).Contains(point))continue;
                var extra=i==5&&attachments.Count>6;
                title=extra?"MORE CARD EFFECTS":attachments[i].title;
                detail=extra?string.Join("\n\n",attachments.Skip(i).Select(a=>a.title+"\n"+a.detail)):attachments[i].detail;
                return true;
            }
            return false;
        }
        private bool AttachmentContains(Rect card,CardDef definition,Vector2 point)
        {
            if(definition==null)return false;
            var count=Mathf.Min(6,CardAttachments(definition).Count);
            for(var i=0;i<count;i++)if(CardAttachmentRect(card,i).Contains(point))return true;
            return false;
        }
    }
}
