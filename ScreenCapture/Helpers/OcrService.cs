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
using WPFCaptureSample.Helpers;

namespace WPFCaptureSample.Helpers
{
    public class OcrService
    {
        private System.Timers.Timer ocrTimer;
        private bool isOcrRunning = false;
        private readonly TesseractEngine ocrEngine;
        private readonly Func<Texture2D> getTextureFunc;

        public event Action<string> OnOcrResult;


        public OcrService(Func<Texture2D> textureProvider, TesseractEngine engine)
        {
            getTextureFunc = textureProvider;
            ocrEngine = engine;
        }

        public void Start()
        {
            ocrTimer = new System.Timers.Timer(500);
            ocrTimer.Elapsed += async (s, e) =>
            {
                if (isOcrRunning) return;
                isOcrRunning = true;

                await Task.Run(() =>
                {
                    var texture = getTextureFunc();
                    if (texture == null)
                    {
                        Debug.WriteLine("텍스처가 null입니다.");
                        isOcrRunning = false;
                        return;
                    }

                    using (var rawBitmap = Direct3D11Helper.ExtractBitmapFromTexture(texture))
                    {
                        var roiWpf = ParseRoiHelper.ParseRectFromSettings(Properties.Settings.Default.Roi_Gold);
                        var roi = new Rectangle(roiWpf.X, roiWpf.Y, roiWpf.Width, roiWpf.Height);

                        using (var roiBitmap = rawBitmap.Clone(roi, PixelFormat.Format32bppArgb))
                        {
                            try
                            {
                                ocrEngine.SetVariable("classify_bln_numeric_mode", "1");
                                ocrEngine.SetVariable("tessedit_char_whitelist", "0123456789");

                                using (var pix = PixConverter.ToPix(roiBitmap))
                                using (var page = ocrEngine.Process(pix, PageSegMode.SingleChar))
                                {
                                    string result = page.GetText().Trim();
                                    string digits = new string(result.Where(char.IsDigit).ToArray());

                                    if (digits == "" && result.Contains("O"))
                                        digits = "0";

                                    OnOcrResult?.Invoke(digits);
                                    Debug.WriteLine("최종 숫자 인식 결과: " + digits);
                                    Debug.WriteLine("OCR 결과: " + result);

                                    if (string.IsNullOrWhiteSpace(result))
                                        roiBitmap.Save($"ocr_fail_{DateTime.Now.Ticks}.png");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("OCR 처리 중 오류: " + ex.Message);
                            }
                            finally
                            {
                                isOcrRunning = false;
                            }
                        }
                    }
                });
            };

            ocrTimer.Start();
        }
        public int ReadCurrentValue(string roiSettingKey = "roi_gold")
        {
            var texture = getTextureFunc();
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
                        ocrEngine.SetVariable("classify_bln_numeric_mode", "1");
                        ocrEngine.SetVariable("tessedit_char_whitelist", "0123456789");

                        using (var pix = PixConverter.ToPix(roiBitmap))
                        using (var page = ocrEngine.Process(pix, PageSegMode.SingleChar))
                        {
                            string result = page.GetText().Trim();
                            string digits = new string(result.Where(char.IsDigit).ToArray());

                            if (digits == "" && result.Contains("O"))
                                digits = "0";
                            Debug.WriteLine("최종 숫자 인식 결과: " + digits);
                            Debug.WriteLine("OCR 결과: " + result);

                            if (string.IsNullOrWhiteSpace(digits))
                                roiBitmap.Save($"ocr_fail_{DateTime.Now.Ticks}.png");

                            // OCR 결과를 정수로 변환
                            return int.TryParse(digits, out int value) ? value : -1;
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
        public void Stop()
        {
            ocrTimer?.Stop();
            ocrTimer?.Dispose();
        }
    }
}
