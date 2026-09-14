using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GildedFate.Core
{
    // Token alignment preserves exact authored text and highlights only changed spans.
    // The renderer opts in per preview; no CardDef is mutated or permanently tinted.
    public static class UpgradeComparison
    {
        public const string Green="83EEA1";
        private static readonly Regex Tokens=new(@"\d+(?:\.\d+)?|[\p{L}]+(?:['’][\p{L}]+)*|[^\s]",RegexOptions.Compiled);
        public static string Highlight(string original,string upgraded,Func<string,string> normal=null)
        {
            original??="";upgraded??="";normal??=s=>s;
            var before=Tokens.Matches(original).Cast<Match>().ToArray();var after=Tokens.Matches(upgraded).Cast<Match>().ToArray();
            var kept=Matching(before,after);var result=new StringBuilder();var at=0;
            for(var i=0;i<after.Length;i++)
            {
                var token=after[i];result.Append(upgraded.Substring(at,token.Index-at));
                var changed=!kept.Contains(i)&&token.Value.Any(char.IsLetterOrDigit);
                result.Append(changed?"<color=#"+Green+"><b>"+token.Value+"</b></color>":normal(token.Value));at=token.Index+token.Length;
            }
            result.Append(upgraded.Substring(at));return result.ToString();
        }
        public static string Removed(string original,string upgraded)
        {
            var a=Tokens.Matches(original??"").Cast<Match>().ToArray();var b=Tokens.Matches(upgraded??"").Cast<Match>().ToArray();
            var kept=Matching(b,a);return string.Join(" ",a.Where((t,i)=>!kept.Contains(i)&&t.Value.Any(char.IsLetter)).Select(t=>t.Value));
        }
        private static HashSet<int> Matching(Match[] before,Match[] after)
        {
            var scores=new int[before.Length+1,after.Length+1];
            for(var i=before.Length-1;i>=0;i--)for(var j=after.Length-1;j>=0;j--)
                scores[i,j]=before[i].Value==after[j].Value?1+scores[i+1,j+1]:Math.Max(scores[i+1,j],scores[i,j+1]);
            var kept=new HashSet<int>();var x=0;var y=0;
            while(x<before.Length&&y<after.Length){if(before[x].Value==after[y].Value){kept.Add(y);x++;y++;}else if(scores[x+1,y]>=scores[x,y+1])x++;else y++;}
            return kept;
        }
    }
}
