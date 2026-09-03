using Spine2_1_25;
using System;
using System.Collections.Generic;

namespace GflChibiDesktop
{
    /// <summary>
    /// 画布尺寸（未缩放基础值：x/y = 模型在画布中的偏移，w/h = 画布尺寸）。
    /// </summary>
    public struct CanvasRect
    {
        public float X;
        public float Y;
        public float W;
        public float H;

        public CanvasRect(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }
    }

    /// <summary>
    /// 动态画布统计计划：剔除离散帧/离群动画后得到"常规画布"，
    /// 仅在播放"严重超出常规画布"的动画时临时切换其全程画布。
    /// </summary>
    public sealed class DynamicCanvasPlan
    {
        /// <summary>常驻画布（未缩放）：非离群动画剔除离散帧后的并集范围。</summary>
        public CanvasRect Base;
        /// <summary>各动画全程包围盒画布（未缩放，含离散帧）——离群动画播放时用它保证完整可见。</summary>
        public Dictionary<string, CanvasRect> FullByAnimation = new Dictionary<string, CanvasRect>();
        /// <summary>需要切换画布的动画（常规范围严重超出常驻画布所属的常规动画）。</summary>
        public HashSet<string> OversizeAnimations = new HashSet<string>();
    }

    /// <summary>
    /// 画布尺寸统计计算（legacy 动态模式）：
    /// 1) 对每个动画逐帧采样，丢弃尺寸超过该动画 P98 分位的离散帧（个别帧飞出画布不撑大画布）；
    /// 2) 动画级剔除：常规尺寸显著大于中位数（&gt;中位×3，至少 3 个动画时）的动画视为"严重超出"，
    ///    不参与常驻画布；常驻画布 = 其余动画常规范围的并集；
    /// 3) 运行时仅当切到"严重超出"的动画时，临时切换为其全程包围盒画布。
    /// </summary>
    internal static class CanvasCalculator
    {
        /// <summary>动画采样率（Hz），与 calcWindowSize.lua 的 240 一致。</summary>
        private const float SampleRate = 240f;
        /// <summary>单动画最大采样步数（240Hz 下约 50 秒）：防止超长/病态动画导致卡顿。</summary>
        private const int MaxSteps = 12000;
        /// <summary>帧级离散剔除分位：保留占全部帧 P98 以内的常规帧。</summary>
        private const double FrameQuantile = 0.98;
        /// <summary>动画级离群阈值：常规尺寸超过全部动画常规尺寸中位数的倍数视为"严重超出"。</summary>
        private const double AnimeOversizeFactor = 3.0;
        /// <summary>少于该动画数不做动画级离群剔除（小集合直接取全部常规并集）。</summary>
        private const int MinAnimesForOversize = 3;

        /// <summary>单动画逐帧采样样本。</summary>
        private sealed class AnimSamples
        {
            public string Name;
            public readonly List<float> MinX = new List<float>();
            public readonly List<float> MinY = new List<float>();
            public readonly List<float> MaxX = new List<float>();
            public readonly List<float> MaxY = new List<float>();
            public float FullMinX = float.MaxValue, FullMinY = float.MaxValue, FullMaxX = float.MinValue, FullMaxY = float.MinValue;
            public float RegMinX = float.MaxValue, RegMinY = float.MaxValue, RegMaxX = float.MinValue, RegMaxY = float.MinValue;
        }

