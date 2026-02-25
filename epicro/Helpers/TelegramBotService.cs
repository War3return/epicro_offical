using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace epicro.Helpers
{
    // Railway 봇 서버의 /notify 엔드포인트를 통해 텔레그램 알림 전송
    public class TelegramBotService : IDisposable
    {
        private string _notifyUrl;
        private string _notifyToken;
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        public bool IsEnabled { get; set; } = true;

        public TelegramBotService()
        {
            _notifyUrl = Properties.Settings.Default.RailwayNotifyUrl?.Trim() ?? "";
            _notifyToken = Properties.Settings.Default.RailwayNotifyToken?.Trim() ?? "";
        }

        public async Task BroadcastAsync(string message)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(_notifyUrl)) return;

            try
            {
                var url = _notifyUrl.TrimEnd('/') + "/notify";
                var json = $"{{\"token\":{JsonString(_notifyToken)},\"message\":{JsonString(message)}}}";
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync(url, content);
                if (!resp.IsSuccessStatusCode)
                    Debug.WriteLine($"[Telegram] Notify 실패: {resp.StatusCode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Telegram] Notify 오류: {ex.Message}");
            }
        }

        public void UpdateConfig(string notifyUrl, string notifyToken)
        {
            _notifyUrl = notifyUrl?.Trim() ?? "";
            _notifyToken = notifyToken?.Trim() ?? "";
        }

        private static string JsonString(string s)
        {
            if (s == null) return "null";
            var escaped = s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                           .Replace("\n", "\\n").Replace("\r", "\\r");
            return $"\"{escaped}\"";
        }

        public void Dispose() { }
    }
}
