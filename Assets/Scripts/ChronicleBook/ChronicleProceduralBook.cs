using GildedFate.Chronicle;
using UnityEngine;

namespace GildedFate.ChronicleBook
{
    /// <summary>Where a rig puts readable text and pictures. Corners are world-space, ordered bottom-left, top-left, top-right, bottom-right,
    /// as seen from the reading camera. Every rig (temporary or finished) provides this so the page content can be laid over it.</summary>
    public interface IChroniclePageSurface
    {
        bool TryGetPageCorners(ChroniclePageSide side, Vector3[] corners);
    }

    /// <summary>
    /// The temporary book: leather covers, gold trim, a four-strand emblem, a page block on each side and one turning sheet,
    /// all Unity primitives. It contains no behaviour of its own; <see cref="ChronicleBookAnimator"/> decides the pose and this
    /// component only applies the resulting frame to transforms. The finished Blender book replaces this class, not the animator.
    /// Page flips never touch the covers: they stay at their fully-open angle while a single sheet turns about the spine.
    /// </summary>
    public sealed class ChronicleProceduralBook : MonoBehaviour, IChronicleBookRig, IChroniclePageSurface
    {
        public const float PageW = 3f, PageH = 4.2f, CoverT = .13f, StackTotal = .34f, SheetT = .014f, MinStack = .02f;
        const float CoverW = PageW + .12f, CoverH = PageH + .22f;

        readonly ChronicleBookAnimator logic = new ChronicleBookAnimator();
        Transform bookRoot, frontPivot, turnPivot, rightStack, leftStack;
        Material leather, gold, paper, tableMat, emerald;
        int layer;

        public ChronicleBookAnimator Logic => logic;
        public ChronicleBookFrame Frame => logic.Frame;
        public float DurationOf(ChronicleBookAction action) => logic.DurationOf(action);
        public void Perform(ChronicleBookAction action, ChronicleNarratorState narrator) { logic.Perform(action, narrator); }
        public void SetPoseImmediate(ChronicleBookPose pose, int spreadIndex) { logic.SetPoseImmediate(pose, spreadIndex); Apply(); }

        /// <summary>Builds the book under <paramref name="parent"/>. Returns null (and logs) if the shader is unavailable, so the game falls back cleanly.</summary>
        public static ChronicleProceduralBook Create(Transform parent, int layer)
        {
            var shader = Resources.Load<Shader>("GildedChronicleBook");
            if (!shader || !shader.isSupported) { Debug.LogWarning("[Chronicle] The book shader is unavailable; the 3D book is disabled."); return null; }
            var host = new GameObject("Chronicle · temporary book") { layer = layer };
            host.transform.SetParent(parent, false);
            var book = host.AddComponent<ChronicleProceduralBook>();
            book.Build(shader, layer); book.Apply();
            return book;
        }

        public void Tick(float dt) { logic.Update(dt); Apply(); }

        static Material Mat(Shader shader, Color color) { var m = new Material(shader) { hideFlags = HideFlags.HideAndDontSave }; m.SetColor("_Color", color); return m; }

        Transform Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material, PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            var collider = go.GetComponent<Collider>(); if (collider) Destroy(collider);
            go.layer = layer; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return go.transform;
        }

        void Build(Shader shader, int layer)
        {
            this.layer = layer;
            leather = Mat(shader, new Color(.07f, .045f, .05f)); gold = Mat(shader, new Color(.95f, .72f, .2f));
            paper = Mat(shader, new Color(.93f, .85f, .68f)); tableMat = Mat(shader, new Color(.035f, .026f, .03f)); emerald = Mat(shader, new Color(.1f, .45f, .3f));

            Part("Table", transform, new Vector3(0, -.12f, 0), new Vector3(60, .2f, 40), tableMat);
            bookRoot = new GameObject("Book") { layer = layer }.transform; bookRoot.SetParent(transform, false);

            Part("Back cover", bookRoot, new Vector3(CoverW * .5f, CoverT * .5f, 0), new Vector3(CoverW, CoverT, CoverH), leather);
            rightStack = Part("Right pages", bookRoot, Vector3.zero, new Vector3(PageW, StackTotal, PageH), paper);
            leftStack = Part("Left pages", bookRoot, Vector3.zero, new Vector3(PageW, MinStack, PageH), paper);
            Part("Spine", bookRoot, new Vector3(-.05f, (StackTotal + 2f * CoverT) * .5f, 0), new Vector3(.1f, StackTotal + 2f * CoverT, CoverH), leather);

            // The front cover hinges on the spine. Everything on its outer face (trim, emblem) rides along.
            frontPivot = new GameObject("Front cover hinge") { layer = layer }.transform; frontPivot.SetParent(bookRoot, false);
            Part("Front cover", frontPivot, new Vector3(CoverW * .5f, 0, 0), new Vector3(CoverW, CoverT, CoverH), leather);
            var y = CoverT * .5f + .007f; const float inset = .14f, bar = .05f;
            Part("Trim top", frontPivot, new Vector3(CoverW * .5f, y, CoverH * .5f - inset), new Vector3(CoverW - 2 * inset, .014f, bar), gold);
            Part("Trim bottom", frontPivot, new Vector3(CoverW * .5f, y, -CoverH * .5f + inset), new Vector3(CoverW - 2 * inset, .014f, bar), gold);
            Part("Trim outer", frontPivot, new Vector3(CoverW - inset, y, 0), new Vector3(bar, .014f, CoverH - 2 * inset), gold);
            Part("Trim spine", frontPivot, new Vector3(inset, y, 0), new Vector3(bar, .014f, CoverH - 2 * inset), gold);
            for (var i = 0; i < 4; i++)   // the four interwoven strands of the Chronicle's emblem
            {
                var strand = Part("Emblem strand " + (i + 1), frontPivot, new Vector3(CoverW * .5f, y + .004f, 0), new Vector3(1.55f, .014f, .045f), gold);
                strand.localRotation = Quaternion.Euler(0, i * 45f, 0);
            }
            Part("Emblem core", frontPivot, new Vector3(CoverW * .5f, y + .008f, 0), new Vector3(.34f, .01f, .34f), emerald, PrimitiveType.Cylinder);

            turnPivot = new GameObject("Turning sheet hinge") { layer = layer }.transform; turnPivot.SetParent(bookRoot, false);
            Part("Turning sheet", turnPivot, new Vector3(PageW * .5f, 0, 0), new Vector3(PageW, SheetT, PageH * .985f), paper);
            turnPivot.gameObject.SetActive(false);
        }

