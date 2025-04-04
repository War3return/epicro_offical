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
using WPFCaptureSample.Utilites;
using System.Windows.Threading;
using WPFCaptureSample.Helpers;

namespace WPFCaptureSample
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

        private BasicCapture backgroundCapture;
        public static WindowInfo TargetWindow { get; private set; }
        private DispatcherTimer ocrTimer;

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

#if DEBUG
            // Force graphicscapture.dll to load.
            var picker = new GraphicsCapturePicker();
#endif
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string hero = Properties.Settings.Default.HeroNum;
            string bag = Properties.Settings.Default.BagNum;
            string beltNum = Properties.Settings.Default.BeltNum;
            string beltSpeed = Properties.Settings.Default.BeltSpeed;

            Debug.WriteLine($"불러온 설정값 - 영웅: {hero}, 창고: {bag}, 벨트번호: {beltNum}, 속도: {beltSpeed}");

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
                TargetWindow = process;
                //StopCapture();
                var hwnd = process.Handle;
                try
                {
                    // 백그라운드 캡처 시작
                    var d3dDevice = Direct3D11Helper.CreateDevice();
                    var item = CaptureHelper.CreateItemForWindow(TargetWindow.Handle);
                    backgroundCapture = new BasicCapture(d3dDevice, item);
                    backgroundCapture.StartCapture();

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
            var bossSetting = new BossSetting();
            bossSetting.ShowDialog();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            StartOcrTimer();
        }

        private void StartOcrTimer()
        {
            ocrTimer = new DispatcherTimer();
            ocrTimer.Interval = TimeSpan.FromMilliseconds(500); // 0.5초마다 실행
            ocrTimer.Tick += async (s, e) =>
            {
                var hwnd = TargetWindow.Handle; // 타겟 윈도우 핸들
                var roi = ParseRoiHelper.ParseRectFromSettings(Properties.Settings.Default.Roi_Gold); // 예시
                var text = await SoftwareBitmapCopy.CaptureAndRecognizeAsync(hwnd, roi);

                Debug.WriteLine($"[OCR] 인식된 텍스트: {text}");
                // 여기서 텍스트를 UI에 표시하거나 저장하면 됩니다.
            };
            ocrTimer.Start();
        }

        private void StopOcrTimer()
        {
            if (ocrTimer != null)
            {
                ocrTimer.Stop();
                ocrTimer = null;
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            StopOcrTimer();
        }
    }
}
