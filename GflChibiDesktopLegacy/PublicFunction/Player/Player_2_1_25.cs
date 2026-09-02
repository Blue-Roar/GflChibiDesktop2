using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spine2_1_25;
using GflChibiDesktop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

public class Player_2_1_25 : IPlayer
{
    private Skeleton skeleton;
    private AnimationState state;
    private SkeletonMeshRenderer skeletonRenderer;
    private List<Animation> listAnimation;
    private List<Skin> listSkin;
    private Atlas atlas;
    private SkeletonData skeletonData;
    private AnimationStateData stateData;
    private SkeletonBinary binary;
    private SkeletonJson json;
    /// <summary>最近一次应用的画布模式（Update 检测面板切换）。</summary>
    private int _lastCanvasMode = 2;
    /// <summary>动态画布统计计划（常规画布 + 离群动画），进程内计算一次。</summary>
    private DynamicCanvasPlan _dynamicPlan;
    /// <summary>当前是否正因播放"严重超出"动画而使用其全程画布。</summary>
    private bool _dynamicOversizeNow;

    /// <summary>
    /// 按画布模式应用画布：
    /// 0=小(448×448)、1=大(768×768)（固定，模型偏移=画布中心）；
    /// 2=动态：常驻画布 = 常规动画并集（剔除离散帧/离群动画），仅切到离群动画时临时用其全程画布。
    /// 窗口尺寸 = 基础 × 当前缩放。setInitial=true（首次加载）不通知窗口（由 OnModelLoaded→ApplyCanvas 完成）；
    /// 否则（运行中切换模式/动画）重摆模型偏移并触发 App.CanvasChanged 让 PetWindow 调整窗口。
    /// </summary>
    private void ApplyCanvasByMode(int mode, bool setInitial)
    {
        CanvasRect rect;
        if (mode == 0)
        {
            rect = new CanvasRect(224, 224, 448, 448);
        }
        else if (mode == 1)
        {
            rect = new CanvasRect(384, 384, 768, 768);
        }
        else
        {
            // 动态：统计常规画布（剔除离散帧与离群动画），当前动画若在离群集合则用其全程画布
            if (_dynamicPlan == null)
            {
                _dynamicPlan = CanvasCalculator.ComputeDynamicPlan(state, stateData, skeleton, App.globalValues.Scale);
            }
            bool oversize = _dynamicPlan.OversizeAnimations.Contains(App.globalValues.SelectAnimeName);
            rect = oversize ? _dynamicPlan.FullByAnimation[App.globalValues.SelectAnimeName] : _dynamicPlan.Base;
            _dynamicOversizeNow = oversize;
        }
        ApplyCanvasRect(rect, setInitial);
    }

    /// <summary>应用画布 rect：设基础画布/窗口尺寸/模型偏移，非首次时通知 PetWindow 调整窗口。</summary>
    private void ApplyCanvasRect(CanvasRect rect, bool setInitial)
    {
        float scale = App.globalValues.Scale;
        App.CanvasW = rect.W;
        App.CanvasH = rect.H;
        App.CanvasX = rect.X;
        App.CanvasY = rect.Y;
        // 窗口尺寸 = 基础 × 当前缩放（与 raylib setWindowSize(modelConfig.w * scale) 一致）
        App.globalValues.FrameWidth = Math.Ceiling(rect.W * scale);
        App.globalValues.FrameHeight = Math.Ceiling(rect.H * scale);
        // 模型默认位置 = 画布偏移
        App.globalValues.PosX = rect.X * scale;
        App.globalValues.PosY = rect.Y * scale;

        if (!setInitial)
        {
            App.NotifyCanvasChanged();
        }
    }

    public void Initialize()
    {
        Player.Initialize(ref App.graphicsDevice, ref App.spriteBatch);
    }

