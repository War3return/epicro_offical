using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace epicro.Helpers
{
    public class TelegramBotService : IDisposable
    {
        private readonly string _botToken;
        private readonly HashSet<long> _chatIds = new HashSet<long>();
        private readonly Action<string> _log;
        private readonly Func<string> _statusProvider;
        private CancellationTokenSource _cts;
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        private int _lastUpdateId = 0;

        public int RegisteredCount => _chatIds.Count;
        public bool IsEnabled { get; set; } = true;

        public TelegramBotService(string savedChatIds, Action<string> log, Func<string> statusProvider)
        {
            _botToken = TelegramConfig.BotToken;
            _log = log;
            _statusProvider = statusProvider;

            if (!string.IsNullOrEmpty(savedChatIds))
            {
                foreach (var part in savedChatIds.Split(','))
                {
                    if (long.TryParse(part.Trim(), out long id))
                        _chatIds.Add(id);
                }
            }
        }

        public void StartPolling()
        {
            if (string.IsNullOrWhiteSpace(_botToken)) return;
            _cts = new CancellationTokenSource();
            Task.Run(() => PollLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        public async Task BroadcastAsync(string message)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(_botToken) || _chatIds.Count == 0) return;
            var tasks = _chatIds.ToList().Select(id => SendAsync(id, message));
            await Task.WhenAll(tasks);
        }

        private async Task PollLoop(CancellationToken token)
        {
            _log?.Invoke("[텔레그램] 봇 폴링 시작");

            // 앱 시작 시 쌓인 이전 메시지 건너뜀
            await SkipPendingUpdatesAsync();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var url = $"https://api.telegram.org/bot{_botToken}/getUpdates?offset={_lastUpdateId + 1}&timeout=25";
                    var response = await _http.GetStringAsync(url);
                    var updates = ParseUpdates(response);

                    foreach (var update in updates)
                    {
                        _lastUpdateId = Math.Max(_lastUpdateId, update.UpdateId);
                        if (update.Message?.Text != null)
                            await ProcessMessage(update.Message.Chat.Id, update.Message.Text.Trim());
                    }
                }
                catch (TaskCanceledException) { break; }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Telegram] Poll error: {ex.Message}");
                    try { await Task.Delay(5000, token); } catch { break; }
                }
            }

            _log?.Invoke("[텔레그램] 봇 폴링 중지");
        }

        private async Task SkipPendingUpdatesAsync()
        {
            try
            {
                var url = $"https://api.telegram.org/bot{_botToken}/getUpdates?offset=-1&timeout=0";
                var response = await _http.GetStringAsync(url);
                var updates = ParseUpdates(response);
                if (updates.Count > 0)
                    _lastUpdateId = updates.Max(u => u.UpdateId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Telegram] SkipPending error: {ex.Message}");
            }
        }

        private async Task ProcessMessage(long chatId, string text)
        {
            var cmd = text.Split(' ')[0].ToLower();
            if (cmd.Contains('@'))
                cmd = cmd.Substring(0, cmd.IndexOf('@'));

            // /chatid 는 누구든 사용 가능 (연동 전 Chat ID 확인용)
            if (cmd == "/chatid")
            {
                await SendAsync(chatId,
                    $"내 Chat ID: {chatId}\n\n" +
                    $"이 번호를 에피크로 → 기타 탭 → 텔레그램 설정창에 입력하세요.");
                return;
            }

            // 나머지 명령어는 등록된 Chat ID만 사용 가능
            if (!_chatIds.Contains(chatId)) return;

            switch (cmd)
            {
                case "/status":
                    var status = _statusProvider?.Invoke() ?? "상태 정보 없음";
                    await SendAsync(chatId, $"📊 현재 상태\n{status}");
                    break;

                case "/help":
                    await SendAsync(chatId,
                        "📋 명령어 목록\n" +
                        "/chatid - 내 Chat ID 확인\n" +
                        "/status - 현재 매크로 상태\n" +
                        "/help - 명령어 목록");
                    break;
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

        private void SaveChatIds()
        {
            Properties.Settings.Default.TelegramChatIds = string.Join(",", _chatIds);
            Properties.Settings.Default.Save();
        }

        private List<TelegramUpdate> ParseUpdates(string json)
        {
            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var settings = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
                    var ser = new DataContractJsonSerializer(typeof(TelegramResponse), settings);
                    var response = (TelegramResponse)ser.ReadObject(ms);
                    return response?.Result ?? new List<TelegramUpdate>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Telegram] Parse error: {ex.Message}");
                return new List<TelegramUpdate>();
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }

    [DataContract]
    internal class TelegramResponse
    {
        [DataMember(Name = "ok")]
        public bool Ok { get; set; }

        [DataMember(Name = "result")]
        public List<TelegramUpdate> Result { get; set; }
    }

    [DataContract]
    internal class TelegramUpdate
    {
        [DataMember(Name = "update_id")]
        public int UpdateId { get; set; }

        [DataMember(Name = "message")]
        public TelegramMessage Message { get; set; }
    }

    [DataContract]
    internal class TelegramMessage
    {
        [DataMember(Name = "chat")]
        public TelegramChat Chat { get; set; }

        [DataMember(Name = "text")]
        public string Text { get; set; }
    }

    [DataContract]
    internal class TelegramChat
    {
        [DataMember(Name = "id")]
        public long Id { get; set; }
    }
}