        /// <summary>统计动态画布计划。采样发生在当前缩放下的骨架，返回结果除以缩放（未缩放）。</summary>
        public static DynamicCanvasPlan ComputeDynamicPlan(AnimationState state, AnimationStateData stateData,
            Skeleton skeleton, float scale)
        {
            var plan = new DynamicCanvasPlan();
            List<AnimSamples> all = SampleAllAnimations(state, stateData, skeleton);

            // 各动画常规范围（剔除离散帧）与离群判定
            var regDiags = new List<float>(all.Count);
            foreach (AnimSamples s in all)
            {
                s.RegMinX = s.RegMinY = float.MaxValue;
                s.RegMaxX = s.RegMaxY = float.MinValue;
                ComputeRegularBounds(s);
                float regW = s.RegMaxX - s.RegMinX;
                float regH = s.RegMaxY - s.RegMinY;
                regDiags.Add((float)Math.Sqrt(regW * (double)regW + regH * (double)regH));
            }
            bool doOversize = all.Count >= MinAnimesForOversize;
            float medianDiag = 0;
            if (doOversize)
            {
                var sorted = new List<float>(regDiags);
                sorted.Sort();
                medianDiag = sorted[sorted.Count / 2];
            }

            // 常驻画布 = 非离群动画常规范围并集
            float bMinX = float.MaxValue, bMinY = float.MaxValue, bMaxX = float.MinValue, bMaxY = float.MinValue;
            bool anyRegular = false;
            for (int i = 0; i < all.Count; i++)
            {
                AnimSamples s = all[i];
                bool oversize = doOversize && medianDiag > 0 && regDiags[i] > medianDiag * AnimeOversizeFactor;
                if (oversize)
                {
                    plan.OversizeAnimations.Add(s.Name);
                }
                else
                {
                    anyRegular = true;
                    if (s.RegMinX < bMinX) bMinX = s.RegMinX;
                    if (s.RegMinY < bMinY) bMinY = s.RegMinY;
                    if (s.RegMaxX > bMaxX) bMaxX = s.RegMaxX;
                    if (s.RegMaxY > bMaxY) bMaxY = s.RegMaxY;
                }
                plan.FullByAnimation[s.Name] = RectFromBounds(s.FullMinX, s.FullMinY, s.FullMaxX, s.FullMaxY);
            }
            if (!anyRegular)
            {
                // 全部动画都离群（病态）：退化为全部常规并集
                bMinX = bMinY = float.MaxValue;
                bMaxX = bMaxY = float.MinValue;
                foreach (AnimSamples s in all)
                {
                    if (s.RegMinX < bMinX) bMinX = s.RegMinX;
                    if (s.RegMinY < bMinY) bMinY = s.RegMinY;
                    if (s.RegMaxX > bMaxX) bMaxX = s.RegMaxX;
                    if (s.RegMaxY > bMaxY) bMaxY = s.RegMaxY;
                }
                plan.OversizeAnimations.Clear();
            }
            plan.Base = RectFromBounds(bMinX, bMinY, bMaxX, bMaxY);

            // 除以当前缩放，得到未缩放结果
            if (scale > 0)
            {
                float inv = 1f / scale;
                CanvasRect b = plan.Base;
                plan.Base = new CanvasRect(b.X * inv, b.Y * inv, b.W * inv, b.H * inv);
                var keys = new List<string>(plan.FullByAnimation.Keys);
                foreach (string k in keys)
                {
                    CanvasRect r = plan.FullByAnimation[k];
                    plan.FullByAnimation[k] = new CanvasRect(r.X * inv, r.Y * inv, r.W * inv, r.H * inv);
                }
            }

            // 画布最小尺寸：动态画布算出的宽/高小于 448 时按 448（与固定"小"画布的下限一致），
            // 避免小模型把窗口撑得过小；偏移 X/Y 保持不变（模型原点相对窗口位置不变）。
            const float MinCanvasSize = 448f;
            CanvasRect bb = plan.Base;
            plan.Base = new CanvasRect(bb.X, bb.Y,
                Math.Max(bb.W, MinCanvasSize), Math.Max(bb.H, MinCanvasSize));
            var fKeys = new List<string>(plan.FullByAnimation.Keys);
            foreach (string k in fKeys)
            {
                CanvasRect r = plan.FullByAnimation[k];
                plan.FullByAnimation[k] = new CanvasRect(r.X, r.Y,
                    Math.Max(r.W, MinCanvasSize), Math.Max(r.H, MinCanvasSize));
            }
            return plan;
        }

