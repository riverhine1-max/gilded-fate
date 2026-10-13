namespace GildedFate.UI
{
    // Source seconds measured from the existing MP4s: sword/scythe contact,
    // magic release, or a fully raised guard. These are not audio timestamps.
    public static class HeroActionTiming
    {
        public static float PlaybackSeconds(string clip)=>clip=="FirstRitual"?1.35f:
            clip=="HeavyAttack"||clip=="MajorFinisherAttack"||clip=="FinisherAttack"?1.2f:.95f;
        public static float PoseSeconds(string hero,string clip)=>(hero+":"+clip) switch
        {
            "Vanguard:BasicAttack"=>1.3f,"Vanguard:AlternateAttack"=>1.4f,
            "Vanguard:HeavyAttack"=>1.85f,"Vanguard:MajorFinisherAttack"=>1.7f,
            "Vanguard:Block"=>.85f,"Vanguard:MajorDefense"=>1.1f,
            "Hexer:BasicMagicalAttack"=>1.8f,"Hexer:AlternateMagicalAttack"=>1.4f,
            "Hexer:PowerfulSigilAttack"=>1.7f,"Hexer:FinisherAttack"=>1.8f,
            "Hexer:BasicDefensiveSkill"=>.85f,"Hexer:MajorDefensiveSkill"=>.9f,
            "Reaper:BasicScytheAttack"=>1.3f,"Reaper:AlternateScytheAttack"=>1.2f,
            "Reaper:HeavyAttack"=>1.8f,"Reaper:MajorFinisherAttack"=>1.9f,
            "Reaper:DefensiveAnimation"=>1.15f,
            _=>0f
        };
    }
}
