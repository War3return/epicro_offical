using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Tesseract;
using Windows.Media.Ocr;
using epicro.Helpers;
using epicro.Models;
using System.Windows.Forms;
using SharpDX.Direct3D11;
using Composition.WindowsRuntimeHelpers;
using OpenCvSharp;
using Windows.Media.Capture;
using System.Collections.ObjectModel;
using Windows.Networking;

namespace epicro.Logic
{
    public class BossSummonerWpf
    {
        private CancellationTokenSource cts;
        private readonly Action<string> log;
        //private readonly Dictionary<string, BossConfig> bossConfigs;
        private int previousGold;
        private OcrService ocrService;
        private Texture2D bossMatcher;
        private bool isRunning;
        private int totalWood = 0;
        private DateTime startTime;

        private ObservableCollection<BossStats> _bossStatsList;
        private readonly Action<int, double> updateWoodCallback;
        private System.Timers.Timer elapsedTimer;

        public string BossOrder { get; set; }
        public string BossZone { get; set; }
        public bool IsRunning => isRunning;

        public BossSummonerWpf(Action<string> log, ObservableCollection<BossStats> bossStatsList, Action<int, double> updateWoodCallback)
        {
            Properties.Settings.Default.Reload();
            isRunning = false;
            this.log = log;
            //this.bossConfigs = BossConfigManager.GetBossConfigs();
            this.ocrService = new OcrService(() =>
            {
                var hwnd = MainWindow.TargetWindow?.Handle ?? IntPtr.Zero;

                if (WindowEnumerationHelper.IsWindowMinimized(hwnd))
                {
                    //log("[INFO] 대상 창이 최소화됨 → 자동 복원 시도");
                    WindowEnumerationHelper.RestoreWindow(hwnd);
                    Thread.Sleep(500); // 복원 후 안정 대기
                }

                return MainWindow.backgroundCapture.GetSafeTextureCopy();
            }, MainWindow.ocrEngine);
            ocrService.RefreshFilterSettings();
            this._bossStatsList = bossStatsList;
            this.updateWoodCallback = updateWoodCallback; // 콜백 저장
        }

        public void Start()
        {
            if (isRunning)
            {
                log("[보스소환] 이미 실행 중입니다");
                return;
            }

            if (cts != null)
                return;

            cts = new CancellationTokenSource();
            Task.Run(() => Run(cts.Token));
            log("[보스소환] 자동 소환 시작됨");
        }

        public void Stop()
        {
            if (!isRunning)
            {
                return; // 실행 중이 아니면 아무 것도 하지 않고 종료
            }

            if (cts != null)
            {
                cts.Cancel();
                cts = null;
                isRunning = false;
                log("[보스소환] 자동 소환 중지됨");

                // OCR 타이머도 종료
                //ocrService?.Stop();
            }
            if (elapsedTimer != null)
            {
                elapsedTimer.Stop();
                elapsedTimer.Dispose();
                elapsedTimer = null;
            }
        }