        /// <summary>对每个动画逐帧采样，记录每帧包围盒与全程并集。</summary>
        private static List<AnimSamples> SampleAllAnimations(AnimationState state, AnimationStateData stateData, Skeleton skeleton)
        {
            float savedMix = stateData.DefaultMix;
            stateData.DefaultMix = 0;   // 采样时不交叉过渡（与 lua 一致）

            float[] regionBuf = new float[8];
            float[] meshBuf = Array.Empty<float>();
            var result = new List<AnimSamples>();
            try
            {
                List<Animation> animations = skeleton.Data.Animations;
                if (animations != null && animations.Count > 0)
                {
                    foreach (Animation anim in animations)
                    {
                        skeleton.SetToSetupPose();   // 独立采样前复位姿势，保证范围准确
                        TrackEntry entry = state.SetAnimation(0, anim, false);
                        float dur = entry.EndTime;
                        if (float.IsNaN(dur) || dur < 0) dur = 0;
                        int steps = (int)Math.Min(Math.Ceiling(dur * SampleRate) + 2, MaxSteps);
                        var s = new AnimSamples { Name = anim.Name };
                        for (int k = 0; k < steps; k++)
                        {
                            state.Update(1f / SampleRate);
                            state.Apply(skeleton);
                            skeleton.UpdateWorldTransform();
                            float mnX = float.MaxValue, mnY = float.MaxValue, mxX = float.MinValue, mxY = float.MinValue;
                            AccumulateBounds(skeleton, regionBuf, ref meshBuf, ref mnX, ref mnY, ref mxX, ref mxY);
                            if (mnX == float.MaxValue) continue;   // 无渲染内容帧
                            s.MinX.Add(mnX);
                            s.MinY.Add(mnY);
                            s.MaxX.Add(mxX);
                            s.MaxY.Add(mxY);
                            if (mnX < s.FullMinX) s.FullMinX = mnX;
                            if (mnY < s.FullMinY) s.FullMinY = mnY;
                            if (mxX > s.FullMaxX) s.FullMaxX = mxX;
                            if (mxY > s.FullMaxY) s.FullMaxY = mxY;
                        }
                        if (s.MinX.Count == 0)
                        {
                            // 无内容：以 setup 姿势单帧计
                            skeleton.SetToSetupPose();
                            state.Apply(skeleton);
                            skeleton.UpdateWorldTransform();
                            float mnX = float.MaxValue, mnY = float.MaxValue, mxX = float.MinValue, mxY = float.MinValue;
                            AccumulateBounds(skeleton, regionBuf, ref meshBuf, ref mnX, ref mnY, ref mxX, ref mxY);
                            s.MinX.Add(mnX); s.MinY.Add(mnY); s.MaxX.Add(mxX); s.MaxY.Add(mxY);
                            s.FullMinX = s.RegMinX = mnX; s.FullMinY = s.RegMinY = mnY;
                            s.FullMaxX = s.RegMaxX = mxX; s.FullMaxY = s.RegMaxY = mxY;
                        }
                        result.Add(s);
                    }
                }
                else
                {
                    // 无动画：取 setup 姿态单帧
                    skeleton.SetToSetupPose();
                    state.Apply(skeleton);
                    skeleton.UpdateWorldTransform();
                    float mnX = float.MaxValue, mnY = float.MaxValue, mxX = float.MinValue, mxY = float.MinValue;
                    AccumulateBounds(skeleton, regionBuf, ref meshBuf, ref mnX, ref mnY, ref mxX, ref mxY);
                    var s = new AnimSamples { Name = string.Empty };
                    s.MinX.Add(mnX); s.MinY.Add(mnY); s.MaxX.Add(mxX); s.MaxY.Add(mxY);
                    s.FullMinX = s.RegMinX = mnX; s.FullMinY = s.RegMinY = mnY;
                    s.FullMaxX = s.RegMaxX = mxX; s.FullMaxY = s.RegMaxY = mxY;
                    result.Add(s);
                }
            }
            finally
            {
                stateData.DefaultMix = savedMix;
            }
            return result;
        }

