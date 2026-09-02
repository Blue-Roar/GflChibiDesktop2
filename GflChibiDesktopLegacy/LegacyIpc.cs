using System;
using System.Collections.Generic;
using System.Threading;

namespace GflChibiDesktop
{
    /// <summary>
    /// 与 V2 主程序通信的控制面板（复用 luajit 的 huMessageQueue 面板协议）。
    /// 子进程→主程序消息类型：0=重置面板, 1=数字, 2=布尔, 3=只读文本, 4=按钮, 5=下拉选择。
    /// 主程序→子进程：id==2 面板配置（[itemId, value]…, 0），id==3 重建面板。
    /// </summary>
    public class LegacyIpc : IDisposable
    {
        private readonly ManagedIpc readInst;   // 读 V2 发来的命令
        private readonly ManagedIpc writeInst;  // 写面板结构给 V2
        private readonly List<PanelItem> panel = new();
        private readonly Action<Action> dispatcher;  // 把面板操作派发到 UI 线程
        private Thread? thread;
        private volatile bool running;

        public LegacyIpc(string readName, string writeName, Action<Action> dispatcher)
        {
            readInst = ManagedIpc.OpenByName(readName);
            writeInst = ManagedIpc.OpenByName(writeName);
            this.dispatcher = dispatcher ?? (a => a());
        }

        // ---------- 注册面板项（index 从 1 开始，与 V2 面板顺序一致） ----------

        public void AddNumeric(string prompt, string hint, Func<int> get, Action<int> set, int min = int.MinValue, int max = int.MaxValue)
            => panel.Add(new NumericItem(panel.Count + 1, prompt, hint, get, set, min, max));

        public void AddBool(string prompt, string hint, Func<bool> get, Action<bool> set)
            => panel.Add(new BoolItem(panel.Count + 1, prompt, hint, get, set));

        public void AddCombo(string prompt, string hint, Func<IReadOnlyList<string>> getItems, Func<int> getIndex, Action<int> setIndex)
            => panel.Add(new ComboItem(panel.Count + 1, prompt, hint, getItems, getIndex, setIndex));

        public void AddReadonly(Func<string> getText)
            => panel.Add(new ReadonlyItem(panel.Count + 1, getText));

        public void AddButton(string prompt, string hint, Action action)
            => panel.Add(new ButtonItem(panel.Count + 1, prompt, hint, action));

        public void Start()
        {
            running = true;
            thread = new Thread(Loop) { IsBackground = true };
            thread.Start();
            SendPanelStructure();
        }

        public void Stop()
        {
            running = false;
            try
            {
                thread?.Join(500);
            }
            catch
            {
            }
        }

