using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace epicro.Helpers
{
    public class ProcessMemoryWatcher
    {
        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hProcess);

        private readonly Process _targetProcess;
        private CancellationTokenSource _cts;
        private readonly Action<string> _onUpdateLabel;

        public ProcessMemoryWatcher(Process targetProcess, Action<string> onUpdateLabel)
        {
            _targetProcess = targetProcess;
            _onUpdateLabel = onUpdateLabel;
        }

        public void Start()
        {
            Stop(); // 중복 방지
            _cts = new CancellationTokenSource();
            Task.Run(() => MonitorLoop(_cts.Token));
        }

        public void Stop()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }

        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_targetProcess.HasExited)
                    {
                        Debug.WriteLine($"[종료됨] 프로세스 '{_targetProcess.ProcessName}' 종료 감지됨.");
                        break;
                    }

                    _targetProcess.Refresh(); // 최신 정보 갱신
                    long before = _targetProcess.WorkingSet64;

                    // 메모리 정리 시도
                    EmptyWorkingSet(_targetProcess.Handle);

                    // 🔽 정리 후 메모리 측정
                    _targetProcess.Refresh(); // 최신 정보 갱신
                    long after = _targetProcess.WorkingSet64;


                    var time = DateTime.Now.ToString("HH:mm:ss");
                    var text = $"메모리 정리 {time} {FormatBytes(before)} → {FormatBytes(after)}";

                    _onUpdateLabel?.Invoke(text);

                    Debug.WriteLine($"메모리 정리됨: {FormatBytes(before)} → {FormatBytes(after)} ({FormatBytes(before - after)} 감소)");

                    await Task.Delay(TimeSpan.FromMinutes(5), token);
                }
                catch (TaskCanceledException)
                {
                    Debug.WriteLine("[정지] 메모리 감시 루프가 정상적으로 취소되었습니다.");
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[오류] ProcessMemoryWatcher: {ex.Message}");
                    break;
                }
            }
        }
        private string FormatBytes(long bytes)
        {
            if (bytes >= 1024 * 1024 * 1024)
                return $"{bytes / (1024 * 1024 * 1024.0):F2} GB";
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024 * 1024.0):F2} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F2} KB";
            return $"{bytes} B";
        }
    }
}