        /// <summary>计算单动画的常规范围：丢弃尺寸超过 P98 分位的离散帧后取并集。</summary>
        private static void ComputeRegularBounds(AnimSamples s)
        {
            int n = s.MinX.Count;
            if (n == 0) return;
            float[] ws = new float[n];
            float[] hs = new float[n];
            for (int i = 0; i < n; i++)
            {
                ws[i] = s.MaxX[i] - s.MinX[i];
                hs[i] = s.MaxY[i] - s.MinY[i];
            }
            Array.Sort(ws);
            Array.Sort(hs);
            int idx = Math.Max(0, (int)Math.Ceiling(n * FrameQuantile) - 1);
            float w98 = ws[idx];
            float h98 = hs[idx];
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                float w = s.MaxX[i] - s.MinX[i];
                float h = s.MaxY[i] - s.MinY[i];
                if (w > w98 || h > h98) continue;   // 离散帧
                any = true;
                if (s.MinX[i] < s.RegMinX) s.RegMinX = s.MinX[i];
                if (s.MinY[i] < s.RegMinY) s.RegMinY = s.MinY[i];
                if (s.MaxX[i] > s.RegMaxX) s.RegMaxX = s.MaxX[i];
                if (s.MaxY[i] > s.RegMaxY) s.RegMaxY = s.MaxY[i];
            }
            if (!any)
            {
                // 全部被判离散（病态）：退回全程并集
                s.RegMinX = s.FullMinX; s.RegMinY = s.FullMinY;
                s.RegMaxX = s.FullMaxX; s.RegMaxY = s.FullMaxY;
            }
        }

        /// <summary>由世界包围盒求画布（与 init.lua 相同的扩展居中规则：x 方向以骨架原点为对称中心对称扩展，
        /// 使水平翻转（FlipX 镜像）后的范围 [-maxX, -minX] 也落在画布内，避免翻转后部分动画超出画布）。</summary>
        private static CanvasRect RectFromBounds(float minX, float minY, float maxX, float maxY)
        {
            if (minX == float.MaxValue) { minX = 0; minY = 0; maxX = 1; maxY = 1; }
            // 对称半径 = max(左侧延伸, 右侧延伸)：画布宽 = 2×半径，骨架原点位于画布中心
            float rx = Math.Max(-minX, maxX);
            float ry = -minY;
            float rw = rx * 2;
            float rh = maxY - minY;
            float nw = Math.Max(0, rw);
            float nh = Math.Max(0, rh);
            float x = (nw - rw) / 2f + rx;
            float y = (nh - rh) / 2f + ry;
            if (float.IsNaN(x) || float.IsNaN(y)) { x = 0; y = 0; }
            if (nw < 1) nw = 1;
            if (nh < 1) nh = 1;
            return new CanvasRect(x, y, nw, nh);
        }

        /// <summary>累加当前姿态下所有可渲染附件的世界顶点包围盒。</summary>
        private static void AccumulateBounds(Skeleton skeleton, float[] regionBuf, ref float[] meshBuf,
            ref float minX, ref float minY, ref float maxX, ref float maxY)
        {
            List<Slot> drawOrder = skeleton.DrawOrder;
            for (int i = 0, n = drawOrder.Count; i < n; i++)
            {
                Slot slot = drawOrder[i];
                Attachment attachment = slot.Attachment;
                if (attachment is RegionAttachment region)
                {
                    region.ComputeWorldVertices(slot.Bone, regionBuf);
                    Accumulate(regionBuf, 8, ref minX, ref minY, ref maxX, ref maxY);
                }
                else if (attachment is MeshAttachment mesh)
                {
                    int count = mesh.Vertices.Length;
                    if (meshBuf.Length < count) meshBuf = new float[count];
                    mesh.ComputeWorldVertices(slot, meshBuf);
                    Accumulate(meshBuf, count, ref minX, ref minY, ref maxX, ref maxY);
                }
                else if (attachment is SkinnedMeshAttachment skinned)
                {
                    int count = skinned.UVs.Length;
                    if (meshBuf.Length < count) meshBuf = new float[count];
                    skinned.ComputeWorldVertices(slot, meshBuf);
                    Accumulate(meshBuf, count, ref minX, ref minY, ref maxX, ref maxY);
                }
                // BoundingBox 等非渲染附件不参与，跳过
            }
        }

        private static void Accumulate(float[] verts, int count,
            ref float minX, ref float minY, ref float maxX, ref float maxY)
        {
            for (int i = 0; i < count; i += 2)
            {
                float vx = verts[i];
                float vy = verts[i + 1];
                if (vx < minX) minX = vx;
                if (vy < minY) minY = vy;
                if (vx > maxX) maxX = vx;
                if (vy > maxY) maxY = vy;
            }
        }
    }
}
