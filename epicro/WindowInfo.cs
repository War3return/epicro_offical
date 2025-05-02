using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace epicro
{   
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public int ProcessId { get; set; }
        public string Title { get; set; }
        public string ClassName { get; set; } // 필요 시

        // 이벤트: 프로세스 종료 시
        public event EventHandler ProcessExited;
        // 이벤트: 같은 이름의 프로세스가 다시 시작되었을 때
        public event EventHandler ProcessRestarted;

        private Process _processMonitor;
        private string _processName;

        public override string ToString()
        {
            return $"{Title} (PID: {ProcessId})";
        }

        /// <summary>
        /// 현재 설정된 ProcessId 프로세스를 모니터링.
        /// 종료되면 ProcessExited 이벤트를, 
        /// 같은 이름의 프로세스가 다시 뜨면 ProcessRestarted 이벤트를 발생시킵니다.
        /// </summary>
        public void StartExitAndRestartMonitoring()
        {
            StopMonitoring();

            try
            {
                _processMonitor = Process.GetProcessById(ProcessId);
                _processName = _processMonitor.ProcessName;
                Handle = _processMonitor.MainWindowHandle;

                _processMonitor.EnableRaisingEvents = true;
                _processMonitor.Exited += OnProcessExited;
            }
            catch (ArgumentException)
            {
                // 이미 종료된 상태라면 즉시 종료 처리
                OnProcessExited(this, EventArgs.Empty);
            }
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            // 1) 종료 알림
            ProcessExited?.Invoke(this, EventArgs.Empty);

            // 2) 기존 모니터링 정리
            _processMonitor.Exited -= OnProcessExited;
            _processMonitor.Dispose();
            _processMonitor = null;

            // 3) 재시작 탐색 비동기 시작
            _ = MonitorRestartLoopAsync();
        }

        /// <summary>
        /// 같은 프로세스 이름을 가진 프로세스가 다시 실행될 때까지 1초 간격으로 탐색.
        /// 발견 시 ProcessRestarted 이벤트를 발생시키고, 다시 종료 모니터링을 재개합니다.
        /// </summary>
        private async Task MonitorRestartLoopAsync()
        {
            while (true)
            {
                // 같은 이름의 프로세스 검색
                var candidates = Process.GetProcessesByName(_processName);
                if (candidates.Length > 0)
                {
                    var restarted = candidates.First();
                    ProcessId = restarted.Id;
                    Handle = restarted.MainWindowHandle;

                    // 재시작 알림
                    ProcessRestarted?.Invoke(this, EventArgs.Empty);

                    // 다시 종료 모니터링 시작
                    StartExitAndRestartMonitoring();
                    break;
                }

                await Task.Delay(1000);
            }
        }

        /// <summary>
        /// 모니터링 중단 및 리소스 정리
        /// </summary>
        public void StopMonitoring()
        {
            if (_processMonitor != null)
            {
                _processMonitor.Exited -= OnProcessExited;
                _processMonitor.Dispose();
                _processMonitor = null;
            }
        }
    }
}
