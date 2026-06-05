using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace WesQA
{
    /// <summary>TcpListener + [4B LE len][utf-8] 프레이밍 + JSON-RPC 디스패치.
    /// 핸들러는 Unity API 접근을 위해 메인스레드에서 실행되도록 큐잉한다.</summary>
    public class WesPocoServer
    {
        private readonly int _port;
        private TcpListener _listener;
        private Thread _accept;
        private volatile bool _running;
        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private GameObject _pump;

        public WesPocoServer(int port) { _port = port; }

        public void Start()
        {
            _running = true;
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            _accept = new Thread(AcceptLoop) { IsBackground = true };
            _accept.Start();

            _pump = new GameObject("[WesQA.Pump]");
            UnityEngine.Object.DontDestroyOnLoad(_pump);
            _pump.AddComponent<WesQAPump>().Bind(this);
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
        }

        // 메인스레드에서 매 프레임 호출(WesQAPump.Update)
        internal void PumpMainThread()
        {
            while (_mainThread.TryDequeue(out var act)) act();
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    new Thread(() => ClientLoop(client)) { IsBackground = true }.Start();
                }
                catch { if (_running) Thread.Sleep(50); }
            }
        }

        private void ClientLoop(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var header = new byte[4];
                while (_running)
                {
                    if (!ReadExactly(stream, header, 4)) break;
                    int len = BitConverter.ToInt32(header, 0); // 프로토콜이 LE; Unity x64도 LE
                    var body = new byte[len];
                    if (!ReadExactly(stream, body, len)) break;
                    string json = Encoding.UTF8.GetString(body);
                    DispatchOnMainThread(stream, json);
                }
            }
        }

        private void DispatchOnMainThread(NetworkStream stream, string json)
        {
            var done = new ManualResetEventSlim(false);
            string response = null;
            _mainThread.Enqueue(() =>
            {
                RpcRequest req = null;
                try
                {
                    req = RpcRequest.Parse(json);
                    object result = RpcMethods.Invoke(req);
                    response = RpcResponse.Result(req.Id, result);
                }
                catch (Exception e)
                {
                    response = RpcResponse.Error(req != null ? req.Id : null, e.Message);
                }
                done.Set();
            });
            done.Wait();
            Send(stream, response);
        }

        private static void Send(NetworkStream stream, string json)
        {
            var payload = Encoding.UTF8.GetBytes(json);
            var header = BitConverter.GetBytes(payload.Length); // 4B LE
            stream.Write(header, 0, 4);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }

        private static bool ReadExactly(Stream s, byte[] buf, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n = s.Read(buf, got, count - got);
                if (n <= 0) return false;
                got += n;
            }
            return true;
        }
    }

    /// <summary>메인스레드 큐를 매 프레임 비우는 펌프 컴포넌트.</summary>
    public class WesQAPump : MonoBehaviour
    {
        private WesPocoServer _server;
        public void Bind(WesPocoServer s) { _server = s; }
        private void Update() { _server?.PumpMainThread(); }
        private void OnDestroy() { _server?.Stop(); }
    }
}