        private void Run(CancellationToken token)
        {
            //----------------------------------------------------초기화
            totalWood = 0;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                updateWoodCallback?.Invoke(totalWood, 0);
            });

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var boss in _bossStatsList)
                {
                    boss.ResetStats(); // 통계만 초기화 (이름은 유지)
                }
            });

            startTime = DateTime.Now;
            StartElapsedTimer();
            isRunning = true;
            var zonesToBosses = BossConfigManager.GetZonesToBosses();
            //----------------------------------------------------------

            var order = BossOrder?.ToUpper().ToCharArray();
            if (order == null || order.Length == 0)
            {
                log("[보스소환] 소환 순서가 비어있습니다");
                return;
            }

            List<string> bossesInZone = zonesToBosses[BossZone];
            List<string> orderedBosses = new List<string>();
            foreach (char bossKey in order)
            {
                // 2단계: 해당 보스 키를 가진 보스를 찾아서 그 보스를 순서대로 매핑
                foreach (var boss in bossesInZone)
                {
                    var bossConfigs = BossConfigManager.GetBossConfigs();
                    if (bossConfigs.ContainsKey(boss) && bossConfigs[boss].Key == bossKey.ToString().ToUpper())
                    {
                        orderedBosses.Add(boss);  // 순서대로 보스를 추가
                        break;  // 한 번에 하나의 보스를 찾으면 그 보스만 추가하고 빠져나옴
                    }
                }
            }
            string selectedROI = Properties.Settings.Default["SelectedROI"] as string;
            string checkRoi;
            if (selectedROI == "gold")
            {
                checkRoi = "roi_gold";
            }
            else
            {
                checkRoi = "roi_tree";
            }

            //초기 값 설정
            int previousValue = ocrService.ReadCurrentValue(checkRoi);
            if (previousValue == -1)
            {
                log("[ERROR] 숫자 초기 값을 가져올 수 없습니다.");
                return;
            }
            while (isRunning)
            {
                try
                {
                    //log("소환 반복문 시작");
                    foreach (var bossName in orderedBosses)
                    {
                        //log("보스 체크 시작");
                        if (token.IsCancellationRequested)
                        {
                            //log("보스소환 반복문 종료. 토큰문제");
                            return;
                        }

                        if (!bossesInZone.Contains(bossName))
                        {
                            log("보스존 체크 실패");
                            continue;
                        }

                        var bossConfigs = BossConfigManager.GetBossConfigs();
                        if (bossConfigs.ContainsKey(bossName) && IsBossMatched(bossConfigs[bossName])) // 이미지 비교함수 추가.
                        {
                            //Console.WriteLine($"[INFO] {bossName} 활성화됨. 소환");
                            SummonBoss(bossName);

                            int startValue = previousValue;
                            DateTime bossStartTime = DateTime.Now;
                            bool valueChanged = false;

                            while ((DateTime.Now - bossStartTime).TotalSeconds < 300)
                            {
                                if (token.IsCancellationRequested) return;
                                int currentValue = ocrService.ReadCurrentValue(checkRoi);
                                if (currentValue == -1)
                                {
                                    continue;
                                }

                                if (currentValue != startValue)
                                {
                                    Console.WriteLine($"[INFO] 골드 변동 감지: {startValue} -> {currentValue}");
                                    previousValue = currentValue;
                                    valueChanged = true;

                                    var killTime = DateTime.Now - bossStartTime;
                                    OnBossKilled(bossName, killTime);
                                    break;
                                }
                                Thread.Sleep(500);
                            }
                            if (!valueChanged)
                            {
                                if (token.IsCancellationRequested) return;
                                log("로그[WARNING] 5분 동안 골드 변동이 없어 다음 보스를 찾습니다");
                            }
                            break;
                        }
                        //Console.WriteLine($"[MEM] {GC.GetTotalMemory(false) / 1024 / 1024} MB");
                    }
                    Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    log($"[ERROR] 보스 소환 중 예외 발생: {ex.Message}");
                    log($"[ERROR] 스택 트레이스: {ex.StackTrace}");
                    break; // 예외 발생 시 반복문 종료
                }
            }
        }
        private void SummonBoss(string bossKey)
        {
            var bossConfigs = BossConfigManager.GetBossConfigs();
            Keys key = GetBossKey(bossKey, bossConfigs);
            //Console.WriteLine($"[INFO] {bossKey} 소환 - {key}");

            InputHelper.SendKey(key.ToString());
        }
        private bool IsBossMatched(BossConfig config)
        {
            try
            {
                //log("이미지매치 실행");
                // 1. GPU 프레임 → Texture2D 복사
                using (var texture = MainWindow.backgroundCapture.GetSafeTextureCopy())
                {
                    if (texture == null)
                    {
                        //log("[WARNING] Texture 복사 실패 (LatestFrameTexture가 null)");
                        return false;
                    }

                    // 2. Texture2D → Bitmap
                    using (var bitmap = Direct3D11Helper.ExtractBitmapFromTexture(texture))
                    {
                        // 3. Bitmap → Mat
                        using (var mat = BossMatcher.Convert(bitmap))
                        {
                            string imageFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
                            return BossMatcher.MatchBossByRoi(mat, config, imageFolder);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log($"[ERROR] 보스 매칭 중 예외 발생: {ex.Message}");
                return false;
            }
        }
        // 보스 키 매핑
        private Keys GetBossKey(string bossName, Dictionary<string, BossConfig> bossConfigs)
        {

            if (bossConfigs.ContainsKey(bossName))
            {
                // 보스의 'Key' 값(예: Q, W, E 등)을 반환
                return (Keys)Enum.Parse(typeof(Keys), bossConfigs[bossName].Key);
            }
            return Keys.None;
        }

        void OnBossKilled(string bossName, TimeSpan killTime)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var boss = _bossStatsList.FirstOrDefault(b => b.Name == bossName);
                if (boss == null)
                {
                    boss = new BossStats { Name = bossName };
                    _bossStatsList.Add(boss);
                }
                boss.AddKill(killTime);
            });

            var bossConfigs = BossConfigManager.GetBossConfigs();
            if (bossConfigs.TryGetValue(bossName, out var config))
            {
                totalWood += config.Tree;
                var elapsed = (DateTime.Now - startTime).TotalHours;
                var woodPerHour = elapsed > 0 ? totalWood / elapsed : 0;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    updateWoodCallback?.Invoke(totalWood, woodPerHour);
                });
            }
        }
        private void StartElapsedTimer()
        {
            elapsedTimer = new System.Timers.Timer(1000); // 1초마다
            elapsedTimer.Elapsed += (s, e) =>
            {
                var elapsed = DateTime.Now - startTime;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = (MainWindow)System.Windows.Application.Current.MainWindow;
                    mainWindow.txtElapsedTime.Text = $"실행 시간: {elapsed:d\\:hh\\:mm}";
                });
            };
            elapsedTimer.Start();
        }

        public void RefreshOcrSettings()
        {
            ocrService?.RefreshFilterSettings();
        }
        private Rectangle ParseROI(string roiValue)
        {
            string[] parts = roiValue.Split(',');
            if (parts.Length == 4 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y) && int.TryParse(parts[2], out int width) && int.TryParse(parts[3], out int height))
            {
                return new Rectangle(x, y, width, height);
            }
            return new Rectangle(0, 0, 0, 0);
        }
    }
}