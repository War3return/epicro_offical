using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using epicro.Utilites;
using epicro.Helpers;

namespace epicro
{
    /// <summary>
    /// BossSetting.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class BossSetting : Window
    {
        public BossSetting()
        {
            InitializeComponent();
        }

        private async void btn_BossROI_Click(object sender, RoutedEventArgs e)
        {
            var TargetWindow = MainWindow.TargetWindow;
            // MainWindow의 TargetWindow를 전달받는 구조가 필요합니다.
            if (Application.Current.MainWindow is MainWindow main && TargetWindow != null)
            {
                var bitmap = await SoftwareBitmapCopy.CaptureSingleFrameAsync(TargetWindow.Handle);

                if (bitmap != null)
                {
                    NativeMethods.GetClientRect(TargetWindow.Handle, out RECT rect);
                    var style = NativeMethods.GetWindowLong(TargetWindow.Handle, NativeMethods.GWL_STYLE);
                    NativeMethods.AdjustWindowRect(ref rect, style, false);

                    var clientWidth = rect.right - rect.left;
                    var clientHeight = rect.bottom - rect.top;

                    var roiWindow = new ROIWindow(bitmap, new[] { "Q", "W", "E", "R", "A" }, "Roi")
                    {
                        Width = clientWidth,
                        Height = clientHeight
                    };

                    roiWindow.ShowDialog();
                }
            }
            else
            {
                MessageBox.Show("먼저 인식 대상을 선택하세요.");
            }
        }

        private async void btn_GoldTree_Click(object sender, RoutedEventArgs e)
        {
            var TargetWindow = MainWindow.TargetWindow;
            if (TargetWindow == null)
            {
                MessageBox.Show("먼저 캡처할 창을 선택하세요.");
                return;
            }

            var bitmap = await SoftwareBitmapCopy.CaptureSingleFrameAsync(TargetWindow.Handle);

            if (bitmap != null)
            {
                NativeMethods.GetClientRect(TargetWindow.Handle, out RECT rect);
                var style = NativeMethods.GetWindowLong(TargetWindow.Handle, NativeMethods.GWL_STYLE);
                NativeMethods.AdjustWindowRect(ref rect, style, false);

                var clientWidth = rect.right - rect.left;
                var clientHeight = rect.bottom - rect.top;

                var roiWindow = new ROIWindow(bitmap, new[] { "Gold", "Tree" }, "Roi") // settings에 Roi_Gold, Roi_Tree 저장됨
                {
                    Width = clientWidth,
                    Height = clientHeight
                };

                roiWindow.ShowDialog();
            }
        }

        private async void btn_AutoCapture_Click(object sender, RoutedEventArgs e)
        {
            var TargetWindow = MainWindow.TargetWindow;
            if (cbb_BossZone.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedZone = selectedItem.Content.ToString();
                await BossImageHelper.CaptureAndSaveBossImagesAsync(selectedZone, TargetWindow.Handle);
            }
            else
            {
                MessageBox.Show("보스존을 선택해주세요.");
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var ocrsetttingwindow = new OcrSettingWindow();
            ocrsetttingwindow.ShowDialog();
        }
    }
}
