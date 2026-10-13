using GildedFate.Chronicle;
using UnityEngine;

namespace GildedFate.ChronicleBook
{
    /// <summary>
    /// Drives an imported, animated Chronicle model. The pose, timing and overlay visibility come from the same
    /// <see cref="ChronicleBookAnimator"/> the temporary book uses, so story playback behaves identically; this class only
    /// also fires the model's Animator states at the right moments. A state name that does not exist is reported once, not thrown.
    /// </summary>
    public sealed class ChronicleAnimatorBookRig : MonoBehaviour, IChronicleBookRig, IChroniclePageSurface, IChronicleBookTempo
    {
        ChronicleBookRigConfig config; Animator animator; ChronicleBookAnimator logic;
        Transform leftAnchor, rightAnchor;
        Renderer turningLeaf;
        SkinnedMeshRenderer leftPaper, rightPaper;
        Mesh paperMesh;
        bool turning;
        readonly System.Collections.Generic.HashSet<string> reported = new System.Collections.Generic.HashSet<string>();

        public ChronicleBookAnimator Logic => logic;
        public ChronicleBookRigConfig Config => config;
        public ChronicleBookFrame Frame => logic.Frame;
        /// <summary>Multi-page journeys: the logic and the existing turn clips play faster together (clips and rig are unchanged).</summary>
        public float Tempo { get => logic.Tempo; set => logic.Tempo = value; }
        public float DurationOf(ChronicleBookAction action) => logic.DurationOf(action);

        /// <summary>Loads the finished book if one has been added. Returns null when there is no model yet, so the temporary book is used.</summary>
        public static ChronicleAnimatorBookRig TryCreate(Transform parent, int layer)
        {
            var prefab = Resources.Load<GameObject>("Chronicle/ChronicleBook");
            if (!prefab) return null;
            var config = Resources.Load<ChronicleBookRigConfig>("Chronicle/ChronicleBookRigConfig");
            if (!config) { config = ScriptableObject.CreateInstance<ChronicleBookRigConfig>(); Debug.LogWarning("[Chronicle] No ChronicleBookRigConfig found; using default state names."); }
            var instance = Instantiate(prefab, parent, false); instance.name = "Chronicle · finished book";
            SetLayer(instance.transform, layer);
            var rig = instance.AddComponent<ChronicleAnimatorBookRig>();
            rig.Setup(config);
            return rig;
        }

        static void SetLayer(Transform t, int layer) { t.gameObject.layer = layer; for (var i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer); }

        static Transform FindDeep(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++) { var found = FindDeep(root.GetChild(i), name); if (found) return found; }
            return null;
        }

        void Setup(ChronicleBookRigConfig cfg)
        {
            config = cfg; logic = new ChronicleBookAnimator(cfg.Timings());
            animator = GetComponentInChildren<Animator>();
            if (animator)
            {
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.applyRootMotion = false;
                animator.keepAnimatorStateOnDisable = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                // Initialize bindings before the first immediate pose or page measurement.
                animator.Rebind();
                animator.Update(0f);
                animator.enabled = false; // the stage owns the unscaled clock and renders immediately after Tick
            }
            else Debug.LogWarning("[Chronicle] The book prefab has no Animator; it will not move.");
            leftAnchor = FindDeep(transform, cfg.leftPageAnchorName); rightAnchor = FindDeep(transform, cfg.rightPageAnchorName);
            var leftSurface = FindDeep(transform, "PageSurface_Left");
            var rightSurface = FindDeep(transform, "PageSurface_Right");
            leftPaper = leftSurface ? leftSurface.GetComponent<SkinnedMeshRenderer>() : null;
            rightPaper = rightSurface ? rightSurface.GetComponent<SkinnedMeshRenderer>() : null;
            if (leftPaper || rightPaper) paperMesh = new Mesh { name = "Chronicle page bounds" };
            LevelOpenSpread();
            var leaf = FindDeep(transform, "TurningPage_01");
            turningLeaf = leaf ? leaf.GetComponent<Renderer>() : null;
            if (turningLeaf) turningLeaf.enabled = false;
            // The imported bookmark obscures the gutter as a long green bar.
            // Hide only its renderer; the cover, gems, bones and pages stay intact.
            var bookmark=FindDeep(transform,"Bookmark_Ribbon");
            if(bookmark && bookmark.TryGetComponent<Renderer>(out var ribbon))ribbon.enabled=false;
            if (!leftAnchor || !rightAnchor) Debug.LogWarning("[Chronicle] Page anchors '" + cfg.leftPageAnchorName + "' / '" + cfg.rightPageAnchorName + "' were not found; page text cannot be placed.");
        }

        // Level the animated paper, rather than a normal from the FBX's bind pose.
        // The imported armature changes the paper angle when its open animation is evaluated.
        void LevelOpenSpread()
        {
            if (!animator || !paperMesh) return;
            SetPoseImmediate(ChronicleBookPose.Open, 0);
            if (TryGetReadingNormal(out var normal))
                transform.rotation = Quaternion.FromToRotation(normal, Vector3.up) * transform.rotation;
            SetPoseImmediate(ChronicleBookPose.Closed, 0);
        }

        public bool TryGetReadingNormal(out Vector3 normal)
        {
            normal = Vector3.zero;
            foreach (var paper in new[] { leftPaper, rightPaper })
            {
                if (!paper || !paperMesh) continue;
                paper.BakeMesh(paperMesh);
                var vertices = paperMesh.vertices;
                var triangles = paperMesh.GetTriangles(0);
                var largest = Vector3.zero;
                for (var i = 0; i + 2 < triangles.Length; i += 3)
                {
                    var a = vertices[triangles[i]];
                    var b = vertices[triangles[i + 1]];
                    var c = vertices[triangles[i + 2]];
                    var cross = Vector3.Cross(paper.transform.TransformVector(b - a), paper.transform.TransformVector(c - a));
                    if (cross.sqrMagnitude > largest.sqrMagnitude) largest = cross;
                }
                if (largest.y < 0f) largest = -largest;
                normal += largest.normalized;
            }
            normal.Normalize(); return normal.sqrMagnitude > .5f;
        }

