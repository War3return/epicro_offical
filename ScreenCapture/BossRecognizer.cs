using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Bitmap;
using System.Collections.Generic;
using System;
using System.Drawing;
using System.IO;
using WPFCaptureSample.Helpers;
using WPFCaptureSample.Models;

public static class BossRecognizer
{
    public static bool IsBossActive(string bossName, Bitmap screenCapture, Dictionary<string, BossConfig> bossConfigs)
    {
        if (!bossConfigs.ContainsKey(bossName))
        {
            //LogHelper.Append($"[ERROR] {bossName} 정보가 BossConfig에 없습니다.");
            return false;
        }

        var config = bossConfigs[bossName];
        var roi = new Rectangle(config.Roi.X, config.Roi.Y, config.Roi.Width, config.Roi.Height);

        using (Bitmap roiImage = screenCapture.Clone(roi, screenCapture.PixelFormat))
        {
            string imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "boss", config.ImagePath);

            if (!File.Exists(imagePath))
            {
                //LogHelper.Append($"[ERROR] {imagePath} 이미지 파일이 존재하지 않습니다.");
                return false;
            }

            using (var mainImage = new Image<Bgr, byte>(roiImage))
            using (var template = new Image<Bgr, byte>(imagePath))
            using (var result = mainImage.MatchTemplate(template, TemplateMatchingType.CcoeffNormed))
            {
                double minVal = 0, maxVal = 0;
                Point minLoc = new Point(), maxLoc = new Point();
                CvInvoke.MinMaxLoc(result, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

                Console.WriteLine($"[INFO] {bossName} 매칭 점수: {maxVal}");
                return maxVal >= 0.99;
            }
        }
    }
    public static Image<Bgr, byte> BitmapToImage(Bitmap bmp)
    {
        using (var ms = new MemoryStream())
        {
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            ms.Position = 0;

            // Bitmap 대신 Emgu.CV Image로 직접 변환
            var byteArray = ms.ToArray();
            return new Image<Bgr, byte>(bmp.Width, bmp.Height, bmp.Width * 3, byteArray);
        }
    }
}
