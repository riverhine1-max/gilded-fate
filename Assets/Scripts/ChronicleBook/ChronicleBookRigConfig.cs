using GildedFate.Chronicle;
using UnityEngine;

namespace GildedFate.ChronicleBook
{
    /// <summary>
    /// Maps the finished book model's animation setup onto the Chronicle. Create one with
    /// Assets > Create > Gilded Fate > Chronicle Book Rig Config, save it as Resources/Chronicle/ChronicleBookRigConfig,
    /// and put the model prefab at Resources/Chronicle/ChronicleBook. No code changes are needed when the clip names differ:
    /// change the names here. If a clip is assigned, its length sets that action's duration.
    /// </summary>
    [CreateAssetMenu(menuName = "Gilded Fate/Chronicle Book Rig Config", fileName = "ChronicleBookRigConfig")]
    public sealed class ChronicleBookRigConfig : ScriptableObject
    {
        [Header("Animator state names (as they appear in the model's Animator Controller)")]
        public string openState = "Open";
        public string closeState = "Close";
        public string turnForwardState = "TurnForward";
        public string turnBackState = "TurnBack";
        public string idleState = "Idle";
        public string correctionState = "ReactCorrection";
        public string majorMagicState = "ReactMajor";
        public int animatorLayer;
        public float crossFadeSeconds = .05f;
        [Tooltip("Optional Animator float set to the current spread index, for page-stack blend trees. Leave empty if unused.")]
        public string spreadParameter = "";

        [Header("Durations in seconds (a clip, if assigned, overrides its number)")]
        public AnimationClip openClip, closeClip, turnForwardClip, turnBackClip, correctionClip, majorMagicClip;
        public float openSeconds = 1.6f, closeSeconds = 1.3f, turnSeconds = .9f, correctionSeconds = .6f, majorMagicSeconds = 1.1f;

        [Header("Readable page areas: names of child transforms in the prefab. +X = toward the page's right, +Y = out of the paper, +Z = toward the top of the page.")]
        public string leftPageAnchorName = "PageAnchor_Left";
        public string rightPageAnchorName = "PageAnchor_Right";
        public Vector2 pageSize = new Vector2(3f, 4.2f);

        [Header("Reading camera")]
        public float cameraDistance = 11.5f;
        public Vector3 cameraTarget = new Vector3(0f, .2f, 0f);

        public ChronicleBookTimings Timings() => new ChronicleBookTimings
        {
            open = openClip ? openClip.length : openSeconds, close = closeClip ? closeClip.length : closeSeconds,
            turn = turnForwardClip ? turnForwardClip.length : turnSeconds, react = correctionClip ? correctionClip.length : correctionSeconds,
            major = majorMagicClip ? majorMagicClip.length : majorMagicSeconds
        };
    }
}
