using System;
using System.Globalization;
using UnityEngine;

[RequireComponent(typeof(UnityWsServer))]
public sealed class UnityGamaBridge : MonoBehaviour
{
    [Header("Refs")]
    public UnityWsServer server;          // auto-assign from same GameObject
    public Transform probeVisual;         // optional: visualize incoming "cycle"
    public Transform tracked;             // Transform cần gửi tọa độ

    [Header("IDs")]
    public string playerId = "player1";   // id để GAMA cập nhật đúng agent

    [Header("Behavior")]
    public bool sendPing = true;
    public float pingInterval = 1.0f;

    public bool sendPos = true;           // bật/tắt gửi tọa độ
    public float posInterval = 0.10f;     // chu kỳ gửi tọa độ (giây)

    float _tPing;
    float _tPos;

    void Reset()
    {
        server = GetComponent<UnityWsServer>();
    }

    void Awake()
    {
        if (server == null) server = GetComponent<UnityWsServer>();
#if UNITY_2023_1_OR_NEWER
        if (server == null) server = UnityEngine.Object.FindFirstObjectByType<UnityWsServer>(FindObjectsInactive.Exclude);
#elif UNITY_2022_2_OR_NEWER
        if (server == null) server = UnityEngine.Object.FindAnyObjectByType<UnityWsServer>(FindObjectsInactive.Exclude);
#endif
        if (server == null) Debug.LogError("UnityWsServer not found in scene.");
    }

    void Update()
    {
        // Ping đều
        if (sendPing && server != null)
        {
            _tPing += Time.deltaTime;
            if (_tPing >= pingInterval)
            {
                _tPing = 0f;
                string msg = "{\"cmd\":\"ping\",\"ts\":" + Time.realtimeSinceStartup.ToString("0.00", CultureInfo.InvariantCulture) + "}";
                server.Broadcast(msg);
            }
        }

        // Gửi tọa độ tracked
        if (sendPos && server != null && tracked != null)
        {
            _tPos += Time.deltaTime;
            if (_tPos >= posInterval)
            {
                _tPos = 0f;
                BroadcastPosition(tracked, playerId);
            }
        }

        // Rút inbox và xử lý gói từ GAMA
        server?.DrainInbox(OnGamaMessage, 2048);

        // Phím tắt điều khiển
        if (Input.GetKeyDown(KeyCode.P)) server?.Broadcast("{\"cmd\":\"pause\"}");
        if (Input.GetKeyDown(KeyCode.R)) server?.Broadcast("{\"cmd\":\"resume\"}"); // đổi U -> R

    }

    public void SendPause() { server?.Broadcast("{\"cmd\":\"pause\"}"); }
    public void SendResume() { server?.Broadcast("{\"cmd\":\"resume\"}"); }

    public void SendPositionNow()
    {
        if (server != null && tracked != null) BroadcastPosition(tracked, playerId);
    }

    void BroadcastPosition(Transform t, string id)
    {
        // Quy ước: GAMA(x,y) = Unity(x,z); Unity.y giữ trong trường "z"
        var p = t.position;
        string msg =
            "{\"cmd\":\"pos\",\"id\":\"" + EscapeJson(id) + "\"," +
            "\"x\":" + p.x.ToString("0.###", CultureInfo.InvariantCulture) + "," +
            "\"y\":" + p.z.ToString("0.###", CultureInfo.InvariantCulture) + "," +
            "\"z\":" + p.y.ToString("0.###", CultureInfo.InvariantCulture) +
            "}";
        server.Broadcast(msg);
    }

    void OnGamaMessage(string s)
    {
        if (string.IsNullOrEmpty(s)) return;
        Debug.Log("[WS] <- " + s);

        // Demo trực quan: trích "cycle" để scale probeVisual theo trục Y
        if (s.Length > 0 && s[0] == '{')
        {
            double cyc = ExtractNumber(s, "cycle", double.NaN);
            if (!double.IsNaN(cyc) && probeVisual != null)
            {
                float y = Mathf.Clamp((float)(0.2 + 0.01 * cyc), 0.1f, 5f);
                probeVisual.localScale = new Vector3(1f, y, 1f);
            }
        }

        // Phản hồi ACK
        server?.Broadcast("{\"ack\":\"ok\"}");
    }

    static double ExtractNumber(string json, string key, double defv)
    {
        var k = $"\"{key}\":";
        int i = json.IndexOf(k, StringComparison.Ordinal);
        if (i < 0) return defv;
        i += k.Length;
        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;

        if (i < json.Length && json[i] == '"')
        {
            int j = json.IndexOf('"', i + 1);
            if (j < 0) return defv;
            var s = json.Substring(i + 1, j - i - 1);
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var vq) ? vq : defv;
        }

        const string digits = "0123456789+-.eE";
        int k2 = i;
        while (k2 < json.Length && digits.IndexOf(json[k2]) >= 0) k2++;
        var num = json.Substring(i, k2 - i);
        return double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : defv;
    }

    static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
