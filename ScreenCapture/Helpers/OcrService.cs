﻿using System;
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
            ocrEngine.SetVariable("classify_bln_numeric_mode", "1");
            ocrEngine.SetVariable("tessedit_char_whitelist", "0123456789");
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
                            using (var pix = PixConverter.ToPix(roiBitmap))
                            using (var page = ocrEngine.Process(pix, PageSegMode.SingleChar))
                            {
                                string result = page.GetText().Trim();
                                string digits = new string(result.Where(char.IsDigit).ToArray());

                                if (digits == "" && result.Contains("O"))
                                    digits = "0";
                                //Debug.WriteLine("최종 숫자 인식 결과: " + digits);
                                //Debug.WriteLine("OCR 결과: " + result);

                                if (string.IsNullOrWhiteSpace(digits))
                                {
                                    var debugPath = $"ocr_fail_{DateTime.Now:yyyyMMdd_HHmmssfff}.png";
                                    roiBitmap.Save(debugPath, System.Drawing.Imaging.ImageFormat.Png);
                                }

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
        }

        public void Stop()
        {
            ocrTimer?.Stop();
            ocrTimer?.Dispose();
        }
    }
}