        private void Loop()
        {
            int idleErrors = 0;
            while (running)
            {
                try
                {
                    var reader = readInst.GetReader();
                    // 轮询 hiMQ_get 检查命令（主程序发送命令不携带事件唤醒信号，
                    // 不能依赖 WaitOne；与 luajit 每帧 hiMQ_get 轮询一致）
                    while (reader.Next())
                    {
                        idleErrors = 0;
                        try
                        {
                            int id = reader.ReadInt();
                            if (id == 2)
                            {
                                // 面板配置：[itemId, value]…, 0；应用与面板重建须在 UI 线程
                                Run(() =>
                                {
                                    try
                                    {
                                        int itemId = reader.ReadInt();
                                        while (itemId > 0)
                                        {
                                            if (itemId <= panel.Count)
                                            {
                                                panel[itemId - 1].Apply(reader);
                                            }
                                            else
                                            {
                                                Console.WriteLine("[ipc] 忽略越界面板项 itemId=" + itemId);
                                            }
                                            itemId = reader.ReadInt();
                                        }
                                        // 应用后重建面板，让主程序显示最新值（与 luajit 一致）
                                        SendPanelStructure();
                                    }
                                    catch (Exception ex)
                                    {
                                        // 单次配置应用失败不退出 IPC 线程：记录并尝试重建面板恢复同步
                                        Console.WriteLine("[ipc] 面板配置应用失败: " + ex);
                                        try { SendPanelStructure(); } catch { }
                                    }
                                });
                            }
                            else if (id == 3)
                            {
                                // 重建面板
                                Run(SendPanelStructure);
                            }
                        }
                        catch (Exception ex)
                        {
                            // 单条消息解析出错：跳过该条，继续响应后续命令
                            Console.WriteLine("[ipc] 消息解析失败: " + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    idleErrors++;
                    Console.WriteLine("[ipc] 轮询异常(" + idleErrors + "): " + ex.Message);
                    // 仅持续故障才放弃（防死循环空转），偶发错误继续轮询
                    if (idleErrors > 200)
                    {
                        Console.WriteLine("[ipc] 持续轮询失败，IPC 线程退出");
                        break;
                    }
                }
                Thread.Sleep(50);
            }
        }

        private void Run(Action action)
        {
            dispatcher(action);
        }

        public void SendPanelStructure()
        {
            try
            {
                // 重置面板
                using (var w = writeInst.BeginWrite())
                {
                    w.Write(0);
                }
                // 各项：最后一项带唤醒信号（bell=true）
                for (int i = 0; i < panel.Count; i++)
                {
                    var w = writeInst.BeginWrite();
                    panel[i].Send(w);
                    w.End(i == panel.Count - 1);
                }
            }
            catch (Exception ex)
            {
                // 面板结构发送失败不影响后续（下次重建再试）
                Console.WriteLine("[ipc] 面板结构发送失败: " + ex.Message);
            }
        }

        public void Dispose()
        {
            Stop();
            readInst.Dispose();
            writeInst.Dispose();
            GC.SuppressFinalize(this);
        }

        abstract class PanelItem
        {
            public readonly int Index;
            protected PanelItem(int index) => Index = index;
            public abstract void Send(ManagedIpc.IpcWriter w);
            public virtual void Apply(ManagedIpc.IpcReader r) { }
        }

        class NumericItem : PanelItem
        {
            private readonly string prompt, hint;
            private readonly Func<int> get;
            private readonly Action<int> set;
            private readonly int min, max;

            public NumericItem(int index, string prompt, string hint, Func<int> get, Action<int> set, int min, int max)
                : base(index)
            {
                this.prompt = prompt; this.hint = hint; this.get = get; this.set = set; this.min = min; this.max = max;
            }

            public override void Send(ManagedIpc.IpcWriter w)
            {
                w.Write(1);
                w.Write(prompt);
                w.Write(hint);
                w.Write(1);
                w.Write(Math.Max(min, Math.Min(max, get())));
                w.Write(min);
                w.Write(max);
            }

            public override void Apply(ManagedIpc.IpcReader r)
            {
                int v = r.ReadInt();
                if (v < min) v = min;
                if (v > max) v = max;
                set(v);
            }
        }

        class BoolItem : PanelItem
        {
            private readonly string prompt, hint;
            private readonly Func<bool> get;
            private readonly Action<bool> set;

            public BoolItem(int index, string prompt, string hint, Func<bool> get, Action<bool> set)
                : base(index)
            {
                this.prompt = prompt; this.hint = hint; this.get = get; this.set = set;
            }

            public override void Send(ManagedIpc.IpcWriter w)
            {
                w.Write(2);
                w.Write(prompt);
                w.Write(hint);
                w.Write(get() ? 1 : 0);
            }

            public override void Apply(ManagedIpc.IpcReader r)
            {
                set(r.ReadInt() != 0);
            }
        }

        class ComboItem : PanelItem
        {
            private readonly string prompt, hint;
            private readonly Func<IReadOnlyList<string>> getItems;
            private readonly Func<int> getIndex;
            private readonly Action<int> setIndex;

            public ComboItem(int index, string prompt, string hint, Func<IReadOnlyList<string>> getItems, Func<int> getIndex, Action<int> setIndex)
                : base(index)
            {
                this.prompt = prompt; this.hint = hint; this.getItems = getItems; this.getIndex = getIndex; this.setIndex = setIndex;
            }

            public override void Send(ManagedIpc.IpcWriter w)
            {
                var items = getItems();
                w.Write(5);
                w.Write(prompt);
                w.Write(hint);
                w.Write(items.Count);
                foreach (string item in items)
                {
                    w.Write(item);
                }
                w.Write(getIndex());
            }

            public override void Apply(ManagedIpc.IpcReader r)
            {
                setIndex(r.ReadInt());
            }
        }

        class ReadonlyItem : PanelItem
        {
            private readonly Func<string> getText;

            public ReadonlyItem(int index, Func<string> getText) : base(index)
            {
                this.getText = getText;
            }

            public override void Send(ManagedIpc.IpcWriter w)
            {
                w.Write(3);
                w.Write(getText() ?? string.Empty);
            }
        }

        class ButtonItem : PanelItem
        {
            private readonly string prompt, hint;
            private readonly Action action;

            public ButtonItem(int index, string prompt, string hint, Action action) : base(index)
            {
                this.prompt = prompt; this.hint = hint; this.action = action;
            }

            public override void Send(ManagedIpc.IpcWriter w)
            {
                w.Write(4);
                w.Write(prompt);
                w.Write(hint);
            }

            public override void Apply(ManagedIpc.IpcReader r)
            {
                action();
            }
        }
    }
}
