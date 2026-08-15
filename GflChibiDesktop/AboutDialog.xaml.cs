using Newtonsoft.Json;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using static GflChibiDesktop.WebAPI;

namespace GflChibiDesktop
{
    /// <summary>
    /// AboutWindow.xaml 的交互逻辑
    /// </summary>
    public partial class AboutDialog
    {
        Version version;
        string build;
        Version buildver;
        string content;

        public readonly string productName = ((AssemblyProductAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyProductAttribute))).Product.ToString();
        public readonly string productTitle = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute))).Title.ToString();
        public readonly string productDescription = ((AssemblyDescriptionAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyDescriptionAttribute))).Description.ToString();
        public readonly string productCopyright = ((AssemblyCopyrightAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyCopyrightAttribute))).Copyright.ToString();
        public readonly string productCompany = ((AssemblyCompanyAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyCompanyAttribute))).Company.ToString();
        public readonly Version productVersion = new Version(((AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyFileVersionAttribute))).Version);
        public readonly Version productBuild = Assembly.GetExecutingAssembly().GetName().Version;
        public readonly string currentBuild = ((AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyInformationalVersionAttribute))).InformationalVersion;
        public string homepageLink { get; set; }
        public string updateLink { get; set; }
        public string donateLink { get; set; }

        public AboutDialog()
        {
            InitializeComponent();
            //MainWindow.AboutWindowState(true);

            //lbl_product.Text = productName;
            lbl_version.Content = $"当前版本：{productVersion} 构建 {currentBuild} {productBuild}";

            lbl_status.Content = string.Empty;
            txt_description.Text = string.Empty;
            Check4Update();
            btn_Close.Focus();
            FetchBiliInfo();
        }

        private void Check4Update()
        {
            lbl_status.Content = string.Empty;
            lbl_latest_version.Content = string.Empty;
            txt_description.Text = string.Empty;
            try
            {
                bool DefaultPost = false;
                string DefaultStr = HttpRequestHelper.PostWebRequest("https://api.brightsu.cn/GflChibiDesktop2/update", string.Empty, Encoding.UTF8, ref DefaultPost);
                if (!DefaultPost)
                {
                    HandyControl.Controls.Growl.WarningGlobal($"无法获取版本信息。API 接口调用失败。\n错误：{DefaultStr}");
                    return;
                }
                UpdateRoot rt = JsonConvert.DeserializeObject<UpdateRoot>(DefaultStr);

                if (rt.ret != 200)
                {
                    HandyControl.Controls.Growl.WarningGlobal($"无法获取版本信息。API 接口调用失败。\n错误：API 接口返回了状态码 {rt.ret}");
                    return;
                }

                if ((rt.data.version == null) || (rt.data.buildver == null)) //检查请求
                {
                    lbl_status.Content = "获取版本信息出错，请手动前往更新";
                    btn_Actions.Content = "前往主页";
                }
                else
                {
                    version = new Version(rt.data.version);
                    build = rt.data.build;
                    buildver = new Version(rt.data.buildver);
                    content = rt.data.content;
                    bool urgentUpdate = false;
                    if (rt.data.urgent == 1) { urgentUpdate = true; }

                    lbl_latest_version.Content = $"线上版本：{version} 构建 {build} {buildver}";
                    if (version > productVersion) //大版本号不同
                    {
                        lbl_status.Content = "有大版本更新可用";
                        if (urgentUpdate) { lbl_status.Content = "有重要更新可用"; }
                        txt_description.Text = content;
                        btn_Actions.Content = "前往更新";
                    }
                    else //大版本号相同
                    {
                        if (buildver > productBuild)
                        {
                            lbl_status.Content = "有构建版本更新可用";
                            if (urgentUpdate) { lbl_status.Content = "有重要更新可用"; }
                            txt_description.Text = content;
                            btn_Actions.Content = "前往更新";
                        }
                        else
                        {
                            lbl_status.Content = "当前是最新版本！";
                            btn_Close.Focus();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lbl_status.Content = "未能获取到最新版本信息";
                txt_description.Text = $"检查更新错误：{ex.Message}";
                btn_Actions.Content = "前往主页";
            }
        }

        private void btn_Actions_Click(object sender, RoutedEventArgs e)
        {
            if (btn_Actions.Content.ToString() == "前往主页")
            {
                System.Diagnostics.Process.Start(homepageLink);
            }
            else if (btn_Actions.Content.ToString() == "前往更新")
            {
                System.Diagnostics.Process.Start(updateLink);
            }
        }
        private void FetchBiliInfo()
        {
            try
            {
                string IndexStr = HttpRequestHelper.GetWebRequest("https://api.bilibili.com/x/space/acc/info?mid=13827887", Encoding.UTF8);
                Console.WriteLine(IndexStr);
                BiliSpaceInfoRoot rt = JsonConvert.DeserializeObject<BiliSpaceInfoRoot>(IndexStr);
                name_BrightSu.Text = rt.data.name;
                avatar_BrightSu.Source = new BitmapImage(new Uri(rt.data.face, UriKind.Absolute));

                IndexStr = HttpRequestHelper.GetWebRequest("https://api.bilibili.com/x/space/acc/info?mid=102421353", Encoding.UTF8);
                Console.WriteLine(IndexStr);
                rt = JsonConvert.DeserializeObject<BiliSpaceInfoRoot>(IndexStr);
                name_Huix.Text = rt.data.name;
                avatar_Huix.Source = new BitmapImage(new Uri(rt.data.face, UriKind.Absolute));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }
        }

        private void btn_BrightSu_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://space.bilibili.com/13827887/");
        }

        private void btn_Huix_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://space.bilibili.com/102421353/");
        }

        int easterCount = 0;
        readonly string[] hk416voices = { "早上好，指挥官。今天的我，也不会输给任何人的。", "指挥官，有我在就足够了。", "那就请好好检查吧，指挥官。就算是细节的问题，也请不要忽略掉。", "指挥官，这段时间过得怎么样？不开心的话，我会不择手段让你笑出来……您有更好的提议吗？是什么呢？", "看到了吗，是我赢了，无论是战绩还是指挥官……", "那么指挥官，我会为您布置好一切的，包括她们能给您的……和不能给您的。", "新的武器制造完毕，没有差错。", "一步一步的改进……直到彻底超越她们。", "增员吗？我的价值，终于被认可了是吗？", "情报确认完毕，出发。", "这才是精准无误的行动。" };
        readonly string[] ump45voices = { "UMP45，来了哦。指挥官，你是想和我好好相处的，对吗♪", "指挥官，你知道我在等什么吧？", "好吧，请对我温柔一点，这对您的鼻子有好处哦，嘻嘻♪", "我刚才出去了很久吗？想我了吗？那么表情不再真诚点吗……对，就是这个傻兮兮的样子，我才能放心呢，哼哼……", "明知道我是这个样子，还想要接纳我吗？\n想改变我？别妄想啦。\n和我在一起，您还是担心一下今后的日子吧，嘻嘻♪", "哎呀，准备好好工作了吗？希望来得及呢，嘻嘻♪", "又多了一个？嗯……多了就多了吧……", "嘻嘻♪马马虎虎的结果吧，能继续前进了吗？", "嘻嘻♪偶尔也让我放个假嘛。", "这……似乎也不是常有的事呢，对吧……嘻嘻♪", "哼，终于遇到了……真正想抹杀掉的家伙。", "我才不是小孩子呢，不许塞糖果给我！诶？那、那就全部拿给9吗？……不许不许塞糖果给我！", "指挥官还真是笨手笨脚呢，真是的，大人可是要既理智又坚定的哦？即使放一堆玩具在这里我也不会看一眼的！我……我喜欢这个兔子，给我啦！", "鼻子？诶，指挥官您在看什么？刚刚的甜点沾到我的鼻子上了吗？", "不要总是跟我搭话啦，我也要思考自己的事呢……比如，怎么捉弄您之类的。" };
        readonly string[] ump9voices = { "指挥官！……嘻嘻♪没事，就是想叫你一下。", "45姐，是你在后面吗？……呃……还是指挥官呢？", "UMP9，正式就任啦！指挥官，今后我们就是一家人了，别和我客气哦！", "干嘛，指挥官！我不是你想象中的那种人！……至少……现在还不是……", "指挥官，您要出门吗？作为家人的我，是不是该送个“再见之吻”呢？你刚才期待了吧！眼神都变了吧！", "这份温暖的触感……就是“拥抱”吗？\n指挥官，没想到我们的关系，还能再进一步呢……\n感谢你，现在，我们就是真正的家人了……", "早啊，指挥官。今天有什么安排，要早点和我说哦。", "任务成功！嘻嘻♪指挥官，你要怎么奖励我呢？", "指挥官指挥官，要一起开小火车吗？我来做火车头哦！诶，不想玩吗？指挥官真是扫兴呀……那就你来做火车头好了嘛！", "咦？带着狗狗包出门会很幼稚吗？不过我本来就是小孩子嘛，我才不要长大呢，大人的世界实在是太麻烦啦。", "呜啊！这比玩具贵好多啊……\n那……以后不光有45姐，还有指挥官照顾我了呢！\n要记得每天帮我准备早餐哦！诶？替小妹妹做这种事不是理所当然嘛？嘻嘻~", "指挥官，你醒着吗？指挥官，现在你还醒着吗？……唔，45姐说等你睡着了就可以给你画花脸了……啊不对，45姐说我不能说是45姐说的！", "指挥官你快看！45姐的小裙子转圈圈的时候会飞起来呢！人家也想穿小裙子嘛。" };
        readonly string[] g11voices = { "指挥官，今天不太忙吧，我可以先睡会儿吧……", "G11……指挥官，这里的床位还够吗？", "请整理下房间吧，不然就只能睡地板了。", "呜……别动手啊……我知道了，马上起来……", "啊……指挥官，我要的抱枕呢？……没有吗？……那你现在忙吗？", "指挥官，这么晚了叫出来我干什么？\n难道这是传说中的……告白？看你的表情这么真诚，不答应的话，我都没法安心睡觉吧……\n好吧，下不为例哦。", "哇，这样是不是可以偷懒了？", "唔，再让我睡会儿~", "呼啊~好困……", "不去不行吗？", "任务完成，呼……这样就能歇会儿了吧……", "睡一觉很快就会结束了吧。" };
        readonly string[] m4a1voices = { "您就是指挥官吗，接下来的事，就拜托您了。", "指挥官，今天的作战计划在这里，我依然会全心地相信您的判断，一起为大家迎接胜利吧。", "指挥官，能永远在您身边战斗，是我的荣幸……\n嗯，您懂我的意思吧？接下来的时光里，请让我一直陪伴您吧。", "M16，我终于……也能帮上忙了。", "我想……变得更强……", "了解……下次，我一定会注意的。", "指挥官，感谢您这段时间一直陪伴着我……\n再这样消沉下去……我可能什么都做不到。\n是您的心声令我重新振作了起来，我会重新面对自己的，请放心吧。", "指挥官，要看看我对作战方案的建议吗？……嗯，偶尔也可以依靠我的。", "指挥官……接下来的事，就拜托您了。", "早，指挥官。没事的话，我继续工作了。", "这么闲能去找其他人吗？我还有训练要做。", "对一下口令，指挥官？" };
        readonly string[] m16a1voices = { "哦，回来了啊！一起来喝一杯吧！", "嘿！我是M16A1。有什么任务，尽管交给我！", "我的爱好？当然是杰克丹尼威士忌啦。", "指挥官，我就算了，我的妹妹可不许你乱碰哦。", "眼睛什么的，有一只就足够啦，毕竟我是百发百中的嘛。", "工作之后一起喝一杯吧，长官。不过，这次不准偷偷告诉M4了，不然挨训的可是我呢。", "哦？长官，您终于发现我的优点啦。\n不过……呃……这么近还真不习惯。\n我——我还是先把这个消息 告诉给M4吧？", "想要给我礼物的话送我酒就行了。", "偶尔也要补充库存啊，不管是人还是啤酒。", "大干一场吧！", "好！快点搞定任务回来喝酒！", "哈哈！今晚要喝个不醉不归哦！", "我回来啦，指挥官。准备好酒了吗？" };
        readonly string[] ar15voices = { "指挥官，我还未收到今天的行动指令。我随时都可以行动的。", "什么时候……才能轮到我呢？", "指挥官，你在看哪里，不、不是商标吧？", "不要以为我是民用武器就可以……总之！在我生气之前，请停手吧！", "指挥官，我今天的表现还可以吧？大家又离各自的目标近了一步呢，总有一天，我们的理想……都会实现的。", "指挥官……这是梦吗？(笑)\n我之前的迷茫……到底是为了什么呢？\n请让我一直看着您吧，因为一闭上眼睛……我就担心自己会醒来……", "这就是我一直渴望的力量吗……", "傀儡增加多少都无所谓，因为她们只会按照我的意志而行动罢了。", "指挥官，不管我变成了什么样子……我只希望能得到您的认可。", "指挥官，这次的胜利……足以称得上“优秀”了吧？", "我渴望的荣誉……就在这场战斗里！" };
        readonly string[] sopmodvoices = { "呜啊——指挥官你来啦！快点快点，开始新一轮作战吧！", "M4 SOPMOD II，指挥官，终于……终于……找到您了啊！", "指挥官，这次要玩什么啊？", "什么什么！指挥官，让我也看看呀！", "虽然您做什么都可以啦，不过碰到什么地方，会把我变成不好的样子……也难说哦。", "指挥官，我的新工艺品怎么样？无论收集还是制作都很辛苦呢，不过我们都是乐在其中啊，嘿嘿……", "嘻嘻嘻嘻……我可是等了很久呢，现在您不得不承认，我们是一路的货色了吧？\n请再抱紧我一点吧，连同我的一切，再抱紧一点……", "哈哈哈，新的伙伴哦，来握个手吧。", "超 进 化！圣——诞——树——！", "讨厌啦指挥官，这样下去，不是会结束得更快嘛！", "等一下嘛，再陪我玩一会儿嘛指挥官。", "嘻嘻嘻……开始吧，快开始吧！", "找·到·你·了·哦！", "这就坏掉了吗……也没什么了不起的嘛……游戏结束。", "呜哇~指挥官快看快看！还认得出我是谁吗？", "指挥官~下次回来给您看看更好玩的纪念品吧~", "控制情绪什么的我是不太懂啦，大家尽情地开心不就很好了吗？", "呜哈哈~想和我玩吗？那我就不客气啦！~", "卟卟卟砰！~樱~桃~炸弹！" };
        readonly string[] ro635voices = { "早上好，长官，这个是今天的报表，请过目。", "初次见面，长官。RO635正在待命，等候你的差遣。", "要出任务吗，长官？", "快把这张可笑的照片拿掉吧！不就是游戏赢了我一次吗，下次我可不会放水了！", "为什么会选择我，您一定有自己的理由。\n而我的信念也终于获得了您的认可，不过……\n能不能别靠得这么近，大家……大家都在看着呢……", "细数你的罪孽吧！", "备用零件多了一点，M16，你要吗？", "在前面等着你们的只有地狱。" };


        private void EasterEgg(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {

            if (easterCount < 5)
            {
                easterCount++;
                System.Media.SystemSounds.Beep.Play();
            }
            else
            {
                Random rand = new Random();
                int n = rand.Next(1, 10);
                string[] voices;
                switch (n)
                {
                    case 1:
                        voices = hk416voices;
                        break;
                    case 2:
                        voices = ump45voices;
                        break;
                    case 3:
                        voices = ump9voices;
                        break;
                    case 4:
                        voices = g11voices;
                        break;
                    case 5:
                        voices = m4a1voices;
                        break;
                    case 6:
                        voices = m16a1voices;
                        break;
                    case 7:
                        voices = ar15voices;
                        break;
                    case 8:
                        voices = sopmodvoices;
                        break;
                    case 9:
                        voices = ro635voices;
                        break;
                    default:
                        voices = hk416voices;
                        break;
                }
                int i = rand.Next(1, voices.Count() - 1);
                //tbtn_easter.IsEnabled = false;
                //tbtn_easter.Description = "你已经戳过了";

                HandyControl.Controls.Growl.InfoGlobal($"“{voices[i]}”");
            }
        }
    }
}
