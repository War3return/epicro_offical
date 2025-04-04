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
using WPFCaptureSample.Helpers;
using WPFCaptureSample.Models;
using System.Windows.Forms;

namespace WPFCaptureSample.Logic
{
    public class BossSummonerWpf
    {
        private CancellationTokenSource cts;
        private readonly Func<Bitmap> captureFunc;
        private readonly Action<string> log;
        private readonly Dictionary<string, BossConfig> bossConfigs;
        private int previousGold;
        private OcrService ocrService;
        private bool isRunning;

        public string BossOrder { get; set; }
        public string BossZone { get; set; }

        public BossSummonerWpf(Func<Bitmap> captureFunc, Action<string> log)
        {
            isRunning = false;
            this.captureFunc = captureFunc;
            this.log = log;
            this.bossConfigs = BossConfigManager.GetBossConfigs();

            ocrService = new OcrService(() => MainWindow.backgroundCapture.GetSafeTextureCopy(), MainWindow.ocrEngine);

        }

        public void Start()
        {
            if (cts != null)
                return;

            cts = new CancellationTokenSource();
            Task.Run(() => Run(cts.Token));
            log("[보스소환] 자동 소환 시작됨");
        }

        public void Stop()
        {
            cts?.Cancel();
            cts = null;
            log("[보스소환] 자동 소환 중지됨");
        }

        private void Run(CancellationToken token)
        {
            isRunning = true;
            var zonesToBosses = BossConfigManager.GetZonesToBosses();

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
                foreach (var bossName in orderedBosses)
                {
                    if (!isRunning) return;

                    if (!bossesInZone.Contains(bossName)) continue;

                    if (bossConfigs.ContainsKey(bossName)) // 이미지 비교함수 추가.
                    {
                        SummonBoss(bossName);
                        int startValue = previousValue;
                        DateTime startTime = DateTime.Now;
                        bool valueChanged = false;

                        while ((DateTime.Now - startTime).TotalSeconds < 300)
                        {
                            if(!isRunning) return;
                            int currentValue = ocrService.ReadCurrentValue(checkRoi);
                            if (currentValue == -1)
                            {
                                continue;
                            }

                            if (currentValue != startValue)
                            {
                                previousValue = currentValue;
                                valueChanged = true;
                                break;
                            }
                            Thread.Sleep(500);
                        }
                        if (!valueChanged)
                        {
                            if (!isRunning) return;
                            // 로그 [WARNING] 5분 동안 골드 변동이 없어 다음 보스를 찾습니다. 남기기
                        }
                        break;
                    }
                }
                Thread.Sleep(500);
            }
            bool goldChanged = false;

            DateTime start = DateTime.Now;

            while ((DateTime.Now - start).TotalDays < 300)
            {
                int valueAfter = ocrService.ReadCurrentValue(checkRoi);
                Thread.Sleep(1000);
            }
        }
        private void SummonBoss(string bossKey)
        {
            Keys key = GetBossKey(bossKey);
            Console.WriteLine($"[INFO] {bossKey} 소환 - {key}");

            InputHelper.SendKey(key.ToString());
        }
        // 보스 키 매핑
        private Keys GetBossKey(string bossName)
        {
            if (bossConfigs.ContainsKey(bossName))
            {
                // 보스의 'Key' 값(예: Q, W, E 등)을 반환
                return (Keys)Enum.Parse(typeof(Keys), bossConfigs[bossName].Key);
            }
            return Keys.None;
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