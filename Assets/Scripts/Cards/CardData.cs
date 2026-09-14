using System;
using System.Collections.Generic;
using UnityEngine;

namespace GildedFate.Cards
{
    public enum CardCharacter { Neutral, Vanguard, Hexer, Reaper }
    public enum CardRarity { Common, Uncommon, Rare, Special, Curse }
    public enum CardType { Attack, Skill, Power, Curse }

    [CreateAssetMenu(menuName = "Gilded Fate/Card", fileName = "Card_")]
    public sealed class CardData : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;
        public CardCharacter character;
        public CardRarity rarity;
        public CardType type;

        [Header("Presentation")]
        public Sprite artwork;
        public Sprite upgradedArtwork;
        [TextArea(2, 5)] public string description;
        [TextArea(2, 5)] public string upgradedDescription;
        public AudioClip playSound;
        public GameObject playVfx;

        [Header("Rules")]
        [Min(0)] public int energyCost = 1;
        public List<int> baseEffectValues = new();
        public List<int> upgradeEffectValues = new();
        public List<string> keywords = new();

        public string GetDescription(bool upgraded) =>
            upgraded && !string.IsNullOrWhiteSpace(upgradedDescription)
                ? upgradedDescription
                : description;
    }
}
