// Requires WebSocketSharp.dll (place in Assets/Plugins/)
using System;
using System.Collections.Concurrent;
using System.Text;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public sealed class UnityWsServer : MonoBehaviour
{
    [Header("Listen Address")]
    public string host = "127.0.0.1";
    public int port = 3001;
    public bool verbose = true;

    static UnityWsServer _instance;
    WebSocketServer _wss;
    static readonly ConcurrentQueue<string> _inbox = new();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            if (verbose) Debug.LogWarning("[WS] Duplicate UnityWsServer destroyed.");
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            var url = $"ws://{host}:{port}";
            _wss = new WebSocketServer(url);
            _wss.AddWebSocketService<GamaEndpoint>("/", ep => ep.onRecv = OnRecvFromGama);
            _wss.Start();
            if (verbose) Debug.Log($"[WS] Server started: {url}/");
        }
        catch (Exception ex)
        {
            try { _wss?.Stop(); } catch { }
            _wss = null;
            Debug.LogError("[WS] Start failed: " + ex);
        }
    }

    void OnDisable()
    {
        try { _wss?.Stop(); } catch { }
        _wss = null;
    }

    void OnApplicationQuit()
    {
        try { _wss?.Stop(); } catch { }
        _wss = null;
    }

    void OnRecvFromGama(string msg)
    {
        if (verbose) Debug.Log("[WS] <- " + (msg ?? ""));
        _inbox.Enqueue(msg ?? string.Empty);
    }

    public int DrainInbox(Action<string> handler, int maxPerFrame = 1024)
    {
        if (handler == null) return 0;
        int n = 0;
        while (n < maxPerFrame && _inbox.TryDequeue(out var s))
        {
            n++;
            handler(s);
        }
        return n;
    }

    public void Broadcast(string s)
    {
        _wss?.WebSocketServices["/"]?.Sessions?.Broadcast(s ?? string.Empty);
        if (verbose) Debug.Log("[WS] -> " + (s ?? ""));
    }

    public sealed class GamaEndpoint : WebSocketBehavior
    {
        public Action<string> onRecv;

        protected override void OnOpen()
        {
            Debug.Log("[WS] GAMA connected: " + ID);
            base.OnOpen();
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Debug.Log("[WS] GAMA closed: " + e.Reason);
            base.OnClose(e);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            string msg = e.IsText
                ? e.Data
                : (e.RawData != null && e.RawData.Length > 0 ? Encoding.UTF8.GetString(e.RawData) : string.Empty);
            onRecv?.Invoke(msg);
        }
    }
}
