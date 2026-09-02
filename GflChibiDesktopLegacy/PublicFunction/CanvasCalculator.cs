using Spine2_1_25;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace GflChibiDesktop
{
    /// <summary>
    /// 画布尺寸（未缩放基础值，语义与 model.conf.json 的 x/y/w/h 一致）。
    /// x/y = 模型在画布中的偏移；w/h = 画布尺寸。
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
    /// 画布尺寸计算与持久化：与 raylib 运行时的 blockly_spine2125 一致
    /// （calcWindowSize.lua 采样 + init.lua 扩展居中）。
    /// 遍历全部动画采样求并集包围盒，向外扩展画布并居中模型；
    /// 结果写回 <工作目录>/assets/model.conf.json（与 luajit 运行时同一文件），
    /// 后续启动与 V2 重建实例（备份/恢复该文件）时直接沿用，避免每次重算包围盒卡顿。
    /// 画布为"全部动画并集"的固定最大值（用户选择回退到固定大画布，而非随动画动态缩放）。
    /// </summary>
    internal static class CanvasCalculator
    {
        /// <summary>模型配置文件（相对工作目录解析，与 luajit 运行时一致）。</summary>
        public static string ModelConfFile => Path.Combine(Environment.CurrentDirectory, "assets", "model.conf.json");

        /// <summary>动画采样率（Hz），与 calcWindowSize.lua 的 240 一致。</summary>
        private const float SampleRate = 240f;
        /// <summary>单动画最大采样步数（240Hz 下约 50 秒）：防止超长/病态动画导致启动卡死。</summary>
        private const int MaxSteps = 12000;

        /// <summary>
        /// 尝试读取已保存的画布尺寸（model.conf.json 中的 x/y/w/h）。
        /// 未保存、模型不匹配或数值非法时返回 null。
        /// </summary>
        public static CanvasRect? TryReadSavedRect()
        {
            try
            {
                string path = ModelConfFile;
                if (!File.Exists(path)) return null;
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) return null;
                    // 配置文件对应其他模型时不沿用（路径经 junction 时绝对前缀不同，按规范化后缀比较）
                    if (root.TryGetProperty("atlas", out JsonElement atlas) && !PathsMatch(LegacyArgs.ModelFile, atlas.GetString()))
                        return null;
                    if (!root.TryGetProperty("x", out JsonElement jx) || !root.TryGetProperty("y", out JsonElement jy) ||
                        !root.TryGetProperty("w", out JsonElement jw) || !root.TryGetProperty("h", out JsonElement jh))
                        return null;
                    float x = jx.GetSingle(), y = jy.GetSingle(), w = jw.GetSingle(), h = jh.GetSingle();
                    if (w <= 0 || h <= 0 || float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(w) || float.IsNaN(h))
                        return null;
                    return new CanvasRect(x, y, w, h);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 把计算出的画布尺寸写回 model.conf.json（保留原有 skeleton/type/atlas 等字段）。
        /// 文件不存在（独立运行等）或对应其他模型时跳过，避免污染 V2 管理的配置。
        /// </summary>
        public static void SaveRect(float x, float y, float w, float h)
        {
            try
            {
                string path = ModelConfFile;
                if (!File.Exists(path)) return;
                string text = File.ReadAllText(path);
                using (JsonDocument doc = JsonDocument.Parse(text))
                {
                    JsonElement root = doc.RootElement;
                    // 配置文件对应其他模型：不动它，交由 V2 在重建实例/切换模型时重写
                    if (root.ValueKind == JsonValueKind.Object &&
                        root.TryGetProperty("atlas", out JsonElement atlas) &&
                        !PathsMatch(LegacyArgs.ModelFile, atlas.GetString()))
                    {
                        return;
                    }
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new Utf8JsonWriter(ms))
                        {
                            writer.WriteStartObject();
                            if (root.ValueKind == JsonValueKind.Object)
                            {
                                foreach (JsonProperty prop in root.EnumerateObject())
                                {
                                    if (prop.Name == "x" || prop.Name == "y" || prop.Name == "w" || prop.Name == "h") continue;
                                    prop.WriteTo(writer);
                                }
                            }
                            writer.WriteNumber("x", x);
                            writer.WriteNumber("y", y);
                            writer.WriteNumber("w", w);
                            writer.WriteNumber("h", h);
                            writer.WriteEndObject();
                        }
                        File.WriteAllText(path, Encoding.UTF8.GetString(ms.ToArray()));
                    }
                }
            }
            catch
            {
                // 写回失败不影响本次运行（下次启动重新计算）
            }
        }

        /// <summary>
        /// 计算画布：遍历全部动画采样求并集包围盒，再按 init.lua 的规则向外扩展并居中。
        /// 采样发生在当前缩放下的骨架（binary.Scale），除以缩放后返回未缩放基础画布。
        /// </summary>
        public static CanvasRect ComputeCanvasRect(AnimationState state, AnimationStateData stateData, Skeleton skeleton, float scale)
        {
            ComputeBounds(state, stateData, skeleton, out float minX, out float minY, out float maxX, out float maxY);

            // 与 init.lua 相同：x 方向包围盒偏在原点一侧时以原点对称扩展（y 不处理，站立模型向下延伸属正常）
            float rx = -minX;
            float ry = -minY;
            float rw = maxX - minX;
            float rh = maxY - minY;
            if (rx * 2 < rw)
            {
                rx = rw - rx;
                rw = rx * 2;
            }
            float nw = Math.Max(0, rw);
            float nh = Math.Max(0, rh);
            float x = (nw - rw) / 2f + rx;
            float y = (nh - rh) / 2f + ry;
            if (float.IsNaN(x) || float.IsNaN(y)) { x = 0; y = 0; }
            if (nw < 1) nw = 1;
            if (nh < 1) nh = 1;

            // 除以当前缩放，得到未缩放基础画布（与 model.conf.json 的存储语义一致）
            if (scale > 0)
            {
                float inv = 1f / scale;
                x *= inv;
                y *= inv;
                nw *= inv;
                nh *= inv;
            }
            return new CanvasRect(x, y, nw, nh);
        }

        /// <summary>遍历全部动画（240Hz 采样）求世界坐标并集包围盒，与 calcWindowSize.lua 一致。</summary>
        private static void ComputeBounds(AnimationState state, AnimationStateData stateData, Skeleton skeleton,
            out float minX, out float minY, out float maxX, out float maxY)
        {
            float savedMix = stateData.DefaultMix;
            stateData.DefaultMix = 0;   // 采样时不交叉过渡（与 lua 一致）

            minX = minY = float.MaxValue;
            maxX = maxY = float.MinValue;

            float[] regionBuf = new float[8];
            float[] meshBuf = Array.Empty<float>();

            try
            {
                List<Animation> animations = skeleton.Data.Animations;
                bool any = false;
                if (animations != null)
                {
                    for (int i = 0; i < animations.Count; i++)
                    {
                        Animation anim = animations[i];
                        TrackEntry entry = state.SetAnimation(0, anim, false);
                        float dur = entry.EndTime;
                        if (float.IsNaN(dur) || dur < 0) dur = 0;
                        // 以动画时长决定采样步数；用步数上限代替浮点相等的 while 循环，避免死循环
                        int steps = (int)Math.Min(Math.Ceiling(dur * SampleRate) + 2, MaxSteps);
                        for (int s = 0; s < steps; s++)
                        {
                            state.Update(1f / SampleRate);
                            state.Apply(skeleton);
                            skeleton.UpdateWorldTransform();
                            AccumulateBounds(skeleton, regionBuf, ref meshBuf, ref minX, ref minY, ref maxX, ref maxY);
                        }
                        any = true;
                    }
                }
                if (!any)
                {
                    // 无动画：取当前（setup）姿态的包围盒
                    state.Apply(skeleton);
                    skeleton.UpdateWorldTransform();
                    AccumulateBounds(skeleton, regionBuf, ref meshBuf, ref minX, ref minY, ref maxX, ref maxY);
                }
            }
            finally
            {
                stateData.DefaultMix = savedMix;
            }
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

        /// <summary>
        /// 比较命令行模型绝对路径与配置文件中的（相对）路径。
        /// 实例目录下 assets/spine 为 junction，绝对前缀不同，故相对路径按规范化后缀匹配。
        /// </summary>
        private static bool PathsMatch(string absolutePath, string confPath)
        {
            if (string.IsNullOrEmpty(absolutePath) || string.IsNullOrEmpty(confPath)) return false;
            string rel = confPath.Replace('/', Path.DirectorySeparatorChar);
            string abs = Path.GetFullPath(absolutePath);
            if (Path.IsPathRooted(rel))
                return string.Equals(Path.GetFullPath(rel), abs, StringComparison.OrdinalIgnoreCase);
            return abs.EndsWith(rel, StringComparison.OrdinalIgnoreCase);
        }
    }
}
