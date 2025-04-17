using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
using epicro.Helpers;
using OpenCvSharp;
using Windows.UI.Xaml.Controls;

namespace epicro
{
    /// <summary>
    /// OcrSettingWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class OcrSettingWindow : System.Windows.Window
    {
        private OcrService ocrService;
        private System.Windows.Media.Color? textColor1 = null;
        private System.Windows.Media.Color? textColor2 = null;
        private System.Windows.Media.Color? textColor3 = null;
        private System.Windows.Media.Color? backgroundColor = null;

        private enum ColorTarget
        {
            None,
            Text1,
            Text2,
            Text3,
            Background
        }

        private ColorTarget currentTarget = ColorTarget.None;

        public OcrSettingWindow()
        {
            InitializeComponent();
            this.ocrService = new OcrService(() => MainWindow.backgroundCapture.GetSafeTextureCopy(), MainWindow.ocrEngine);
            Loaded += (sender, e) => LoadRoiImage();
        }

        private void LoadRoiImage()
        {
            var bmp = ocrService.GetProcessedRoiBitmap("roi_gold"); // 필요시 키 변경
            if (bmp == null) return;

            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Seek(0, SeekOrigin.Begin);

                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = ms;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();

                OriginalImage.Source = image;
            }

            bmp.Dispose();
        }
        private void OriginalImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var bitmap = OriginalImage.Source as BitmapSource;
            if (bitmap == null) return;

            var pos = e.GetPosition(OriginalImage);
            int x = (int)(pos.X * bitmap.PixelWidth / OriginalImage.ActualWidth);
            int y = (int)(pos.Y * bitmap.PixelHeight / OriginalImage.ActualHeight);