    public void LoadContent(ContentManager contentManager)
    {
        skeletonRenderer = new SkeletonMeshRenderer(App.graphicsDevice);
        skeletonRenderer.PremultipliedAlpha = App.globalValues.Alpha;

        atlas = new Atlas(App.globalValues.SelectAtlasFile, new XnaTextureLoader(App.graphicsDevice));

        if (Common.IsBinaryData(App.globalValues.SelectSpineFile))
        {
            binary = new SkeletonBinary(atlas);
            binary.Scale = App.globalValues.Scale;
            skeletonData = binary.ReadSkeletonData(App.globalValues.SelectSpineFile);
        }
        else
        {
            json = new SkeletonJson(atlas);
            json.Scale = App.globalValues.Scale;
            skeletonData = json.ReadSkeletonData(App.globalValues.SelectSpineFile);
        }
        App.globalValues.SpineVersion = skeletonData.Version;
        skeleton = new Skeleton(skeletonData);

        Common.SetInitLocation(skeleton.Data.Height);
        App.globalValues.FileHash = skeleton.Data.Hash;

        stateData = new AnimationStateData(skeleton.Data);
        // 动画切换交叉过渡时长（与新版 raylib 的 defaultMix=0.2 对齐），
        // 使 SetAnimation 在已有轨道上平滑过渡而非硬切
        stateData.DefaultMix = 0.2f;

        state = new AnimationState(stateData);
        // 一次性（非循环）动画播完即通知（victory→victoryloop 等接续由 PetWindow 处理，
        // 不再依赖按 AnimeDuration 猜测时长的 DispatcherTimer，避免 victory 播完静止无人接续）
        state.Complete += (s, trackIndex, loopCount) =>
        {
            if (!App.globalValues.IsLoop)
            {
                App.NotifyOnceAnimationFinished();
            }
        };

        List<string> AnimationNames = new List<string>();
        listAnimation = state.Data.skeletonData.Animations;
        foreach (Animation An in listAnimation)
        {
            AnimationNames.Add(An.name);
        }
        App.globalValues.AnimeList = AnimationNames;

        List<string> SkinNames = new List<string>();
        listSkin = state.Data.skeletonData.Skins;
        foreach (Skin Sk in listSkin)
        {
            SkinNames.Add(Sk.name);
        }
        App.globalValues.SkinList = SkinNames;

        // ===== 画布：按画布模式应用（0=小 448×448、1=大 768×768、2=动态）=====
        // 动态模式：常驻画布 = 常规动画并集（剔除离散帧/离群动画，避免个别动画几帧就把画布撑到最大）；
        // 切到"严重超出"的离群动画（如 victory）时才临时用其全程画布，播完恢复常驻。
        // 固定模式（小/大）：模型偏移 = 画布中心（超出画布部分裁切）。
        try
        {
            ApplyCanvasByMode(App.globalValues.CanvasMode, true);
        }
        catch (Exception ex)
        {
            // 计算失败时沿用默认 448 画布，不阻断模型加载
            Console.WriteLine("[canvas] 计算画布失败，使用默认尺寸: " + ex.Message);
        }
        _lastCanvasMode = App.globalValues.CanvasMode;

        // 恢复的动画名（settings1.json）若在当前模型不存在，回退默认（模型可能已变更）
        if (App.globalValues.SelectAnimeName != string.Empty &&
            state.Data.skeletonData.FindAnimation(App.globalValues.SelectAnimeName) != null)
        {
            state.SetAnimation(0, App.globalValues.SelectAnimeName, App.globalValues.IsLoop);
        }
        else
        {
            // 默认选择 wait 动画；模型没有 wait 则用第一个动画
            string defaultAnime = state.Data.skeletonData.animations[0].name;
            foreach (Animation a in state.Data.skeletonData.animations)
            {
                if (a.name == "wait")
                {
                    defaultAnime = "wait";
                    break;
                }
            }
            App.globalValues.SelectAnimeName = defaultAnime;
            state.SetAnimation(0, defaultAnime, App.globalValues.IsLoop);
        }

        if (App.isNew)
        {
            // 新加载模型完成（模型默认位置/画布已在 ApplyCanvasByMode 设置，
            // 窗口尺寸/位置由 PetWindow.ApplyCanvas 调整）
            App.NotifyModelLoaded?.Invoke();
        }
        App.isNew = false;

        if (state != null)
        {
            TrackEntry entry = state.GetCurrent(0);
            if (entry != null)
            {
                App.globalValues.AnimeDuration = entry.endTime;
            }
        }

    }



