using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;
using System.IO;
using System.Drawing;
using Composition.WindowsRuntimeHelpers;
using SharpDX.Direct3D11;
using System.Diagnostics;
using System.Timers;
using System.Drawing.Imaging;
using epicro.Helpers;
using OpenCvSharp.Extensions;
using OpenCvSharp;

namespace epicro.Helpers
{
    public class OcrService
    {
        private readonly TesseractEngine ocrEngine;
        private readonly Func<Texture2D> getTextureFunc;

        public event Action<string> OnOcrResult;

        private List<Tuple<System.Drawing.Color, int>> textColors;
        private Tuple<System.Drawing.Color, int> backgroundColor;


        public OcrService(Func<Texture2D> textureProvider, TesseractEngine engine)
        {
            getTextureFunc = textureProvider;
            ocrEngine = engine;
            ocrEngine.SetVariable("classify_bln_numeric_mode", "1");
            ocrEngine.SetVariable("tessedit_char_whitelist", "0123456789");
            LoadFilterSettings();
        }
        public int ReadCurrentValue(string roiSettingKey = "roi_gold")
        {
            using (var texture = getTextureFunc())
            {
                if (texture == null)
                {
                    Debug.WriteLine("텍스처가 null입니다.");
                    return -1;
                }

                using (var rawBitmap = Direct3D11Helper.ExtractBitmapFromTexture(texture))
                {
                    var roiStr = Properties.Settings.Default[roiSettingKey] as string;
                    if (string.IsNullOrWhiteSpace(roiStr))
                    {
                        Debug.WriteLine($"ROI 설정이 존재하지 않음: {roiSettingKey}");
                        return -1;
                    }

                    var roiParse = ParseRoiHelper.ParseRectFromSettings(roiStr);
                    var roi = new Rectangle(roiParse.X, roiParse.Y, roiParse.Width, roiParse.Height);

                    using (var roiBitmap = rawBitmap.Clone(roi, PixelFormat.Format32bppArgb))
                    {
                        try
                        {
                            // ▶ 전처리 시작
                            using (var mat = BitmapConverter.ToMat(roiBitmap))
                            {
                                Cv2.CvtColor(mat, mat, ColorConversionCodes.BGRA2BGR); // BGRA → BGR 변환

                                for (int y = 0; y < mat.Rows; y++)
                                {
                                    for (int x = 0; x < mat.Cols; x++)
                                    {
                                        var color = mat.At<Vec3b>(y, x); // BGR
                                        var bgr = System.Drawing.Color.FromArgb(color.Item2, color.Item1, color.Item0); // R, G, B

                                        bool isText = false;
                                        foreach (var t in textColors)
                                        {
                                            if (IsWithinRange(bgr, t.Item1, t.Item2))
                                            {
                                                isText = true;
                                                break;
                                            }
                                        }

                                        bool isBackground = backgroundColor != null &&
                                                            IsWithinRange(bgr, backgroundColor.Item1, backgroundColor.Item2);

                                        if (isText)
                                            mat.Set(y, x, new Vec3b(0, 0, 0)); // 검정
                                        else if (isBackground)
                                            mat.Set(y, x, new Vec3b(255, 255, 255)); // 흰색
                                        else
                                            mat.Set(y, x, new Vec3b(127, 127, 127)); // 중간 회색
                                    }
                                }

                                using (var processedBitmap = BitmapConverter.ToBitmap(mat))
                                using (var pix = PixConverter.ToPix(processedBitmap))
                                using (var page = ocrEngine.Process(pix, PageSegMode.SingleLine))
                                {
                                    string result = page.GetText().Trim();
                                    string digits = new string(result.Where(char.IsDigit).ToArray());

                                    if (string.IsNullOrWhiteSpace(digits) && result.Contains("O"))
                                        digits = "0";

                                    /*
                                    if (string.IsNullOrWhiteSpace(digits))
                                    {
                                        //string failPath = $"ocr_fail_{DateTime.Now:yyyyMMdd_HHmmssfff}.png";
                                        ///processedBitmap.Save(failPath);
                                    }
                                    */

                                    return int.TryParse(digits, out int value) ? value : -1;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("OCR 처리 중 오류: " + ex.Message);
                            return -1;
                        }
                    }
                }
            }
        }

        public Bitmap GetProcessedRoiBitmap(string roiSettingKey = "roi_gold")
        {
            using (var texture = getTextureFunc())
            {
                if (texture == null)
                    return null;

                using (var rawBitmap = Direct3D11Helper.ExtractBitmapFromTexture(texture))
                {
                    var roiStr = Properties.Settings.Default[roiSettingKey] as string;
                    if (string.IsNullOrWhiteSpace(roiStr))
                        return null;

                    var roiParse = ParseRoiHelper.ParseRectFromSettings(roiStr);
                    var roi = new Rectangle(roiParse.X, roiParse.Y, roiParse.Width, roiParse.Height);

                    var roiBitmap = rawBitmap.Clone(roi, PixelFormat.Format32bppArgb);

                    return roiBitmap;
                }
            }
        }

        private bool IsWithinRange(System.Drawing.Color target, System.Drawing.Color baseColor, int range)
        {
            return Math.Abs(target.R - baseColor.R) <= range &&
                   Math.Abs(target.G - baseColor.G) <= range &&
                   Math.Abs(target.B - baseColor.B) <= range;
        }

        public void LoadFilterSettings()
        {
            textColors = new List<Tuple<System.Drawing.Color, int>>();

            AddTextColor(Properties.Settings.Default.TextColor1, Properties.Settings.Default.TextRange1);
            AddTextColor(Properties.Settings.Default.TextColor2, Properties.Settings.Default.TextRange2);
            AddTextColor(Properties.Settings.Default.TextColor3, Properties.Settings.Default.TextRange3);

            try
            {
                var bg = Properties.Settings.Default.BackgroundColor;
                if (!string.IsNullOrWhiteSpace(bg))
                {
                    var bgColor = ColorTranslator.FromHtml(bg);
                    backgroundColor = new Tuple<System.Drawing.Color, int>(bgColor, Properties.Settings.Default.BackgroundRange);
                }
            }
            catch
            {
                backgroundColor = null;
            }
        }

        public void RefreshFilterSettings()
        {
            // 색상 값 갱신
            textColors.Clear();

            // 다시 색상 값을 리스트로 추가
            AddTextColor(Properties.Settings.Default.TextColor1, Properties.Settings.Default.TextRange1);
            AddTextColor(Properties.Settings.Default.TextColor2, Properties.Settings.Default.TextRange2);
            AddTextColor(Properties.Settings.Default.TextColor3, Properties.Settings.Default.TextRange3);

            // 배경색
            try
            {
                var bgHex = Properties.Settings.Default.BackgroundColor;
                if (!string.IsNullOrWhiteSpace(bgHex))
                {
                    var bgColor = System.Drawing.ColorTranslator.FromHtml(bgHex);
                    backgroundColor = new Tuple<System.Drawing.Color, int>(bgColor, Properties.Settings.Default.BackgroundRange);
                }
            }
            catch
            {
                backgroundColor = null;
            }
        }

        private void AddTextColor(string hex, int range)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hex) && range > 0)
                {
                    var color = ColorTranslator.FromHtml(hex);
                    textColors.Add(new Tuple<System.Drawing.Color, int>(color, range));
                }
            }
            catch
            {
                // 무시
            }
        }
    }
}
