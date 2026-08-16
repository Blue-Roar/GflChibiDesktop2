#nullable disable
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using static GflChibiDesktop.DummyListReader;
using MessageBox = HandyControl.Controls.MessageBox;
using static GflChibiDesktop.WebAPI;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace GflChibiDesktop.Windows
{
    /// <summary>
    /// 数据加载结果，直接传递给主窗口。
    /// </summary>
    public class ChibiModelData
    {
        public string DisplayName { get; set; }
        public string SkeletonFile { get; set; }
        public string AtlasFile { get; set; }
        /// <summary>
        /// 是否为多开（新开一个桌宠实例），而不是应用到当前选中实例。
        /// </summary>
        public bool NewInstance { get; set; }
    }

    public partial class DataManagerWindow : Window
    {
        public static string AppDir => Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "app") + Path.DirectorySeparatorChar;
        public readonly string productName = ((AssemblyProductAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyProductAttribute))).Product.ToString();
        public readonly string productTitle = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute))).Title.ToString();
        public readonly string productDescription = ((AssemblyDescriptionAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyDescriptionAttribute))).Description.ToString();
        public readonly string productCopyright = ((AssemblyCopyrightAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyCopyrightAttribute))).Copyright.ToString();
        public readonly string productCompany = ((AssemblyCompanyAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyCompanyAttribute))).Company.ToString();
        public readonly Version productVersion = new Version(((AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyFileVersionAttribute))).Version) ?? Assembly.GetExecutingAssembly().GetName().Version;
        public readonly string currentBuild = ((AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyInformationalVersionAttribute))).InformationalVersion;
        public string homepageLink = "https://projects.brightsu.cn/GflChibiDesktop/V2/";
        public string updateLink = "https://projects.brightsu.cn/GflChibiDesktop/V2/download";
        public string donateLink = "https://projects.brightsu.cn/GflChibiDesktop/donate";
        public string chibiListLink = "https://projects.brightsu.cn/GFL/chibi-list";
        public string extraStr = string.Empty;

        /// <summary>
        /// 由主窗口传入的公告消息（主窗口启动时从 startup 接口获取）。
        /// </summary>
        public string AnnouncementMsg
        {
            get => announcementMsg;
            set
            {
                announcementMsg = value;
                if (!string.IsNullOrEmpty(value))
                {
                    extraStr = value;
                    Dispatcher.Invoke(() => lblExtraStr.Content = extraStr);
                }
            }
        }
        private string announcementMsg = string.Empty;

        /// <summary>
        /// 数据加载完成后触发，把加载结果直接传递给主窗口。
        /// </summary>
        public event Action<ChibiModelData> ModelLoadRequested;

        List<ComponentModel> initializeDataSet = new List<ComponentModel>();
        /// <summary>
        /// 数据表是否已完成加载（防止排队的进度刷新覆盖完成提示）。
        /// </summary>
        volatile bool dummyListLoaded = false;

        public DataManagerWindow()
        {
            InitializeComponent();

            btnVersion.Content = $"程序版本：{productVersion}";

            // 网络请求与数据表构建放后台线程，避免阻塞 UI（主窗口由同线程创建，会因此卡住）
            System.Threading.Tasks.Task.Run(() => UpdateLinks());
            System.Threading.Tasks.Task.Run(() => LoadDummyList());

            lblExtraStr.Content = extraStr;
        }

        private void UpdateLinks()
        {
            try
            {
                bool IndexPost = false;
                string IndexStr = HttpRequestHelper.PostWebRequest("https://api.brightsu.cn/GflChibiDesktop2", string.Empty, Encoding.UTF8, ref IndexPost);
                if (IndexPost)
                {
                    IndexRoot rt = JsonConvert.DeserializeObject<IndexRoot>(IndexStr);
                    if (rt.ret == 200)
                    {
                        if (CheckIsUrlFormat(rt.data.homepage_link)) { homepageLink = rt.data.homepage_link; }
                        if (CheckIsUrlFormat(rt.data.update_link)) { updateLink = rt.data.update_link; }
                        if (CheckIsUrlFormat(rt.data.donate_link)) { donateLink = rt.data.donate_link; }
                        if (CheckIsUrlFormat(rt.data.chibi_list_link)) { chibiListLink = rt.data.chibi_list_link; }
                    }
                    else
                    {
                        Dispatcher.Invoke(() => HandyControl.Controls.Growl.ErrorGlobal($"API 接口调用失败，链接更新失败。\n部分功能可能会受到影响。\n错误：API 接口返回了状态码 {rt.ret}"));
                    }
                }
                else
                {
                    Dispatcher.Invoke(() => HandyControl.Controls.Growl.ErrorGlobal($"API 接口调用失败，链接更新失败。\n部分功能可能会受到影响。\n错误：{IndexStr}"));
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => HandyControl.Controls.Growl.ErrorGlobal($"API 接口调用失败，链接更新失败。\n部分功能可能会受到影响。\n错误：{ex}"));
                return;
            }
        }

        //private void CheckForUpdates()
        //{
        //    try
        //    {
        //        bool UpdatePost = false;
        //        string UpdateStr = HttpRequestHelper.PostWebRequest("https://api.brightsu.cn/GflChibiDesktop2/update", string.Empty, Encoding.UTF8, ref UpdatePost);
        //        if (!UpdatePost)
        //        {
        //            return;
        //        }
        //        UpdateRoot rt = JsonConvert.DeserializeObject<UpdateRoot>(UpdateStr);

        //        if (rt.ret != 200)
        //        {
        //            return;
        //        }
        //        if (rt.data.version != null)
        //        {
        //            Version latestBuild = new Version(rt.data.buildver);
        //            bool urgentUpdate = false;
        //            if (rt.data.urgent == 1) { urgentUpdate = true; }
        //            if (latestBuild > productBuild)
        //            {
        //                if (urgentUpdate)
        //                {
        //                    HandyControl.Controls.Dialog.Show(_about);
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception)
        //    {
        //        return;
        //    }
        //}

        SolidColorBrush defaultColor = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        SolidColorBrush type0color = new SolidColorBrush(Color.FromRgb(255, 111, 181));
        SolidColorBrush type2color = new SolidColorBrush(Color.FromRgb(234, 234, 234));
        SolidColorBrush type3color = new SolidColorBrush(Color.FromRgb(107, 218, 199));
        SolidColorBrush type4color = new SolidColorBrush(Color.FromRgb(209, 223, 91));
        SolidColorBrush type5color = new SolidColorBrush(Color.FromRgb(254, 179, 0));
        SolidColorBrush type6color = new SolidColorBrush(Color.FromRgb(252, 79, 0));
        SolidColorBrush type7color = new SolidColorBrush(Color.FromRgb(222, 182, 255));

        public void LoadDummyList()
        {
            dummyListLoaded = false;
            KillEmptyDirectory($@"{AppDir}assets/spine");
            Dispatcher.Invoke(() => sbQuery.Clear());

            initializeDataSet.Clear();

            initializeDataSet.Add(new ComponentModel() { ComponentID = 1, ComponentName = "HGclass", Level = 1, ParentID = 0, ToolTip = "手枪人形", Header = "手枪(HG)", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 2, ComponentName = "HG2class", Level = 2, ParentID = 1, ToolTip = "初始二星手枪人形", Header = "★★", Foreground = type2color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 3, ComponentName = "HG3class", Level = 2, ParentID = 1, ToolTip = "初始三星手枪人形", Header = "★★★", Foreground = type3color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 4, ComponentName = "HG4class", Level = 2, ParentID = 1, ToolTip = "初始四星手枪人形", Header = "★★★★", Foreground = type4color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 5, ComponentName = "HG5class", Level = 2, ParentID = 1, ToolTip = "初始五星手枪人形", Header = "★★★★★", Foreground = type5color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 7, ComponentName = "HG7class", Level = 2, ParentID = 1, ToolTip = "特典手枪人形", Header = "★EXTRA", Foreground = type7color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 9, ComponentName = "HG0class", Level = 2, ParentID = 1, ToolTip = "特殊手枪人形", Header = "SPECIAL", Foreground = type0color });

            initializeDataSet.Add(new ComponentModel() { ComponentID = 11, ComponentName = "SMGclass", Level = 1, ParentID = 0, ToolTip = "冲锋枪人形", Header = "冲锋枪(SMG)", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 12, ComponentName = "SMG2class", Level = 2, ParentID = 11, ToolTip = "初始二星冲锋枪人形", Header = "★★", Foreground = type2color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 13, ComponentName = "SMG3class", Level = 2, ParentID = 11, ToolTip = "初始三星冲锋枪人形", Header = "★★★", Foreground = type3color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 14, ComponentName = "SMG4class", Level = 2, ParentID = 11, ToolTip = "初始四星冲锋枪人形", Header = "★★★★", Foreground = type4color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 15, ComponentName = "SMG5class", Level = 2, ParentID = 11, ToolTip = "初始五星冲锋枪人形", Header = "★★★★★", Foreground = type5color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 17, ComponentName = "SMG7class", Level = 2, ParentID = 11, ToolTip = "特典冲锋枪人形", Header = "★EXTRA", Foreground = type7color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 19, ComponentName = "SMG0class", Level = 2, ParentID = 11, ToolTip = "特殊冲锋枪人形", Header = "SPECIAL", Foreground = type0color });

            initializeDataSet.Add(new ComponentModel() { ComponentID = 21, ComponentName = "RFclass", Level = 1, ParentID = 0, ToolTip = "步枪人形", Header = "步枪(RF)", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 22, ComponentName = "RF2class", Level = 2, ParentID = 21, ToolTip = "初始二星步枪人形", Header = "★★", Foreground = type2color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 23, ComponentName = "RF3class", Level = 2, ParentID = 21, ToolTip = "初始三星步枪人形", Header = "★★★", Foreground = type3color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 24, ComponentName = "RF4class", Level = 2, ParentID = 21, ToolTip = "初始四星步枪人形", Header = "★★★★", Foreground = type4color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 25, ComponentName = "RF5class", Level = 2, ParentID = 21, ToolTip = "初始五星步枪人形", Header = "★★★★★", Foreground = type5color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 27, ComponentName = "RF7class", Level = 2, ParentID = 21, ToolTip = "特典步枪人形", Header = "★EXTRA", Foreground = type7color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 29, ComponentName = "RF0class", Level = 2, ParentID = 21, ToolTip = "特殊步枪人形", Header = "SPECIAL", Foreground = type0color });

            initializeDataSet.Add(new ComponentModel() { ComponentID = 31, ComponentName = "ARclass", Level = 1, ParentID = 0, ToolTip = "突击步枪人形", Header = "突击步枪(AR)", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 32, ComponentName = "AR2class", Level = 2, ParentID = 31, ToolTip = "初始二星突击步枪人形", Header = "★★", Foreground = type2color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 33, ComponentName = "AR3class", Level = 2, ParentID = 31, ToolTip = "初始三星突击步枪人形", Header = "★★★", Foreground = type3color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 34, ComponentName = "AR4class", Level = 2, ParentID = 31, ToolTip = "初始四星突击步枪人形", Header = "★★★★", Foreground = type4color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 35, ComponentName = "AR5class", Level = 2, ParentID = 31, ToolTip = "初始五星突击步枪人形", Header = "★★★★★", Foreground = type5color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 37, ComponentName = "AR7class", Level = 2, ParentID = 31, ToolTip = "特典突击步枪人形", Header = "★EXTRA", Foreground = type7color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 39, ComponentName = "AR0class", Level = 2, ParentID = 31, ToolTip = "特殊突击步枪人形", Header = "SPECIAL", Foreground = type0color });

            initializeDataSet.Add(new ComponentModel() { ComponentID = 41, ComponentName = "MGclass", Level = 1, ParentID = 0, ToolTip = "机枪人形", Header = "机枪(MG)", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 42, ComponentName = "MG2class", Level = 2, ParentID = 41, ToolTip = "初始二星机枪人形", Header = "★★", Foreground = type2color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 43, ComponentName = "MG3class", Level = 2, ParentID = 41, ToolTip = "初始三星机枪人形", Header = "★★★", Foreground = type3color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 44, ComponentName = "MG4class", Level = 2, ParentID = 41, ToolTip = "初始四星机枪人形", Header = "★★★★", Foreground = type4color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 45, ComponentName = "MG5class", Level = 2, ParentID = 41, ToolTip = "初始五星机枪人形", Header = "★★★★★", Foreground = type5color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 47, ComponentName = "MG7class", Level = 2, ParentID = 41, ToolTip = "特典机枪人形", Header = "★EXTRA", Foreground = type7color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 49, ComponentName = "MG0class", Level = 2, ParentID = 41, ToolTip = "特殊机枪人形", Header = "SPECIAL", Foreground = type0color });

            initializeDataSet.Add(new ComponentModel() { ComponentID = 51, ComponentName = "SGclass", Level = 1, ParentID = 0, ToolTip = "霰弹枪人形", Header = "霰弹枪(SG)", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 52, ComponentName = "SG2class", Level = 2, ParentID = 51, ToolTip = "初始二星霰弹枪人形", Header = "★★", Foreground = type2color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 53, ComponentName = "SG3class", Level = 2, ParentID = 51, ToolTip = "初始三星霰弹枪人形", Header = "★★★", Foreground = type3color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 54, ComponentName = "SG4class", Level = 2, ParentID = 51, ToolTip = "初始四星霰弹枪人形", Header = "★★★★", Foreground = type4color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 55, ComponentName = "SG5class", Level = 2, ParentID = 51, ToolTip = "初始五星霰弹枪人形", Header = "★★★★★", Foreground = type5color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 57, ComponentName = "SG7class", Level = 2, ParentID = 51, ToolTip = "特典霰弹枪人形", Header = "★EXTRA", Foreground = type7color });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 59, ComponentName = "SG0class", Level = 2, ParentID = 51, ToolTip = "特殊霰弹枪人形", Header = "SPECIAL", Foreground = type0color });

            initializeDataSet.Add(new ComponentModel() { ComponentID = 61, ComponentName = "HOCclass", Level = 1, ParentID = 0, ToolTip = "重装部队人形", Header = "重装部队(HOC)", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 62, ComponentName = "MTRclass", Level = 2, ParentID = 61, ToolTip = "迫击炮人形", Header = "迫击炮(MTR)", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 63, ComponentName = "ATWclass", Level = 2, ParentID = 61, ToolTip = "反坦克武器人形", Header = "反坦克武器(ATW)", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 64, ComponentName = "AGLclass", Level = 2, ParentID = 61, ToolTip = "榴弹发射器人形", Header = "榴弹发射器(AGL)", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 69, ComponentName = "HOCunclass", Level = 2, ParentID = 61, ToolTip = "未分类其它", Header = "未分类其它", Foreground = defaultColor });

            initializeDataSet.Add(new ComponentModel() { ComponentID = 71, ComponentName = "NPCclass", Level = 1, ParentID = 0, ToolTip = "NPC", Header = "NPC", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 72, ComponentName = "HUMANclass", Level = 2, ParentID = 71, ToolTip = "人类", Header = "人类", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 79, ComponentName = "NPCunclass", Level = 2, ParentID = 71, ToolTip = "未分类其它", Header = "未分类其它", Foreground = defaultColor });

            initializeDataSet.Add(new ComponentModel() { ComponentID = 81, ComponentName = "ENEMYclass", Level = 1, ParentID = 0, ToolTip = "敌方势力单位", Header = "敌方势力", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 82, ComponentName = "SANGVISclass", Level = 2, ParentID = 81, ToolTip = "铁血工造势力", Header = "铁血工造", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 83, ComponentName = "KCCOclass", Level = 2, ParentID = 81, ToolTip = "正规军势力", Header = "正规军", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 84, ComponentName = "PARADEUSclass", Level = 2, ParentID = 81, ToolTip = "帕拉蒂斯势力", Header = "帕拉蒂斯", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 85, ComponentName = "ETCclass", Level = 2, ParentID = 81, ToolTip = "其它势力", Header = "其它", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 89, ComponentName = "ENEMYunclass", Level = 2, ParentID = 81, ToolTip = "未分类其它", Header = "未分类其它", Foreground = defaultColor });

            initializeDataSet.Add(new ComponentModel() { ComponentID = 101, ComponentName = "OTHERclass", Level = 1, ParentID = 0, ToolTip = "其它人形", Header = "其它", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 102, ComponentName = "TDOLLunclass", Level = 2, ParentID = 101, ToolTip = "未分类的战术人形", Header = "战术人形", Foreground = defaultColor });
            initializeDataSet.Add(new ComponentModel() { ComponentID = 109, ComponentName = "UNKNOWNclass", Level = 2, ParentID = 101, ToolTip = "未分类的数据", Header = "未分类", Foreground = defaultColor });

            try
            {
                string str = File.ReadAllText($"{AppDir}chibi_list.json");
                RootObject rb = JsonConvert.DeserializeObject<RootObject>(str);
                int total = rb.content.Count;
                Dispatcher.Invoke(() =>
                {
                    btn_LoadDummyList.ToolTip = $"当前人形数据列表版本 {rb.meta.version}";
                    lblListVersion.Text = rb.meta.version;
                    pb_loader.IsIndeterminate = false;
                    pb_loader.Maximum = total;
                    pb_loader.Value = 0;
                    tii.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
                    tii.ProgressValue = 0;
                });

                int counter = 0;

                foreach (Content content in rb.content)
                {
                    counter++;
                    content.type = content.type ?? "";
                    content.display = content.display ?? content.name;
                    content.display_full = content.display_full ?? content.display;

                    // 逐条刷新 UI 会阻塞线程，改为按批次刷新
                    if (counter % 200 == 0)
                    {
                        int c = counter;
                        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                        {
                            if (dummyListLoaded) return;
                            lbl_loader.Content = $"正在处理：{c} / {total}";
                            pb_loader.Value = c;
                            tii.ProgressValue = (double)c / total;
                        }));
                    }
                    try
                    {
                        bool displaySwitch = true;
                        ComponentModel node = new ComponentModel();
                        node.ComponentName = $"dummy_{content.name.Replace(" ", string.Empty)}";
                        node.Header = content.display;
                        node.ComponentID = 200 + counter;
                        string[] tagString = new string[12];
                        tagString[0] = $"{displaySwitch}";
                        tagString[1] = content.name;
                        tagString[2] = content.parent;
                        tagString[3] = content.type;
                        tagString[4] = content.display;
                        tagString[5] = content.display_full;
                        tagString[6] = content.path;
                        tagString[7] = content.filename;
                        tagString[8] = content.cg;
                        tagString[9] = content.cg_d;
                        tagString[10] = content.filename_r;
                        tagString[11] = content.files;
                        node.Tag = tagString;
                        //node.ImageKey = content.type;
                        //node.SelectedImageKey = content.type;
                        node.Foreground = defaultColor;
                        node.ToolTip = content.display_full;
                        if (content.category == "TDOLL")
                        {
                            if (content.type.Contains("0")) { node.Foreground = type0color; }
                            if (content.type.Contains("2")) { node.Foreground = type2color; }
                            if (content.type.Contains("3")) { node.Foreground = type3color; }
                            if (content.type.Contains("4")) { node.Foreground = type4color; }
                            if (content.type.Contains("5")) { node.Foreground = type5color; }
                            if (content.type.Contains("6")) { node.Foreground = type6color; }
                            if (content.type.Contains("7")) { node.Foreground = type7color; }
                        }
                        else if (content.category == "ENEMY")
                        {
                            if (content.type.Contains("0")) { node.Foreground = type0color; }
                            if (content.type.Contains("1")) { node.Foreground = type2color; }
                            if (content.type.Contains("2")) { node.Foreground = type3color; }
                            if (content.type.Contains("3")) { node.Foreground = type5color; }
                        }

                        node.ParentID = 109;

                        if (content.name == content.parent)
                        {
                            node.Level = 3;
                            if (content.category == "TDOLL")
                            {
                                switch (content.type.ToUpper())
                                {
                                    case "HG2":
                                        node.ParentID = 2;
                                        break;
                                    case "HG3":
                                        node.ParentID = 3;
                                        break;
                                    case "HG4":
                                        node.ParentID = 4;
                                        break;
                                    case "HG5":
                                        node.ParentID = 5;
                                        break;
                                    case "HG7":
                                        node.ParentID = 7;
                                        break;
                                    case "HG0":
                                        node.ParentID = 9;
                                        break;
                                    case "SMG2":
                                        node.ParentID = 12;
                                        break;
                                    case "SMG3":
                                        node.ParentID = 13;
                                        break;
                                    case "SMG4":
                                        node.ParentID = 14;
                                        break;
                                    case "SMG5":
                                        node.ParentID = 15;
                                        break;
                                    case "SMG7":
                                        node.ParentID = 17;
                                        break;
                                    case "SMG0":
                                        node.ParentID = 19;
                                        break;
                                    case "RF2":
                                        node.ParentID = 22;
                                        break;
                                    case "RF3":
                                        node.ParentID = 23;
                                        break;
                                    case "RF4":
                                        node.ParentID = 24;
                                        break;
                                    case "RF5":
                                        node.ParentID = 25;
                                        break;
                                    case "RF7":
                                        node.ParentID = 27;
                                        break;
                                    case "RF0":
                                        node.ParentID = 29;
                                        break;
                                    case "AR2":
                                        node.ParentID = 32;
                                        break;
                                    case "AR3":
                                        node.ParentID = 33;
                                        break;
                                    case "AR4":
                                        node.ParentID = 34;
                                        break;
                                    case "AR5":
                                        node.ParentID = 35;
                                        break;
                                    case "AR7":
                                        node.ParentID = 37;
                                        break;
                                    case "AR0":
                                        node.ParentID = 39;
                                        break;
                                    case "MG2":
                                        node.ParentID = 42;
                                        break;
                                    case "MG3":
                                        node.ParentID = 43;
                                        break;
                                    case "MG4":
                                        node.ParentID = 44;
                                        break;
                                    case "MG5":
                                        node.ParentID = 45;
                                        break;
                                    case "MG7":
                                        node.ParentID = 47;
                                        break;
                                    case "MG0":
                                        node.ParentID = 49;
                                        break;
                                    case "SG2":
                                        node.ParentID = 52;
                                        break;
                                    case "SG3":
                                        node.ParentID = 53;
                                        break;
                                    case "SG4":
                                        node.ParentID = 54;
                                        break;
                                    case "SG5":
                                        node.ParentID = 55;
                                        break;
                                    case "SG7":
                                        node.ParentID = 57;
                                        break;
                                    case "SG0":
                                        node.ParentID = 59;
                                        break;
                                    default:
                                        node.ParentID = 102;
                                        break;
                                }
                            }
                            else if (content.category == "HOC")
                            {
                                switch (content.type.ToUpper())
                                {
                                    case "MTR":
                                        node.ParentID = 62;
                                        break;
                                    case "ATW":
                                        node.ParentID = 63;
                                        break;
                                    case "AGL":
                                        node.ParentID = 64;
                                        break;
                                    default:
                                        node.ParentID = 69;
                                        break;
                                }
                            }
                            else if (content.category == "NPC")
                            {
                                switch (content.type.ToUpper())
                                {
                                    case "HUMAN":
                                        node.ParentID = 72;
                                        break;
                                    default:
                                        node.ParentID = 79;
                                        break;
                                }
                            }
                            else if (content.category == "ENEMY")
                            {
                                if (content.type != "")
                                {
                                    switch (content.type.ToUpper().Substring(0, content.type.Length - 1))
                                    {
                                        case "SANGVIS":
                                           node.ParentID = 82;
                                           break;
                                        case "KCCO":
                                           node.ParentID = 83;
                                           break;
                                        case "PARADEUS":
                                           node.ParentID = 84;
                                           break;
                                        case "ETC":
                                           node.ParentID = 85;
                                           break;
                                        default:
                                            node.ParentID = 89;
                                            break;
                                    }
                                }
                            }
                            initializeDataSet.Add(node);
                        }
                        else
                        {
                            node.Level = 4;
                            //if (content.type == "HUMAN") { node.Level = 3; }
                            node.ParentID = 109;
                            foreach (ComponentModel item in initializeDataSet)
                            {
                                if (item.ComponentName == $"dummy_{content.parent.Replace(" ", string.Empty)}")
                                {
                                    //node.IsExpanded = true;
                                    node.ParentID = item.ComponentID;
                                }
                            }
                            initializeDataSet.Add(node);
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => HandyControl.Controls.Growl.ErrorGlobal($"构建战术人形数据列表时出错。\n{ex}"));
                    }
                }


                // 移除没有子节点的一级分类（Level<3 并且没有其他项的 ParentID 指向它）
                // 预构建 ParentID 索引，避免 O(n²) 扫描
                var childrenIndex = new Dictionary<int, List<ComponentModel>>();
                foreach (var item in initializeDataSet)
                {
                    if (!childrenIndex.TryGetValue(item.ParentID, out var list))
                    {
                        list = new List<ComponentModel>();
                        childrenIndex[item.ParentID] = list;
                    }
                    list.Add(item);
                }
                var emptyParents = initializeDataSet.Where(n => n.Level < 3 && !childrenIndex.ContainsKey(n.ComponentID)).ToList();
                foreach (var ep in emptyParents)
                {
                    initializeDataSet.Remove(ep);
                    if (childrenIndex.TryGetValue(ep.ParentID, out var pl))
                    {
                        pl.Remove(ep);
                    }
                }

                //加载数据（LoadTreeView 为纯数据处理，用索引避免 O(n²)）
                List<ComponentModel> tree = LoadTreeView(0, childrenIndex);

                List<ComponentModel> LoadTreeView(int id, Dictionary<int, List<ComponentModel>> index)
                {
                    if (!index.TryGetValue(id, out var node)) return new List<ComponentModel>();
                    foreach (var item in node)
                    {
                        item.Children = LoadTreeView(item.ComponentID, index);
                    }
                    return node;
                }

                //});
                int loaded = counter;
                dummyListLoaded = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    tv_InternalSelector.ItemsSource = tree;
                    lbl_loader.Content = $"已加载 {loaded} 条数据，等待下一步操作";
                    pb_loader.IsIndeterminate = true;
                    tii.ProgressValue = 100;
                    tii.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Indeterminate;
                }));
                //tv_InternalSelector.Items.Add(treeViewItemTemp);


            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(() => HandyControl.Controls.Growl.ErrorGlobal($"加载战术人形数据列表时出错。\n{ex}"));
            }
            Dispatcher.BeginInvoke(() => tvAfterSelect());
        }
        private ComponentModel SelectedItem;
        private void tv_InternalSelector_Selected(object sender, RoutedEventArgs e)
        {
            lbl_InternalSelected.Text = "请选择要加载的战术人形";
            lbl_InternalSelected.Foreground = defaultColor;
            lblSelectedItem.Content = "未选择";
            lblSelectedItem.Foreground = defaultColor;

            img_Preview.Source = null;

            TreeViewItem tvi = e.OriginalSource as TreeViewItem;
            ComponentModel item = (ComponentModel)tvi.Header;
            SelectedItem = item;

            tvAfterSelect();
        }

        private void tvAfterSelect()
        {
            ComponentModel item = SelectedItem;
            //TreeViewItem item = (TreeViewItem)tv_InternalSelector.SelectedItem;

            btn_downloadData.IsEnabled = false;
            btn_downloadData.Visibility = Visibility.Visible;
            btn_deleteData.IsEnabled = false;
            btn_deleteData.Visibility = Visibility.Collapsed;
            btn_loadCG.IsEnabled = false;
            chb_save_cg.IsEnabled = false;
            btn_loadData.IsEnabled = false;
            btn_loadDefaultData.IsEnabled = false;
            btn_loadDormData.IsEnabled = false;
            btng_loadData.Visibility = Visibility.Collapsed;

            if (item != null)
            {
                lbl_InternalSelected.Text = item.ToolTip;
                lbl_InternalSelected.Foreground = item.Foreground;
                if (!item.ComponentName.Contains("class"))
                {
                    KillEmptyDirectory($@"{AppDir}assets/spine");

                    string[] tagString = new string[12];
                    tagString[0] = item.Tag[0];//displaySwitch
                    tagString[1] = item.Tag[1];//content.name;
                    tagString[2] = item.Tag[2];//content.parent;
                    tagString[3] = item.Tag[3];//content.type;
                    tagString[4] = item.Tag[4];//content.display;
                    tagString[5] = item.Tag[5];//content.display_full;
                    tagString[6] = item.Tag[6];//content.path;
                    tagString[7] = item.Tag[7];//content.filename;
                    tagString[8] = item.Tag[8];//content.cg;
                    tagString[9] = item.Tag[9];//content.cg_d;
                    tagString[10] = item.Tag[10];//content.filename_r;
                    tagString[11] = item.Tag[11];//content.files;

                    lblSelectedItem.Content = tagString[1].Replace("_", "__");
                    lblSelectedItem.Foreground = item.Foreground;

                    if (tagString[8] != null)
                    {
                        btn_loadCG.IsEnabled = true;
                        chb_save_cg.IsEnabled = true;
                        string cg_filename = tagString[8];
                        if ((bool)chb_preview_d.IsChecked) //默认大破立绘
                        {
                            if (tagString[9] != null)
                            {
                                cg_filename = tagString[9];
                            }
                        }
                        string cgURL = $@"{AppDir}assets/pic/{cg_filename}";
                        try
                        {
                            if (File.Exists(cgURL))
                            {
                                img_Preview.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(cgURL, UriKind.Absolute));
                            }
                            else
                            {
                                if ((bool)chb_preview.IsChecked)
                                {
                                    img_Preview.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri($"{Properties.Settings.Default.DownloadSource}pic/{cg_filename}", UriKind.Absolute));
                                }
                            }
                        }
                        catch (Exception)
                        {

                        }
                    }

                    if ((tagString[6] != null) && (tagString[7] != null) && (tagString[11]!=null)) //存在数据
                    {
                        btn_downloadData.IsEnabled = true;
                        btn_downloadData.Visibility = Visibility.Visible;

                        if (Directory.Exists($@"{AppDir}assets/spine/{tagString[6]}"))
                        {
                            bool checkResult = true;
                            foreach (string filename in tagString[11].Split('|'))
                            {
                                if (!File.Exists($@"{AppDir}assets/spine/{tagString[6]}/{filename}"))
                                {
                                    checkResult = false;
                                }
                            }
                            if (checkResult)
                            {
                                btn_downloadData.IsEnabled = false;
                                btn_downloadData.Visibility = Visibility.Visible;
                                //if ((tagString[1] != Properties.Settings.Default.DummyName) && (tagString[1] != App.globalValues.Dummy))
                                //{
                                //    btn_deleteData.IsEnabled = true;
                                //    btn_deleteData.Visibility = Visibility.Visible;
                                //    btn_downloadData.Visibility = Visibility.Collapsed;
                                //}

                                btn_loadData.IsEnabled = true;
                                btn_loadDefaultData.IsEnabled = true;
                                if (tagString[10] != null)
                                {
                                    btng_loadData.Visibility = Visibility.Visible;
                                    btn_loadDormData.IsEnabled = true;
                                }
                            }
                        }
                    }

                }
            }
        }

        private void btn_LoadDummyList_Click(object sender, RoutedEventArgs e)
        {
            btn_LoadDummyList.IsEnabled = false;
            bool DummyListPost = false;
            string DummyListStr = HttpRequestHelper.PostWebRequest(chibiListLink, string.Empty, Encoding.UTF8, ref DummyListPost);
            if (DummyListPost)
            {
                DummyListRoot rt = JsonConvert.DeserializeObject<DummyListRoot>(DummyListStr);

                if (rt.ret != 200) //API请求失败
                {
                    if (File.Exists($"{AppDir}chibi_list.json"))//本地存在即加载本地
                    {
                        LoadDummyList();
                    }
                    else
                    {
                        MessageBoxResult downloadListResult = MessageBox.Show("本地战术人形数据表不存在，且 API 接口调用失败。加载进程已中止。\n是否重试？", "数据表加载失败", MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.Yes);
                        if (downloadListResult == MessageBoxResult.Yes)
                        {
                            btn_LoadDummyList_Click(this, null);
                        }
                    }
                    return;
                }

                if (File.Exists($"{AppDir}chibi_list.json"))//API请求成功，本地存在
                {
                    string str = File.ReadAllText($"{AppDir}chibi_list.json");
                    RootObject rb = JsonConvert.DeserializeObject<RootObject>(str);
                    if (rt.data.uuid != rb.meta.uuid)//有新版本
                    {
                        sp_downloader.Visibility = Visibility.Visible;
                        lbl_loader.Content = "正在更新战术人形数据表";
                        HttpClass.DownloadFile(rt.data.url, $"{AppDir}chibi_list.json", pb_downloader, lbl_downloader);
                        sp_downloader.Visibility = Visibility.Collapsed;
                        btn_LoadDummyList_Click(this, null);
                        return;
                    }
                    else//相同则加载本地
                    {
                        LoadDummyList();
                    }
                }
                else//API成功，本地不存在
                {
                    try
                    {
                        bool downloaded = HttpClass.DownloadFile(rt.data.url, $"{AppDir}chibi_list.json", pb_loader, lbl_loader);
                        if (downloaded)
                        {
                            btn_LoadDummyList_Click(this, null);
                        }
                        else
                        {
                            HandyControl.Controls.Growl.ErrorGlobal("数据表下载失败");
                        }
                    }
                    catch (Exception ex)
                    {
                        HandyControl.Controls.Growl.ErrorGlobal($"获取与更新数据表时出错。\n{ex.Message}");
                    }
                }
            }
            else //API请求失败
            {
                if (File.Exists($"{AppDir}chibi_list.json"))//存在本地即加载本地
                {
                    LoadDummyList();
                }
                else
                {
                    MessageBoxResult downloadListResult = MessageBox.Show("本地数据表不存在，且 API 接口调用失败。加载进程已中止。\n是否重试？", "数据表加载失败", MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.Yes);
                    if (downloadListResult == MessageBoxResult.Yes)
                    {
                        btn_LoadDummyList_Click(this, null);
                    }
                }
            }
            btn_LoadDummyList.IsEnabled = true;
        }
        

        private void btn_loadCG_Click(object sender, RoutedEventArgs e)
        {
            ComponentModel item = SelectedItem;
            string[] tagString = new string[10];
            tagString[0] = item.Tag[0];//displaySwitch
            tagString[1] = item.Tag[1];//content.name;
            tagString[2] = item.Tag[2];//content.parent;
            tagString[3] = item.Tag[3];//content.type;
            tagString[4] = item.Tag[4];//content.display;
            tagString[5] = item.Tag[5];//content.display_full;
            tagString[6] = item.Tag[6];//content.path;
            tagString[7] = item.Tag[7];//content.filename;
            tagString[8] = item.Tag[8];//content.cg;
            tagString[9] = item.Tag[9];//content.cg_d;

            bool cg = false;
            bool cg_d = false;
            bool local_cg = false;
            bool local_cg_d = false;
            
            if (tagString[8] != null)
            { 
                cg = true; 
                local_cg = File.Exists($@"{AppDir}assets/pic/{tagString[8]}");
            }
            if (tagString[9] != null)
            {
                cg_d = true;
                local_cg_d = File.Exists($@"{AppDir}assets/pic/{tagString[9]}");
            }

            if (cg) //存在立绘
            {
                if (cg && cg_d) //同时存在两种立绘
                {
                    if (local_cg && local_cg_d) //本地同时存在两种立绘，直接加载
                    {
                        new GflChibiDesktop.WindowCG().LoadCG(tagString[5], $@"{AppDir}assets/pic/", tagString[8], tagString[9]);
                    }
                    else
                    {
                        if ((bool)chb_save_cg.IsChecked)
                        {
                            DownloadCG(tagString);
                        }
                        else
                        {
                            new GflChibiDesktop.WindowCG().LoadCG(tagString[5], $"{Properties.Settings.Default.DownloadSource}/pic/", tagString[8], tagString[9]);
                        }
                    }
                }
                else //只有一种立绘
                {
                    if (local_cg) //本地存在，直接加载
                    {
                        new GflChibiDesktop.WindowCG().LoadCG(tagString[5], $@"{AppDir}assets/pic/", tagString[8]);
                    }
                    else
                    {
                        if ((bool)chb_save_cg.IsChecked)
                        {
                            DownloadCG(tagString);
                        }
                        else
                        {
                            new GflChibiDesktop.WindowCG().LoadCG(tagString[5], $"{Properties.Settings.Default.DownloadSource}/pic/", tagString[8]);
                        }
                    }
                }
            }
            else
            {
                HandyControl.Controls.Growl.InfoGlobal($"{tagString[5]} ({tagString[1]}) 没有对应的立绘数据。");
            }
        }

        private void DownloadCG(string[] tagString)
        {
            //tagString[0] = item.Tag[0];//displaySwitch
            //tagString[1] = item.Tag[1];//content.name;
            //tagString[2] = item.Tag[2];//content.parent;
            //tagString[3] = item.Tag[3];//content.type;
            //tagString[4] = item.Tag[4];//content.display;
            //tagString[5] = item.Tag[5];//content.display_full;
            //tagString[6] = item.Tag[6];//content.path;
            //tagString[7] = item.Tag[7];//content.filename;
            //tagString[8] = item.Tag[8];//content.cg;
            //tagString[9] = item.Tag[9];//content.cg_d;

            tv_InternalSelector.IsEnabled = false;
            btn_loadCG.IsEnabled = false;
            chb_save_cg.IsEnabled = false;
            btn_downloadData.IsEnabled = false;

            string cg_url = Properties.Settings.Default.DownloadSource + "pic/";

            if (!Directory.Exists($@"{AppDir}assets/pic"))
            {
                Directory.CreateDirectory($@"{AppDir}assets/pic");
            }
            sp_downloader.Visibility = Visibility.Visible;
            pb_loader.IsIndeterminate = false;
            pb_loader.Value = 0;
            pb_loader.Maximum = 1;
            if (tagString[9] != null)
            {
                lbl_loader.Content = $"正在下载 {tagString[5]} 的大破立绘数据";
                pb_loader.Maximum = 2;
                HttpClass.DownloadFile($"{cg_url}/{tagString[9]}", $@"{AppDir}assets/pic/{tagString[9]}", pb_downloader, lbl_downloader);
                pb_loader.Value++;
            }
            lbl_loader.Content = $"正在下载 {tagString[5]} 的立绘数据";
            HttpClass.DownloadFile($"{cg_url}/{tagString[8]}", $@"{AppDir}assets/pic/{tagString[8]}", pb_downloader, lbl_downloader);
            pb_loader.Value++;
            sp_downloader.Visibility = Visibility.Collapsed;
            lbl_loader.Content = "准备就绪";
            pb_loader.IsIndeterminate = true;

            if (tagString[9] != null)
            {
                new GflChibiDesktop.WindowCG().LoadCG(tagString[5], $@"{AppDir}assets/pic/", tagString[8], tagString[9]);
            }
            else
            {
                new GflChibiDesktop.WindowCG().LoadCG(tagString[5], $@"{AppDir}assets/pic/", tagString[8]);
            }

            tv_InternalSelector.IsEnabled = true;
            btn_loadCG.IsEnabled = true;
            chb_save_cg.IsEnabled = true;
            tvAfterSelect();
        }

        /// <summary>
        /// 检测串值是否为合法的网址格式
        /// </summary>
        /// <param name="strValue">要检测的String值</param>
        /// <returns>成功返回true 失败返回false</returns>
        public static bool CheckIsUrlFormat(string strValue)
        {
            return CheckIsFormat(@"(http://)?([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?", strValue);
        }

        /// <summary>
        /// 检测串值是否为合法的格式
        /// </summary>
        /// <param name="strRegex">正则表达式</param>
        /// <param name="strValue">要检测的String值</param>
        /// <returns>成功返回true 失败返回false</returns>
        public static bool CheckIsFormat(string strRegex, string strValue)
        {
            if (strValue != null && strValue.Trim() != string.Empty)
            {
                Regex re = new Regex(strRegex);
                if (re.IsMatch(strValue))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }



        /// <summary>
        /// 删除掉空文件夹
        /// 所有没有子“文件系统”的都将被删除
        /// </summary>
        /// <param name="storagepath"></param>
        private void KillEmptyDirectory(String storagepath)
        {
            if (Directory.Exists(storagepath))
            {
                DirectoryInfo dir = new DirectoryInfo(storagepath);
                DirectoryInfo[] subdirs = dir.GetDirectories("*.*", SearchOption.AllDirectories);
                foreach (DirectoryInfo subdir in subdirs)
                {
                    FileSystemInfo[] subFiles = subdir.GetFileSystemInfos();
                    if (subFiles.Count() == 0)
                    {
                        subdir.Delete();
                    }
                }
            }
        }

        private void btn_loadData_Click(object sender, RoutedEventArgs e)
        {
            ComponentModel item = SelectedItem;
            LoadInternalSpine(item.Tag, false);
        }

        private void btn_loadDormData_Click(object sender, RoutedEventArgs e)
        {
            ComponentModel item = SelectedItem;
            LoadInternalSpine(item.Tag, true);
        }

        private void LoadInternalSpine(string[] tagString, bool dormMode)
        {
            //tagString[0] = $"{displaySwitch}";
            //tagString[1] = content.name;
            //tagString[2] = content.parent;
            //tagString[3] = content.type;
            //tagString[4] = content.display;
            //tagString[5] = content.display_full;
            //tagString[6] = content.path;
            //tagString[7] = content.filename;
            //tagString[8] = content.cg;
            //tagString[9] = content.cg_d;
            //tagString[10] = content.filename_r;
            //tagString[11] = content.files;

            string AtlasFile = $@"assets/spine/{tagString[6]}/{tagString[7]}.atlas";
            string SpineFile = $@"assets/spine/{tagString[6]}/{tagString[7]}.skel";
            string DisplayName = tagString[5];
            if (dormMode)
            {
                if (File.Exists($@"{AppDir}assets/spine/{tagString[6]}/{tagString[10]}.atlas"))
                { AtlasFile = $@"assets/spine/{tagString[6]}/{tagString[10]}.atlas"; }
                if (File.Exists($@"{AppDir}assets/spine/{tagString[6]}/{tagString[10]}.skel"))
                { SpineFile = $@"assets/spine/{tagString[6]}/{tagString[10]}.skel"; }
                DisplayName = $"{tagString[5]} [宿舍]";
            }
            //else
            //{
            //    AtlasFile = $@"assets/spine/{tagString[6]}/{tagString[7]}.atlas";
            //    SpineFile = $@"assets/spine/{tagString[6]}/{tagString[7]}.skel";
            //    DisplayName = tagString[5];
            //}
            //if (File.Exists($@"{AppDir}assets/name.txt")) { File.Delete($@"{AppDir}assets/name.txt"); }
            File.WriteAllText($@"{AppDir}assets/name.txt", DisplayName);
            //if (File.Exists($@"{AppDir}assets/model.conf.json")) { File.Delete($@"{AppDir}assets/model.conf.json"); }
            File.WriteAllText($@"{AppDir}assets/model.conf.json", "{\"skeleton\":\"" + SpineFile + "\",\"type\":\"skel\",\"atlas\":\"" + AtlasFile + "\",\"h\":448,\"w\":448,\"x\":224,\"y\":224}");

            //Process.Start($@"{Path.GetFullPath("..")}/HDTLPanel.exe");
            ModelLoadRequested?.Invoke(new ChibiModelData
            {
                DisplayName = DisplayName,
                SkeletonFile = SpineFile,
                AtlasFile = AtlasFile,
                NewInstance = chb_force_load.IsChecked == true
            });
            tvAfterSelect();
        }


        private void btn_downloadData_Click(object sender, RoutedEventArgs e)
        {
            ComponentModel item = SelectedItem;
            DownloadData(item.Tag);
        }


        private void DownloadData(string[] tagString)
        {
            ComponentModel item = SelectedItem;
            btn_downloadData.IsEnabled = false;
            btn_downloadData.Visibility = Visibility.Visible;
            btn_deleteData.IsEnabled = false;
            btn_deleteData.Visibility = Visibility.Collapsed;
            btn_loadCG.IsEnabled = false;
            chb_save_cg.IsEnabled = false;
            tv_InternalSelector.IsEnabled = false;

            //tagString[0] = $"{displaySwitch}";
            //tagString[1] = content.name;
            //tagString[2] = content.parent;
            //tagString[3] = content.type;
            //tagString[4] = content.display;
            //tagString[5] = content.display_full;
            //tagString[6] = content.path;
            //tagString[7] = content.filename;
            //tagString[8] = content.cg;
            //tagString[9] = content.cg_d;
            //tagString[10] = content.filename_r;
            //tagString[11] = content.files;
            string downloadSource = string.Empty;
            if (Properties.Settings.Default.DownloadSource != string.Empty)
            {
                downloadSource = Properties.Settings.Default.DownloadSource + "spine/" + tagString[6];
            }

            if (tagString[11].Split('|').Count() > 0)
            {
                if (CheckIsUrlFormat(downloadSource))
                {
                    int total_files = tagString[11].Split('|').Count();
                    pb_loader.Value = 0;
                    pb_loader.Maximum = total_files;
                    tii.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;

                    sp_downloader.Visibility = Visibility.Visible;
                    pb_loader.IsIndeterminate = false;
                    lbl_loader.Content = $"正在下载 {tagString[5]}";

                    KillEmptyDirectory($@"{AppDir}assets/spine");

                    foreach (string filename in tagString[11].Split('|'))
                    {
                        lbl_loader.Content = $"正在下载 {tagString[5]}：{filename} ({pb_loader.Value}/{total_files})";

                        if (!Directory.Exists($@"{AppDir}assets/spine/{tagString[6]}"))
                        {
                            Directory.CreateDirectory($@"{AppDir}assets/spine/{tagString[6]}");
                        }

                        HttpClass.DownloadFile($"{downloadSource}/{filename}", $@"{AppDir}assets/spine/{tagString[6]}/{filename}", pb_downloader, lbl_downloader);

                        pb_loader.Value++;
                        tii.ProgressValue = pb_loader.Value / pb_loader.Maximum;
                    }

                    lbl_loader.Content = $"已完成下载 {tagString[5]}，等待下一步操作...";
                    pb_loader.IsIndeterminate = true;
                    sp_downloader.Visibility = Visibility.Collapsed;
                    tii.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Indeterminate;

                }
                else
                {
                    HandyControl.Controls.Growl.ErrorGlobal("战术人形数据下载失败。\nURL 无效。请检查下载源设置。");
                }
            }
            else
            {
                HandyControl.Controls.Growl.ErrorGlobal("战术人形数据下载失败。\n服务器端未包含该人形的有效数据。");
            }

            tv_InternalSelector.IsEnabled = true;
            
            tvAfterSelect();
        }

        private void btn_deleteData_Click(object sender, RoutedEventArgs e)
        {
            ComponentModel item = SelectedItem;
            string[] tagString = new string[8];
            tagString[0] = item.Tag[0];//displaySwitch
            tagString[1] = item.Tag[1];//content.name;
            tagString[2] = item.Tag[2];//content.parent;
            tagString[3] = item.Tag[3];//content.type;
            tagString[4] = item.Tag[4];//content.display;
            tagString[5] = item.Tag[5];//content.display_full;
            tagString[6] = item.Tag[6];//content.path;
            tagString[7] = item.Tag[7];//content.filename;
            DeleteData(tagString[1]);
        }

        private void DeleteData(string dummy)
        {
            if (Directory.Exists($@"{AppDir}assets/spine/{dummy}"))
            {
                Directory.Delete($@"{AppDir}assets/spine/{dummy}", true);
            }
            tvAfterSelect();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //e.Cancel = true;
            //Hide();
        }

        private void tv_InternalSelector_Unselected(object sender, RoutedEventArgs e)
        {
            SelectedItem = null;
            btn_downloadData.IsEnabled = false;
            btn_downloadData.Visibility = Visibility.Visible;
            btn_deleteData.IsEnabled = false;
            btn_deleteData.Visibility = Visibility.Collapsed;

            btn_loadCG.IsEnabled = false;
            chb_save_cg.IsEnabled = false;
            btn_loadData.IsEnabled = false;
            btn_loadDefaultData.IsEnabled = false;
            btn_loadDormData.IsEnabled = false;
            btng_loadData.Visibility = Visibility.Collapsed;
            img_Preview.Source = null;
        }

        private void btn_sources_Click(object sender, RoutedEventArgs e)
        {
            DownloadSources();
        }
        DownloadSourcesDialog _downloadSources = new DownloadSourcesDialog();
        public void DownloadSources()
        {
            HandyControl.Controls.Dialog.Show(_downloadSources);
        }

        private void SearchBar_SearchStarted(object sender, HandyControl.Data.FunctionEventArgs<string> e)
        {
            string queryText = sbQuery.Text;
            if (queryText != string.Empty)
            {
                initializeDataSet.Clear();
                try
                {
                    string str = File.ReadAllText($"{AppDir}chibi_list.json");
                    RootObject rb = JsonConvert.DeserializeObject<RootObject>(str);
                    btn_LoadDummyList.ToolTip = $"当前数据列表版本 {rb.meta.version}";
                    lblListVersion.Text = rb.meta.version;
                    int total = rb.content.Count;

                    int counter = 0;

                    foreach (Content content in rb.content)
                    {
                        counter++;
                        content.type = content.type ?? "";
                        content.display = content.display ?? content.name;
                        content.display_full = content.display_full ?? content.display;

                        if (content.name.Contains(queryText) || content.parent.Contains(queryText) || content.display.Contains(queryText) || content.display_full.Contains(queryText))
                        {
                            try
                            {
                                bool displaySwitch = true;
                                ComponentModel node = new ComponentModel();
                                node.ComponentName = $"dummy_{content.name.Replace(" ", string.Empty)}";
                                node.Header = content.display;
                                node.ComponentID = 100 + counter;
                                string[] tagString = new string[12];
                                tagString[0] = $"{displaySwitch}";
                                tagString[1] = content.name;
                                tagString[2] = content.parent;
                                tagString[3] = content.type;
                                tagString[4] = content.display;
                                tagString[5] = content.display_full;
                                tagString[6] = content.path;
                                tagString[7] = content.filename;
                                tagString[8] = content.cg;
                                tagString[9] = content.cg_d;
                                tagString[10] = content.filename_r;
                                tagString[11] = content.files;
                                node.Tag = tagString;
                                //node.ImageKey = content.type;
                                //node.SelectedImageKey = content.type;
                                node.Foreground = defaultColor;
                                node.ToolTip = content.display_full;
                                if (content.type.Contains("2")) { node.Foreground = type2color; }
                                if (content.type.Contains("3")) { node.Foreground = type3color; }
                                if (content.type.Contains("4")) { node.Foreground = type4color; }
                                if (content.type.Contains("5")) { node.Foreground = type5color; }
                                if (content.type.Contains("6")) { node.Foreground = type6color; }
                                if (content.type.Contains("7")) { node.Foreground = type7color; }

                                node.ParentID = 0;
                                if (content.name == content.parent)
                                {
                                    node.Level = 1;
                                    node.ParentID = 0;
                                    initializeDataSet.Add(node);
                                }
                                else
                                {
                                    node.Level = 2;
                                    node.ParentID = 0;
                                    foreach (ComponentModel item in initializeDataSet)
                                    {
                                        if (item.ComponentName == $"dummy_{content.parent.Replace(" ", string.Empty)}")
                                        {
                                            //node.IsExpanded = true;
                                            node.ParentID = item.ComponentID;
                                        }
                                    }
                                    initializeDataSet.Add(node);
                                }
                            }
                            catch (Exception ex)
                            {
                                HandyControl.Controls.Growl.ErrorGlobal($"构建战术人形数据列表时出错。\n{ex}");
                            }
                        }

                    }

                    //加载数据
                    tv_InternalSelector.ItemsSource = LoadTreeView(0);

                    List<ComponentModel> LoadTreeView(int id)
                    {
                        List<ComponentModel> node = initializeDataSet.FindAll(s => s.ParentID.Equals(id));
                        foreach (var item in node)
                        {
                            item.Children = LoadTreeView(item.ComponentID);
                        }
                        return node;
                    }

                    //});
                    //tv_InternalSelector.Items.Add(treeViewItemTemp);
                }
                catch (Exception ex)
                {
                    HandyControl.Controls.Growl.ErrorGlobal($"加载战术人形数据列表时出错。\n{ex}");
                }
                tvAfterSelect();
            }
            else
            {
                LoadDummyList();
            }
        }

        private void chb_thinList_Click(object sender, RoutedEventArgs e)
        {
            //ResourceDictionary resourceDictionary = new ResourceDictionary();
            //Application.LoadComponent(resourceDictionary, new Uri("pack://application:,,,/HandyControl;component/Themes/Theme.xaml", UriKind.Relative));
            //Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);
            if ((bool)chb_thinList.IsChecked)
            {
                tv_InternalSelector.SetValue(StyleProperty, Application.Current.Resources["TreeView.Small"]);
            }
            else
            {
                tv_InternalSelector.SetValue(StyleProperty, Application.Current.Resources["TreeViewBaseStyle"]);
            }
        }

        AboutDialog _about = new AboutDialog();

        private void chb_preview_d_Click(object sender, RoutedEventArgs e)
        {
            tvAfterSelect();
        }

        private void btnVersion_Click(object sender, RoutedEventArgs e)
        {
            _about.homepageLink = homepageLink;
            _about.updateLink = updateLink;
            _about.donateLink = donateLink;
            HandyControl.Controls.Dialog.Show(_about);
        }
    }
}