    public void Update(GameTime gameTime)
    {
        // 画布模式切换（面板"画布"下拉）：应用新模式——固定小/大 或 动态（全部动画并集，conf 沿用/计算）
        if (App.globalValues.CanvasMode != _lastCanvasMode)
        {
            _lastCanvasMode = App.globalValues.CanvasMode;
            try
            {
                ApplyCanvasByMode(App.globalValues.CanvasMode, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[canvas] 切换画布模式失败: " + ex.Message);
            }
        }

        if (App.globalValues.SelectAnimeName != string.Empty && App.globalValues.SetAnime)
        {
            // 动态模式：切到"严重超出常规画布"的离群动画（如 victory）时，临时切换为其全程画布；
            // 切回常规动画时恢复常驻画布——常驻画布按常规范围保持，避免被个别大动画长期撑大
            if (App.globalValues.CanvasMode == 2 && _dynamicPlan != null && _dynamicPlan.OversizeAnimations.Count > 0)
            {
                bool oversize = _dynamicPlan.OversizeAnimations.Contains(App.globalValues.SelectAnimeName);
                if (oversize != _dynamicOversizeNow)
                {
                    _dynamicOversizeNow = oversize;
                    try
                    {
                        // 画布扩大/缩小时窗口反向平移，使人形保持在原屏幕位置（模型原点不动）
                        float oldX = App.globalValues.PosX;
                        float oldY = App.globalValues.PosY;
                        CanvasRect target = oversize
                            ? _dynamicPlan.FullByAnimation[App.globalValues.SelectAnimeName]
                            : _dynamicPlan.Base;
                        float sc = App.globalValues.Scale;
                        App.CanvasShiftX = oldX - target.X * sc;
                        App.CanvasShiftY = oldY - target.Y * sc;
                        ApplyCanvasRect(target, false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[canvas] 动画切换画布失败: " + ex.Message);
                    }
                }
            }
            // 不清轨道/不重置姿势：由 AnimationState 依据 DefaultMix 在现有动画上交叉过渡，
            // 与新版 raylib 的 setAnimationByName 行为一致，避免硬切抽搐
            state.SetAnimation(0, App.globalValues.SelectAnimeName, App.globalValues.IsLoop);
            TrackEntry entry = state.GetCurrent(0);
            if (entry != null)
            {
                // 供一次性动作（s/reload/victory）接续定时器使用的当前动画时长
                App.globalValues.AnimeDuration = entry.EndTime;
            }
            App.globalValues.SetAnime = false;
        }

        if (App.globalValues.SelectSkin != string.Empty && App.globalValues.SetSkin)
        {
            skeleton.SetSkin(App.globalValues.SelectSkin);
            skeleton.SetSlotsToSetupPose();
            App.globalValues.SetSkin = false;
        }
        if (App.globalValues.SelectSpineVersion != "2.1.25" || App.globalValues.FileHash != skeleton.Data.Hash)
        {
            state = null;
            skeletonRenderer = null;
            return;
        }
        App.graphicsDevice.Clear(Color.Transparent);

        Player.DrawBG(ref App.spriteBatch);
        // 动画速度与“帧率”设置解耦：帧率（Speed）只控制渲染帧率，动画恒为正常速度
        App.globalValues.TimeScale = 1f;

        state.Update((float)gameTime.ElapsedGameTime.TotalMilliseconds / 1000f);

        state.Apply(skeleton);
        if (binary != null)
        {
            if (App.globalValues.Scale != binary.Scale)
            {
                binary.Scale = App.globalValues.Scale;
                skeletonData = binary.ReadSkeletonData(App.globalValues.SelectSpineFile);
                skeleton = new Skeleton(skeletonData);
            }
        }
        else if (json != null)
        {
            if (App.globalValues.Scale != json.Scale)
            {
                json.Scale = App.globalValues.Scale;
                skeletonData = json.ReadSkeletonData(App.globalValues.SelectSpineFile);
                skeleton = new Skeleton(skeletonData);
            }
        }

        skeleton.X = App.globalValues.PosX;
        skeleton.Y = App.globalValues.PosY;
        skeleton.FlipX = App.globalValues.FilpX;

        skeleton.RootBone.Rotation = App.globalValues.Rotation;
        skeleton.UpdateWorldTransform();
        skeletonRenderer.PremultipliedAlpha = App.globalValues.Alpha;
        skeletonRenderer.Begin();
        skeletonRenderer.Draw(skeleton);
        skeletonRenderer.End();


    }

    public void Draw()
    {
        if (state != null)
        {
            TrackEntry entry = state.GetCurrent(0);
            if (entry != null)
            {
                if (App.globalValues.TimeScale == 0)
                {
                    entry.Time = entry.EndTime * App.globalValues.Lock;
                    entry.TimeScale = 0;
                }
                else
                {
                    App.globalValues.Lock = (entry.LastTime % entry.EndTime) / entry.EndTime;
                    entry.TimeScale = App.globalValues.TimeScale;
                }
                App.globalValues.LoadingProcess = $"{ Math.Round((entry.Time % entry.EndTime) / entry.EndTime * 100, 2)}%";
            }
        }
    }

    public void ChangeSet()
    {
        App.appXC.ContentManager.Dispose();
        atlas.Dispose();
        atlas = null;
        App.appXC.LoadContent.Invoke(App.appXC.ContentManager);
    }

    public void SizeChange()
    {
        if (App.graphicsDevice != null)
        Player.UserControl_SizeChanged(ref App.graphicsDevice);
    }
}

