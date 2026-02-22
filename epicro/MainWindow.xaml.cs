//  ---------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// 
//  The MIT License (MIT)
// 
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
// 
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.
//  ---------------------------------------------------------------------------------

using CaptureSampleCore;
using Composition.WindowsRuntimeHelpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;
using Windows.UI.Composition;
using epicro.Utilites;
using System.Windows.Threading;
using epicro.Helpers;
using Tesseract;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using epicro.Logic;
using epicro.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Reflection;


namespace epicro
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IntPtr hwnd;
        private Compositor compositor;
        private CompositionTarget target;
        private ContainerVisual root;

        private BasicSampleApplication sample;
        private ObservableCollection<WindowInfo> processes;
        private BossSummonerWpf summoner;
        private ProcessMemoryWatcher processWatcher;

        public static BasicCapture backgroundCapture;
        private OcrService ocrService;
        private BeltMacro beltMacro;
        public static WindowInfo TargetWindow { get; private set; }
        private System.Timers.Timer ocrTimer;
        public static TesseractEngine ocrEngine;
        private bool isOcrRunning = false;

        public ObservableCollection<BossStats> AllBossStats { get; set; } = new ObservableCollection<BossStats>();
        public ObservableCollection<BossStats> FilteredBossStatsList { get; set; } = new ObservableCollection<BossStats>();

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool AdjustWindowRect(ref RECT lpRect, uint dwStyle, bool bMenu);

        [DllImport("user32.dll")]
        static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        const int GWL_STYLE = -16;

        public MainWindow()
        {
            InitializeComponent();
            var picker = new GraphicsCapturePicker();
            string tessPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tesseract", "tessdata");
            ocrEngine = new TesseractEngine(tessPath, "eng", EngineMode.Default);
            InitBossSummoner();
            InitBossStats();
            this.DataContext = this;
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var shortVer = $"{version.Major}.{version.Minor}";
            this.Title = $"epicro v{shortVer}";
#if DEBUG

#endif
        }
        private void InitBossSummoner()
        {
            summoner = new BossSummonerWpf(AppendLog, FilteredBossStatsList, UpdateWoodStatus);
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            int hero = Properties.Settings.Default.HeroNum;
            int bag = Properties.Settings.Default.BagNum;
            string beltNum = Properties.Settings.Default.BeltNum;
            double beltSpeed = Properties.Settings.Default.BeltSpeed;

            // 🔹 보스존 복원
            foreach (ComboBoxItem item in cbb_BossZone.Items)
            {
                if (item.Content.ToString() == Properties.Settings.Default.BossZone)
                {
                    cbb_BossZone.SelectedItem = item;
                    break;
                }
            }

            // 🔹 인식방법 복원
            string roi = Properties.Settings.Default.SelectedROI;
            if (roi == "gold") rb_Gold.IsChecked = true;
            else if (roi == "tree") rb_Tree.IsChecked = true;

            // 🔹 소환순서 복원
            txt_BossOrder.Text = Properties.Settings.Default.BossOrder;

            // 🔹 체크박스 복원
            cb_save.IsChecked = Properties.Settings.Default.SaveEnabled;
            cb_pickup.IsChecked = Properties.Settings.Default.PickupEnabled;
            cb_heroselect.IsChecked = Properties.Settings.Default.HeroSelectEnabled;

            // 필터 설정을 Properties.Settings.Default에서 가져옴
            var textColors = new List<Tuple<System.Drawing.Color, int>>();

            // 각 설정 값이 비어있지 않으면 리스트에 추가
            if (!string.IsNullOrEmpty(Properties.Settings.Default.TextColor1))
                textColors.Add(new Tuple<System.Drawing.Color, int>(ColorTranslator.FromHtml(Properties.Settings.Default.TextColor1), Properties.Settings.Default.TextRange1));
            if (!string.IsNullOrEmpty(Properties.Settings.Default.TextColor2))
                textColors.Add(new Tuple<System.Drawing.Color, int>(ColorTranslator.FromHtml(Properties.Settings.Default.TextColor2), Properties.Settings.Default.TextRange2));
            if (!string.IsNullOrEmpty(Properties.Settings.Default.TextColor3))
                textColors.Add(new Tuple<System.Drawing.Color, int>(ColorTranslator.FromHtml(Properties.Settings.Default.TextColor3), Properties.Settings.Default.TextRange3));

            // 배경 색상도 설정
            var backgroundColor = string.IsNullOrEmpty(Properties.Settings.Default.BackgroundColor)
                ? null
                : new Tuple<System.Drawing.Color, int>(ColorTranslator.FromHtml(Properties.Settings.Default.BackgroundColor), Properties.Settings.Default.BackgroundRange);

            AppendLog("에피크로 로딩 완료");
            //Debug.WriteLine($"불러온 설정값 - 영웅: {hero}, 창고: {bag}, 벨트번호: {beltNum}, 속도: {beltSpeed}");

            LoadRoiAreas();
            /*
            var interopWindow = new WindowInteropHelper(this);
            hwnd = interopWindow.Handle;

            var presentationSource = PresentationSource.FromVisual(this);
            double dpiX = 1.0;
            double dpiY = 1.0;
            if (presentationSource != null)
            {
                dpiX = presentationSource.CompositionTarget.TransformToDevice.M11;
                dpiY = presentationSource.CompositionTarget.TransformToDevice.M22;
            }
            var controlsWidth = (float)(ControlsGrid.ActualWidth * dpiX);

            InitComposition(controlsWidth);
            InitWindowList();
            */
            // 기존 실시간 송출 제거 → 대신 BackgroundCapture만 시작
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 🔸 보스존 저장
            if (cbb_BossZone.SelectedItem is ComboBoxItem selectedZone)
            {
                Properties.Settings.Default.BossZone = selectedZone.Content.ToString();
            }

            // 🔸 인식방법 저장 (라디오버튼)
            if (rb_Gold.IsChecked == true)
                Properties.Settings.Default.SelectedROI = "gold";
            else if (rb_Tree.IsChecked == true)
                Properties.Settings.Default.SelectedROI = "tree";

            // 🔸 소환순서 저장
            Properties.Settings.Default.BossOrder = txt_BossOrder.Text;

            // 🔹 체크박스 상태 저장
            Properties.Settings.Default.SaveEnabled = cb_save.IsChecked == true;
            Properties.Settings.Default.PickupEnabled = cb_pickup.IsChecked == true;
            Properties.Settings.Default.HeroSelectEnabled = cb_heroselect.IsChecked == true;

            Properties.Settings.Default.Save(); // 저장!

            // 보스소환기가 실행 중이면 중지
            if (summoner != null)
            {
                summoner.Stop();  // 내부적으로 isRunning = false, CancellationToken.Cancel()
            }
            if (beltMacro != null)
            {
                beltMacro.StopMacro(); // 매크로 중지
                beltMacro = null; // BeltMacro 객체 해제
            }

            if (backgroundCapture != null)
            {
                backgroundCapture.StopCapture();
                backgroundCapture.Dispose(); // 기존 캡처 정리
                backgroundCapture = null;
                Debug.WriteLine("이전 백그라운드 캡처 해제 완료");
            }

            if (TargetWindow != null)
            {
                TargetWindow.ProcessExited -= OnTargetWindowExited;
                TargetWindow.StopMonitoring();
            }

            processWatcher?.Stop();
        }
        private void UpdateWoodStatus(int totalWood, double woodPerHour)
        {
            txtTotalWood.Text = $"총 목재: {totalWood:N0}";
            txtWoodPerHour.Text = $"시간당 목재: {woodPerHour:N0}";
        }

        private void InitBossStats()
        {
            var bossConfigs = BossConfigManager.GetBossConfigs();

            foreach (var kvp in bossConfigs)
            {
                string bossName = kvp.Key;

                // 중복 방지
                if (!AllBossStats.Any(x => x.Name == bossName))
                {
                    AllBossStats.Add(new BossStats { Name = bossName });
                }
            }
        }
        public void UpdateFilteredStats(string selectedZone)
        {
            var bossConfigs = BossConfigManager.GetBossConfigs();

            FilteredBossStatsList.Clear();

            foreach (var stat in AllBossStats)
            {
                if (bossConfigs.TryGetValue(stat.Name, out var config) && config.Zone == selectedZone)
                {
                    FilteredBossStatsList.Add(stat);
                }
            }
        }

        public void AppendLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txt_log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                txt_log.ScrollToEnd(); // 항상 최신 로그 보기
            });
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            //StopCapture();
            WindowComboBox.SelectedIndex = -1;
        }

        private void WindowComboBox_DropDownOpened(object sender, EventArgs e)
        {
            InitWindowList();
        }

        private void WindowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = (ComboBox)sender;
            var process = (WindowInfo)comboBox.SelectedItem;

            if (process != null)
            {
                // 이전 TargetWindow 이벤트 구독 해제
                if (TargetWindow != null)
                {
                    TargetWindow.ProcessExited -= OnTargetWindowExited;
                    TargetWindow.StopMonitoring();
                }

                TargetWindow = process;

                if (backgroundCapture != null)
                {
                    backgroundCapture.StopCapture();
                    backgroundCapture.Dispose(); // 기존 캡처 정리
                    backgroundCapture = null;
                    Debug.WriteLine("이전 백그라운드 캡처 해제 완료");
                }
                //StopCapture();
                var hwnd = process.Handle;
                try
                {
                    // 백그라운드 캡처 시작
                    var d3dDevice = Direct3D11Helper.CreateDevice();
                    var item = CaptureHelper.CreateItemForWindow(TargetWindow.Handle);
                    backgroundCapture = new BasicCapture(d3dDevice, item);
                    backgroundCapture.StartCapture();
                    AppendLog($"창 선택됨: {process.ToString()}");

                    if (processWatcher != null)
                    {
                        processWatcher.Stop(); // 이전 감시 종료
                        processWatcher = null;
                    }

                    processWatcher = new ProcessMemoryWatcher(Process.GetProcessById(process.ProcessId), UpdateMemoryLabel);
                    processWatcher.Start();

                    // 프로세스 종료 감지 시작
                    TargetWindow.ProcessExited += OnTargetWindowExited;
                    TargetWindow.StartExitAndRestartMonitoring();

                    Debug.WriteLine("백그라운드 캡처 시작됨");
                    //StartHwndCapture(hwnd);
                }
                catch (Exception)
                {
                    Debug.WriteLine($"Hwnd 0x{hwnd.ToInt32():X8} is not valid for capture!");
                    processes.Remove(process);
                    comboBox.SelectedIndex = -1;
                }
            }
        }

        private void OnTargetWindowExited(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                AppendLog("워크래프트 창이 종료되었습니다. 실행 중인 매크로를 중지합니다.");

                if (beltMacro != null)
                {
                    beltMacro.StopMacro();
                    beltMacro = null;
                    AppendLog("벨트 매크로 중지됨");
                }

                summoner?.Stop();

                if (backgroundCapture != null)
                {
                    backgroundCapture.StopCapture();
                    backgroundCapture.Dispose();
                    backgroundCapture = null;
                }

                processWatcher?.Stop();
                processWatcher = null;

                TargetWindow = null;
                WindowComboBox.SelectedIndex = -1;
                memoryLabel.Content = "";
            });
        }

        private void InitComposition(float controlsWidth)
        {
            // Create the compositor.
            compositor = new Compositor();

            // Create a target for the window.
            target = compositor.CreateDesktopWindowTarget(hwnd, true);

            // Attach the root visual.
            root = compositor.CreateContainerVisual();
            root.RelativeSizeAdjustment = Vector2.One;
            root.Size = new Vector2(-controlsWidth, 0);
            root.Offset = new Vector3(controlsWidth, 0, 0);
            target.Root = root;

            // Setup the rest of the sample application.
            sample = new BasicSampleApplication(compositor);
            root.Children.InsertAtTop(sample.Visual);
        }

        private List<Int32Rect> LoadRoiAreas()
        {
            var list = new List<Int32Rect>();

            string[] keys = { "Roi_Q", "Roi_W", "Roi_E", "Roi_R", "Roi_A" };
            foreach (var key in keys)
            {
                string value = Properties.Settings.Default[key] as string;
                if (!string.IsNullOrEmpty(value))
                {
                    var parts = value.Split(',');
                    var rect = new Int32Rect(
                        int.Parse(parts[0]),
                        int.Parse(parts[1]),
                        int.Parse(parts[2]),
                        int.Parse(parts[3]));
                    list.Add(rect);
                }
            }
            return list;
        }
        private string GetRoiImagePath(int index)
        {
            string[] roiNames = { "Q", "W", "E", "R", "A" };
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"ROI_{roiNames[index]}.png");
        }


        private void InitWindowList()
        {
            if (ApiInformation.IsApiContractPresent(typeof(Windows.Foundation.UniversalApiContract).FullName, 8))
            {
                var processesWithWindows = from p in Process.GetProcesses()
                                           where !string.IsNullOrWhiteSpace(p.MainWindowTitle)
                                           && p.MainWindowTitle.ToLower().Contains("warcraft")
                                           && WindowEnumerationHelper.IsWindowValidForCapture(p.MainWindowHandle)
                                           select new WindowInfo
                                           {
                                               Handle = p.MainWindowHandle,
                                               ProcessId = p.Id,
                                               Title = p.MainWindowTitle
                                           };
                processes = new ObservableCollection<WindowInfo>(processesWithWindows);
                WindowComboBox.ItemsSource = processes;
            }
            else
            {
                WindowComboBox.IsEnabled = false;
            }
        }

        private void StartHwndCapture(IntPtr hwnd)
        {
            GraphicsCaptureItem item = CaptureHelper.CreateItemForWindow(hwnd);
            if (item != null)
            {
                //sample.StartCaptureFromItem(item);
            }
        }

        private void StopCapture()
        {
            //sample.StopCapture();
        }

        private async void RoiButton_Click(object sender, RoutedEventArgs e)
        {
            if (TargetWindow == null)
            {
                MessageBox.Show("먼저 캡처할 창을 선택하세요.");
                return;
            }

            var bitmap = await SoftwareBitmapCopy.CaptureSingleFrameAsync(TargetWindow.Handle);

            if (bitmap != null)
            {
                GetClientRect(TargetWindow.Handle, out RECT rect);
                var style = GetWindowLong(TargetWindow.Handle, GWL_STYLE);
                AdjustWindowRect(ref rect, style, false);

                var clientWidth = rect.right - rect.left;
                var clientHeight = rect.bottom - rect.top;

                var roiWindow = new ROIWindow(bitmap, new[] { "Q", "W", "E", "R", "A" }, "Roi")
                {
                    Width = clientWidth,
                    Height = clientHeight
                };

                if (roiWindow.ShowDialog() == true)
                {

                    // 이후 ROI 정보를 저장하거나 비교용으로 사용하면 됩니다.
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btn_BossSetting_Click(object sender, RoutedEventArgs e)
        {
            if (TargetWindow == null)
            {
                MessageBox.Show("먼저 캡처할 창을 선택하세요.");
                return;
            }

            var bossSetting = new BossSetting();
            bossSetting.ShowDialog();
        }

        private void StopOcrTimer()
        {
            //ocrService?.Stop();
        }

        private void btn_BossStart_Click(object sender, RoutedEventArgs e)
        {
            LoadRoiAreas();
            Properties.Settings.Default.Reload();
            if (TargetWindow == null)
            {
                MessageBox.Show("먼저 캡처할 창을 선택하세요.");
                return;
            }

            //StartOcrTimer();
            if (cbb_BossZone.SelectedItem is ComboBoxItem selectedItem)
            {
                summoner.BossZone = selectedItem.Content.ToString();
            }

            summoner.RefreshOcrSettings();

            summoner.BossOrder = txt_BossOrder.Text;

            if (rb_Gold.IsChecked == true)
                Properties.Settings.Default.SelectedROI = "gold";
            else if (rb_Tree.IsChecked == true)
                Properties.Settings.Default.SelectedROI = "tree";

            summoner.Start();
        }

        private void btn_BossStop_Click(object sender, RoutedEventArgs e)
        {
            if (TargetWindow == null)
            {
                MessageBox.Show("먼저 캡처할 창을 선택하세요.");
                return;
            }

            summoner?.Stop();
        }

        private void cbb_BossZone_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbb_BossZone.SelectedItem is ComboBoxItem item)
            {
                string selectedZone = item.Content.ToString();
                UpdateFilteredStats(selectedZone);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void btnBeltSet_Click(object sender, RoutedEventArgs e)
        {
            if (TargetWindow == null)
            {
                MessageBox.Show("먼저 캡처할 창을 선택하세요.");
                return;
            }

            var beltSetting = new BeltSetting();
            beltSetting.ShowDialog();
        }

        private void btnBeltStart_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Reload(); // 디스크에서 최신 설정 불러오기
            
            if (beltMacro != null)
            {
                AppendLog("벨트 매크로가 이미 실행 중입니다.");
                return;
            }

            beltMacro = new BeltMacro(AppendLog, TargetWindow.Handle);
            Properties.Settings.Default.Reload();
            if (TargetWindow == null)
            {
                MessageBox.Show("먼저 캡처할 창을 선택하세요.");
                return;
            }
            beltMacro.StartMacro();
            AppendLog("벨트 매크로 시작");
        }

        private void btnBeltStop_Click(object sender, RoutedEventArgs e)
        {
            if (beltMacro != null)
            {
                beltMacro.StopMacro();
                beltMacro = null;
                AppendLog("벨트 매크로 중지됨");
            }
            else
            {
                AppendLog("벨트 매크로가 실행 중이 아닙니다.");
            }
        }

        private void CheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (sender == cb_save)
            {
                Properties.Settings.Default.SaveEnabled = cb_save.IsChecked == true;
            }
            else if (sender == cb_pickup)
            {
                Properties.Settings.Default.PickupEnabled = cb_pickup.IsChecked == true;
            }
            else if (sender == cb_heroselect)
            {
                Properties.Settings.Default.HeroSelectEnabled = cb_heroselect.IsChecked == true;
            }

            Properties.Settings.Default.Save(); // 반드시 저장 호출
        }

        private void btnItemMix_Click(object sender, RoutedEventArgs e)
        {
            var mixWindow = new ItemMixWindow();
            mixWindow.Owner = this; // 부모 창 지정 (선택 사항)
            mixWindow.Show();       // 또는 ShowDialog(); 로 모달창으로 열기 가능
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            if (TargetWindow == null)
            {
                MessageBox.Show("먼저 캡처할 창을 선택하세요.");
                return;
            }

            string targetTitle = TargetWindow.Title;

            InitWindowList();

            var matchingWindow = processes?.FirstOrDefault(w => w.Title == targetTitle);
            if (matchingWindow != null)
            {
                WindowComboBox.SelectedItem = matchingWindow;
            }
            else
            {
                AppendLog($"같은 이름의 창을 찾을 수 없습니다: {targetTitle}");
                MessageBox.Show($"'{targetTitle}' 창을 찾을 수 없습니다.");
            }
        }

        public void UpdateMemoryLabel(string text)
        {
            Dispatcher.Invoke(() =>
            {
                memoryLabel.Content = text;
            });
        }
    }
}
