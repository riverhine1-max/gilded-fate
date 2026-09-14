using System.Text.RegularExpressions;

namespace GildedFate.Core
{
    // Presentation-only terminology. Serialized enum names, content identifiers,
    // rule matching and resource paths deliberately retain their stable names.
    public static class GameplayTerms
    {
        private static readonly Regex LegacyTerms=new(@"\b(Powers?|Exhaust(?:s|ed|ing)?)\b",RegexOptions.IgnoreCase|RegexOptions.Compiled);
        public static string Display(string text)=>string.IsNullOrEmpty(text)?text:LegacyTerms.Replace(text,m=>
        {
            var lower=m.Value.ToLowerInvariant();var word=lower switch{"power"=>"aspect","powers"=>"aspects","exhausts"=>"dissipates","exhausted"=>"dissipated","exhausting"=>"dissipating",_=>"dissipate"};
            return m.Value==m.Value.ToUpperInvariant()?word.ToUpperInvariant():char.IsUpper(m.Value[0])?char.ToUpperInvariant(word[0])+word.Substring(1):word;
        });
    }
}