        public void Tick(float dt)
        {
            logic.Update(dt);
            if (turning && logic.Frame.flipDirection == 0)
            {
                // Hide before resetting this reusable leaf from the left stack to the right.
                turning = false;
                if (turningLeaf) turningLeaf.enabled = false;
                if (animator) animator.speed = 1f;
                Play(config.idleState);
            }
            if (animator) animator.Update(dt);
        }

        public void Perform(ChronicleBookAction action, ChronicleNarratorState narrator)
        {
            logic.Perform(action, narrator);
            if (!animator || config == null) return;
            turning = action == ChronicleBookAction.TurnPageForward || action == ChronicleBookAction.TurnPageBack;
            if (turningLeaf) turningLeaf.enabled = turning;
            animator.speed = turning ? logic.Tempo : 1f;   // the turn clip keeps pace with the logic's sheet
            switch (action)
            {
                case ChronicleBookAction.Open: Play(config.openState); break;
                case ChronicleBookAction.Close: Play(config.closeState); break;
                case ChronicleBookAction.TurnPageForward: SetSpread(); Play(config.turnForwardState); break;
                case ChronicleBookAction.TurnPageBack: SetSpread(); Play(config.turnBackState); break;
                case ChronicleBookAction.Idle: Play(config.idleState); break;
                case ChronicleBookAction.ReactToCorrection: Play(config.correctionState); break;
                case ChronicleBookAction.MajorMagic: Play(config.majorMagicState); break;
            }
        }

        public void SetPoseImmediate(ChronicleBookPose pose, int spreadIndex)
        {
            logic.SetPoseImmediate(pose, spreadIndex);
            turning = false;
            if (turningLeaf) turningLeaf.enabled = false;
            if (!animator || config == null) return;
            animator.speed = 1f;
            SetSpread();
            // Jump to the last frame of the matching state: open at its end is the open pose, close at its end is the closed pose.
            var state = pose == ChronicleBookPose.Open ? config.openState : config.closeState;
            var hash = Animator.StringToHash(state);
            if (animator.HasState(config.animatorLayer, hash)) { animator.Play(hash, config.animatorLayer, 1f); animator.Update(0f); }
            else Report(state);
        }

        void SetSpread() { if (!string.IsNullOrEmpty(config.spreadParameter)) animator.SetFloat(config.spreadParameter, logic.Frame.spreadIndex); }

        void Play(string state)
        {
            if (string.IsNullOrEmpty(state)) return;
            var hash = Animator.StringToHash(state);
            if (!animator.HasState(config.animatorLayer, hash)) { Report(state); return; }
            if (config.crossFadeSeconds > 0f && !turning) animator.CrossFadeInFixedTime(hash, config.crossFadeSeconds, config.animatorLayer, 0f);
            else animator.Play(hash, config.animatorLayer, 0f);
            animator.Update(0f);
        }

        void Report(string state) { if (reported.Add(state)) Debug.LogWarning("[Chronicle] The book's Animator has no state named '" + state + "'. Map it in the ChronicleBookRigConfig."); }

        void OnDestroy()
        {
            if (!paperMesh) return;
            if (Application.isPlaying) Destroy(paperMesh); else DestroyImmediate(paperMesh);
        }

        public bool TryGetPageCorners(ChroniclePageSide side, Vector3[] corners)
        {
            if (corners == null || corners.Length < 4) return false;
            var paper = side == ChroniclePageSide.Left ? leftPaper : rightPaper;
            if (paper && paperMesh)
            {
                // Read the animated paper itself: anchor sizes can differ from the imported mesh.
                paper.BakeMesh(paperMesh);
                var bounds = paperMesh.bounds;
                var thin = bounds.size.x <= bounds.size.y && bounds.size.x <= bounds.size.z ? 0
                    : bounds.size.y <= bounds.size.z ? 1 : 2;
                var a = (thin + 1) % 3; var b = (thin + 2) % 3;
                var lo = bounds.min; var hi = bounds.max;
                var insetA = bounds.size[a] * .10f; var insetB = bounds.size[b] * .10f;
                lo[a] += insetA; hi[a] -= insetA; lo[b] += insetB; hi[b] -= insetB;
                var point = bounds.center;
                point[a] = lo[a]; point[b] = lo[b]; corners[0] = paper.transform.TransformPoint(point);
                point[b] = hi[b]; corners[1] = paper.transform.TransformPoint(point);
                point[a] = hi[a]; corners[2] = paper.transform.TransformPoint(point);
                point[b] = lo[b]; corners[3] = paper.transform.TransformPoint(point);
                return true;
            }
            var anchor = side == ChroniclePageSide.Left ? leftAnchor : rightAnchor;
            if (!anchor) return false;
            var hx = config.pageSize.x * .5f; var hz = config.pageSize.y * .5f;
            corners[0] = anchor.TransformPoint(new Vector3(-hx, 0, -hz)); corners[1] = anchor.TransformPoint(new Vector3(-hx, 0, hz));
            corners[2] = anchor.TransformPoint(new Vector3(hx, 0, hz)); corners[3] = anchor.TransformPoint(new Vector3(hx, 0, -hz));
            return true;
        }
    }
}
