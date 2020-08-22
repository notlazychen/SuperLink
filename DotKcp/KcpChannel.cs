using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.Sockets.Kcp;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotKcp
{
    public delegate void ReceiveMessageDelegate(Memory<byte> data);
    public delegate void ProcessOutputDelegate(Memory<byte> data);
    public delegate void CloseDelegate();

    public class KcpChannel : IDisposable
    {
        private Kcp _kcp;
        public event ReceiveMessageDelegate MessageReceived;
        public event ProcessOutputDelegate ProcessPost;
        public event CloseDelegate Closed;

        private CancellationTokenSource _cancellationTokenSource;
        private Task _mainTask;

        public KcpChannel(uint conv)
        {
            var handler = new CallbackHandle();
            handler.DoPost = DoPost;
            _kcp = new Kcp(conv, handler);
            _kcp.NoDelay(1, 10, 2, 1);//fast
            _kcp.WndSize(64, 64);
            _kcp.SetMtu(512);
        }

        public void Run()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            //Task.Run(async () =>
            //{
            //    try
            //    {
            //        while (!_cancellationTokenSource.IsCancellationRequested)
            //        {
            //            _kcp.Update(DateTime.UtcNow);
            //            int len;
            //            while ((len = _kcp.PeekSize()) > 0)
            //            {
            //                var buffer = new byte[len];
            //                if (_kcp.Recv(buffer) >= 0)
            //                {
            //                    OnReceive(buffer);
            //                    //handle.Receive(buffer);
            //                }
            //            }
            //            await Task.Delay(5);
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        e.ToString();
            //    }
            //});

            //标准顺序是每次调用了 ikcp_update后，
            //使用 ikcp_check决定下次什么时间点再次调用 ikcp_update，
            //而如果中途发生了 ikcp_send, ikcp_input 的话，在下一轮 interval 立马调用 ikcp_update和 ikcp_check。
            //使用该方法，原来在处理2000个 kcp连接且每 个连接每10ms调用一次update，改为 check机制后，cpu从 60%降低到 15%。
            //_kcp.Update(DateTime.UtcNow);
            //var nextTime = _kcp.Check(DateTime.UtcNow);  
            _mainTask = Task.Run(async () =>
            {
                try
                {
                    DateTime lastTime = DateTime.UtcNow;
                    DateTime nextTime = DateTime.UtcNow;
                    DateTime lastPong = DateTime.UtcNow;
                    while (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        var now = DateTime.UtcNow;
                        if (_needUpdate || (lastTime < nextTime && now > nextTime))
                        {
                            _needUpdate = false;
                            _kcp.Update(now);
                            int len;
                            while ((len = _kcp.PeekSize()) > 0)
                            {
                                var buffer = new byte[len];
                                if (_kcp.Recv(buffer) >= 0)
                                {
                                    OnReceive(buffer);
                                }
                                lastPong = now;
                            }
                            nextTime = _kcp.Check(now);
                            lastTime = now;
                        }

                        //断线检查
                        if((now - lastPong).TotalSeconds > 5)
                        {
                            OnClosed();
                            break;
                        }
                        await Task.Delay(5);
                    }
                }
                catch (Exception e)
                {
                    e.ToString();
                }
            });
        }

        private async void OnClosed()
        {
            await Task.Run(() => Closed?.Invoke());
        }

        private void DoPost(Memory<byte> data)
        {
            ProcessPost?.Invoke(data);
        }

        private void OnReceive(Memory<byte> data)
        {
            MessageReceived?.Invoke(data);
        }

        private bool _needUpdate;
        public virtual void Send(byte[] data)
        {
            _kcp.Send(data);
            _needUpdate = true;
        }
        public void MockInput(Span<byte> span)
        {
            _kcp.Input(span);
            _needUpdate = true;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            Task.WaitAll(_mainTask);
        }
    }


    public class CallbackHandle : IKcpCallback
    {
        public void Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            using (buffer)
            {
                var data = buffer.Memory.Slice(0, avalidLength);
                //var str = Encoding.UTF8.GetString(data.Span);
                DoPost?.Invoke(data);
            }
        }

        public ProcessOutputDelegate DoPost;
    }
}
