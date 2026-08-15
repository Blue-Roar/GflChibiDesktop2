using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace HDTLPanel
{
    static class Ipc
    {
        [DllImport("huMessageQueue.dll")]
        extern public static IntPtr hiMQ_createIPC(uint size);
        [DllImport("huMessageQueue.dll")]
        extern public static void hiMQ_close(IntPtr inst);
        [DllImport("huMessageQueue.dll")]
        extern public static void hiMQ_closeIPC(IntPtr inst);
        [DllImport("huMessageQueue.dll", CharSet = CharSet.Ansi)]
        extern public static string hiMQ_getIPCName(IntPtr name);
        [DllImport("huMessageQueue.dll")]
        extern public static uint hiMQ_get(IntPtr inst);
        [DllImport("huMessageQueue.dll")]
        extern public static uint hiMQ_next(IntPtr inst);
        [DllImport("huMessageQueue.dll")]
        extern public static void hiMQ_begin(IntPtr inst);
        [DllImport("huMessageQueue.dll")]
        extern public static void hiMQ_ensure(IntPtr inst, uint size);
        [DllImport("huMessageQueue.dll")]
        extern public static void hiMQ_end(IntPtr inst, uint size, uint setEvent);
    }

    public class ManagedIpc : IDisposable
    {
        readonly IntPtr ptr;
        private bool disposedValue;
        public EventWaitHandle waitHandle;

        public class IpcReader
        {
            readonly ManagedIpc m;
            uint size = 0;
            uint read = 0;

            public IpcReader(ManagedIpc m)
            {
                this.m=m;
            }

            public int ReadInt()
            {
                if (read + 4 + 4 > size) throw new ArgumentOutOfRangeException();
                int r = Marshal.ReadInt32(Marshal.ReadIntPtr(m.ptr + 24) + (int)read);
                read += 4;
                return r;
            }

            public string ReadString()
            {
                int len = ReadInt();
                byte[] bytes = new byte[len];
                Marshal.Copy(Marshal.ReadIntPtr(m.ptr + 24) + (int)read, bytes, 0, len);
                read += (uint)len;
                return System.Text.Encoding.UTF8.GetString(bytes);
            }

            public bool Next()
            {
                m.waitHandle.Reset();
                size = size == 0 ? Ipc.hiMQ_get(m.ptr) : Ipc.hiMQ_next(m.ptr);
                read = 0;
                return size != 0;
            }

            public bool Wait(int timeout)
            {
                return m.waitHandle.WaitOne(timeout);
            }
        }

        public class IpcWriter : IDisposable
        {
            readonly ManagedIpc m;

            public IpcWriter(ManagedIpc m)
            {
                this.m=m;
                Ipc.hiMQ_begin(m.ptr);
            }

            public void Write(int value)
            {
                Ipc.hiMQ_ensure(m.ptr, 4);
                Marshal.WriteInt32(Marshal.ReadIntPtr(m.ptr + 24), value);
                Marshal.WriteIntPtr(m.ptr + 24, Marshal.ReadIntPtr(m.ptr + 24) + 4);
            }

            public void Write(string value)
            {
                var b = System.Text.Encoding.UTF8.GetBytes(value);
                Ipc.hiMQ_ensure(m.ptr, 4 + (uint)b.Length);
                Write(b.Length);
                Marshal.Copy(b, 0, Marshal.ReadIntPtr(m.ptr + 24), b.Length);
                Marshal.WriteIntPtr(m.ptr + 24, Marshal.ReadIntPtr(m.ptr + 24) + b.Length);
            }

            public void Dispose()
            {
                Ipc.hiMQ_end(m.ptr, 0, 0);
            }
        }

        public ManagedIpc(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(ptr));
            }
            this.ptr=ptr;
            this.waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset, Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(ptr + 32)));
        }

        public static ManagedIpc CreateInstance(uint size)
        {
            ManagedIpc ipc = new(Ipc.hiMQ_createIPC(size));
            return ipc;
        }

        public IpcReader GetReader()
        {
            return new IpcReader(this);
        }

        public IpcWriter BeginWrite()
        {
            return new(this);
        }

        public string GetName()
        {
            return Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(ptr + 40) + 8) ?? throw new NullReferenceException();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                Ipc.hiMQ_closeIPC(ptr);
                // TODO: 将大型字段设置为 null
                disposedValue=true;
            }
        }

        // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        ~ManagedIpc()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
