using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GflChibiDesktop2
{
    internal class ProcessManager : IDisposable
    {
        public readonly Process process;
        public event EventHandler? Exited;
        public ManagedIpc txIpc, rxIpc;
        public CancellationTokenSource cancellationTokenSource = new();
        public Task updateTask;
        public event Action? OnReceiveIpcMessage;

        private bool disposedValue;

        readonly private StreamWriter outWriter, errWriter;

        public ProcessManager(string exeName, string workingDirectory, string arguments, Action? onReceiveIpcMessage)
        {
            txIpc = ManagedIpc.CreateInstance(16 * 1024);
            rxIpc = ManagedIpc.CreateInstance(16 * 1024);
            ProcessStartInfo processStartInfo = new(exeName, arguments + " " + txIpc.GetName() + " " + rxIpc.GetName())
            {
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            // 确保工作目录存在（File.OpenWrite 不会自动创建父目录）
            Directory.CreateDirectory(workingDirectory);
            outWriter = new StreamWriter(File.OpenWrite(Path.Combine(workingDirectory, "out.log")));
            errWriter = new StreamWriter(File.OpenWrite(Path.Combine(workingDirectory, "err.log")));

            process = Process.Start(processStartInfo) ?? throw new Exception("Failed to start process");
            process.Exited += Process_Exited;
            process.EnableRaisingEvents = true;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.OutputDataReceived += (_, e) => outWriter.WriteLine(e.Data);
            process.ErrorDataReceived += (_, e) => errWriter.WriteLine(e.Data);

            if (onReceiveIpcMessage is not null)
            {
                OnReceiveIpcMessage += onReceiveIpcMessage;
            }

            var token = cancellationTokenSource.Token;
            updateTask = Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (rxIpc.waitHandle.WaitOne(100))
                        {
                            rxIpc.waitHandle.Reset();
                            OnReceiveIpcMessage?.Invoke();
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
            });
        }

        public void TryCloseWindow()
        {
            process.CloseMainWindow();
        }

        public void ForceCloseWindow()
        {
            process.Kill(true);
        }

        private void Process_Exited(object? sender, EventArgs e)
        {
            outWriter.Dispose();
            errWriter.Dispose();
            Exited?.Invoke(this, e);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // 释放托管状态(托管对象)
                    cancellationTokenSource.Cancel();
                    try
                    {
                        updateTask?.Wait(1000);
                    }
                    catch
                    {
                    }
                    txIpc.Dispose();
                    rxIpc.Dispose();
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~ProcessManager()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