        static float Thick(float fraction) => Mathf.Max(MinStack, StackTotal * fraction);

        void Apply()
        {
            if (!bookRoot) return;
            var f = logic.Frame;
            var open = Mathf.SmoothStep(0f, 1f, f.openAmount);
            const float closedY = CoverT + StackTotal + CoverT * .5f, openY = CoverT * .5f;
            frontPivot.localPosition = new Vector3(0, Mathf.Lerp(closedY, openY, open), 0);
            frontPivot.localRotation = Quaternion.Euler(0, 0, f.coverAngle);

            var rightThick = Thick(f.rightStack); var leftThick = Thick(f.leftStack);
            rightStack.localScale = new Vector3(PageW, rightThick, PageH); rightStack.localPosition = new Vector3(PageW * .5f + .03f, CoverT + rightThick * .5f, 0);
            leftStack.gameObject.SetActive(f.openAmount > .5f);   // a thin endpaper is always there once the book lies open
            leftStack.localScale = new Vector3(PageW, leftThick, PageH); leftStack.localPosition = new Vector3(-PageW * .5f - .03f, CoverT + leftThick * .5f, 0);

            var turning = f.flipDirection != 0;
            turnPivot.gameObject.SetActive(turning);
            if (turning)
            {
                var forward = f.flipDirection > 0; var p = f.flipProgress;
                float rightTop = CoverT + rightThick, leftTop = CoverT + leftThick;
                float fromY = forward ? rightTop : leftTop, toY = forward ? leftTop : rightTop;
                turnPivot.localPosition = new Vector3(0, Mathf.Lerp(fromY, toY, p) + f.flipLift * .35f + SheetT, 0);
                turnPivot.localRotation = Quaternion.Euler(0, 0, forward ? 180f * p : 180f * (1f - p));
            }

            bookRoot.localPosition = new Vector3(Mathf.Lerp(-CoverW * .5f, 0f, open) + f.tremorX, f.bob, f.tremorZ);
            gold.SetColor("_Emission", new Color(.5f, .35f, .06f) * (.05f + f.glow * .45f));
            emerald.SetColor("_Emission", new Color(.1f, .9f, .55f) * (.35f + f.glow * .9f));
        }

        public bool TryGetPageCorners(ChroniclePageSide side, Vector3[] corners)
        {
            if (!bookRoot || corners == null || corners.Length < 4) return false;
            var f = logic.Frame; var left = side == ChroniclePageSide.Left;
            var y = CoverT + Thick(left ? f.leftStack : f.rightStack) + .006f;
            const float inner = .2f, outer = .14f, edge = .26f;
            float x0 = left ? -(PageW - outer) : inner, x1 = left ? -inner : PageW - outer;
            float z0 = -(PageH * .5f - edge), z1 = PageH * .5f - edge;
            corners[0] = bookRoot.TransformPoint(new Vector3(x0, y, z0)); corners[1] = bookRoot.TransformPoint(new Vector3(x0, y, z1));
            corners[2] = bookRoot.TransformPoint(new Vector3(x1, y, z1)); corners[3] = bookRoot.TransformPoint(new Vector3(x1, y, z0));
            return true;
        }

        void OnDestroy() { foreach (var m in new[] { leather, gold, paper, tableMat, emerald }) if (m) Destroy(m); }
    }
}
