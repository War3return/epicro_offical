using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace epicro.Helpers
{
    // 알림 전송 전용 서비스 (명령어 처리는 Lambda 서버가 담당)
    public class TelegramBotService : IDisposable
    {
        private readonly string _botToken;
        private readonly HashSet<long> _chatIds = new HashSet<long>();
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        public int RegisteredCount => _chatIds.Count;
        public bool IsEnabled { get; set; } = true;

        public TelegramBotService(string savedChatIds, Action<string> log, Func<string> statusProvider)
        {
            _botToken = TelegramConfig.BotToken;

            if (!string.IsNullOrEmpty(savedChatIds))
            {
                foreach (var part in savedChatIds.Split(','))
                {
                    if (long.TryParse(part.Trim(), out long id))
                        _chatIds.Add(id);
                }
            }
        }

        public async Task BroadcastAsync(string message)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(_botToken) || _chatIds.Count == 0) return;
            var tasks = _chatIds.ToList().Select(id => SendAsync(id, message));
            await Task.WhenAll(tasks);
        }

        public async Task SendAsync(long chatId, string message)
        {
            try
            {
                var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("chat_id", chatId.ToString()),
                    new KeyValuePair<string, string>("text", message)
                });
                await _http.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Telegram] Send error: {ex.Message}");
            }
        }

        public void UpdateChatIds(string commaSeparated)
        {
            _chatIds.Clear();
            if (!string.IsNullOrEmpty(commaSeparated))
            {
                foreach (var part in commaSeparated.Split(','))
                {
                    if (long.TryParse(part.Trim(), out long id))
                        _chatIds.Add(id);
                }
            }
        }

        public void Dispose() { }
    }
}