            byte[] pixels = new byte[4];
            bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixels, 4, 0);
            var selectedColor = System.Windows.Media.Color.FromRgb(pixels[2], pixels[1], pixels[0]);

            switch (currentTarget)
            {
                case ColorTarget.Text1:
                    textColor1 = selectedColor;
                    Rectangle1.Fill = new SolidColorBrush(selectedColor);
                    break;
                case ColorTarget.Text2:
                    textColor2 = selectedColor;
                    Rectangle2.Fill = new SolidColorBrush(selectedColor);
                    break;
                case ColorTarget.Text3:
                    textColor3 = selectedColor;
                    Rectangle3.Fill = new SolidColorBrush(selectedColor);
                    break;
                case ColorTarget.Background:
                    backgroundColor = selectedColor;
                    RectangleBG.Fill = new SolidColorBrush(selectedColor);
                    break;
            }

            ApplyFilterToImage();
        }
        private void ApplyFilterToImage()
        {
            var bmp = ocrService.GetProcessedRoiBitmap();
            if (bmp == null)
                return;

            var mat = OpenCvSharp.Extensions.BitmapConverter.ToMat(bmp); // Mat은 using 가능하지만 수동 dispose도 가능
            try
            {
                for (int y = 0; y < mat.Rows; y++)
                {
                    for (int x = 0; x < mat.Cols; x++)
                    {
                        var color = mat.At<Vec3b>(y, x);
                        var bgr = System.Windows.Media.Color.FromRgb(color.Item2, color.Item1, color.Item0);

                        bool isText =
                            (CheckBox1.IsChecked == true && textColor1 != null && IsWithinRange(bgr, textColor1.Value, (int)Slider1.Value)) ||
                            (CheckBox2.IsChecked == true && textColor2 != null && IsWithinRange(bgr, textColor2.Value, (int)Slider2.Value)) ||
                            (CheckBox3.IsChecked == true && textColor3 != null && IsWithinRange(bgr, textColor3.Value, (int)Slider3.Value));

                        bool isBackground =
                            (CheckBoxBG.IsChecked == true && backgroundColor != null && IsWithinRange(bgr, backgroundColor.Value, (int)SliderBG.Value));

                        if (isText)
                            mat.Set(y, x, new Vec3b(0, 0, 0));
                        else if (isBackground)
                            mat.Set(y, x, new Vec3b(255, 255, 255));
                        else
                            mat.Set(y, x, new Vec3b(127, 127, 127));
                    }
                }

                var filteredBitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(mat);
                using (var ms = new MemoryStream())
                {
                    filteredBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Seek(0, SeekOrigin.Begin);

                    var image = new BitmapImage();
                    image.BeginInit();
                    image.StreamSource = ms;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();

                    FilteredImage.Source = image;
                }
            }
            finally
            {
                mat.Dispose();
            }
        }
        private Bitmap BitmapSourceToBitmap(BitmapSource bitmapSource)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(outStream);
                var bmp = new Bitmap(outStream);
                return bmp;
            }
        }
        private void SaveColorSettings()
        {
            // 문자 색상 1
            if (textColor1 != null)
                Properties.Settings.Default.TextColor1 = textColor1.Value.ToString();
            Properties.Settings.Default.TextRange1 = (int)Slider1.Value;

            // 문자 색상 2
            if (textColor2 != null)
                Properties.Settings.Default.TextColor2 = textColor2.Value.ToString();
            Properties.Settings.Default.TextRange2 = (int)Slider2.Value;

            // 문자 색상 3
            if (textColor3 != null)
                Properties.Settings.Default.TextColor3 = textColor3.Value.ToString();
            Properties.Settings.Default.TextRange3 = (int)Slider3.Value;

            // 배경 색상
            if (backgroundColor != null)
                Properties.Settings.Default.BackgroundColor = backgroundColor.Value.ToString();
            Properties.Settings.Default.BackgroundRange = (int)SliderBG.Value;

            ocrService.RefreshFilterSettings();
            Properties.Settings.Default.Save(); // 저장!
        }
        private bool IsWithinRange(System.Windows.Media.Color target, System.Windows.Media.Color baseColor, int range)
        {
            return Math.Abs(target.R - baseColor.R) <= range &&
                   Math.Abs(target.G - baseColor.G) <= range &&
                   Math.Abs(target.B - baseColor.B) <= range;
        }
        private void ColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ApplyFilterToImage(); // 슬라이더 값 바뀔 때마다 필터 다시 적용
            SaveColorSettings();
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double zoom = e.NewValue;
            ZoomTransform.ScaleX = zoom;
            ZoomTransform.ScaleY = zoom;
        }

        private void FillteredZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double zoom = e.NewValue;
            FillterdZoomTransform.ScaleX = zoom;
            FillterdZoomTransform.ScaleY = zoom;
        }

        private void CheckBox1_Click(object sender, RoutedEventArgs e)
        {
            currentTarget = ColorTarget.Text1;
        }
        private void CheckBox2_Click(object sender, RoutedEventArgs e)
        {
            currentTarget = ColorTarget.Text2;
        }
        private void CheckBox3_Click(object sender, RoutedEventArgs e)
        {
            currentTarget = ColorTarget.Text3;
        }
        private void CheckBoxBG_Click(object sender, RoutedEventArgs e)
        {
            currentTarget = ColorTarget.Background;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SaveColorSettings();
            this.Close();
        }

        private void OcrTestButton_Click(object sender, RoutedEventArgs e)
        {
            // 선택된 ROI에 맞춰 OCR 테스트 수행
            string roiKey = "roi_gold"; // 필요에 따라 다른 ROI를 선택할 수 있음
            int ocrResult = ocrService.ReadCurrentValue(roiKey);

            if (ocrResult != -1)
            {
                // OCR이 성공적으로 결과를 가져왔을 경우, 결과를 텍스트박스에 출력
                OcrResultBox.Text = ocrResult.ToString();
            }
            else
            {
                // OCR 실패 시, 텍스트박스에 오류 메시지 출력
                OcrResultBox.Text = "OCR 실패. 결과를 가져올 수 없습니다.";
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

}