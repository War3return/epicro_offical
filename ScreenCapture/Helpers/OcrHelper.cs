using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;
using System.IO;
using System.Drawing;

namespace WPFCaptureSample.Helpers
{
    public static class OcrHelper
    {
        public static string RecognizeDigits(Bitmap bitmap)
        {
            try
            {
                string tessPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tesseract", "tessdata");
                using (var engine = new TesseractEngine(Path.GetDirectoryName(tessPath), "eng", EngineMode.Default))
                {
                    engine.SetVariable("tessedit_char_whitelist", "0123456789");
                    using (var pix = PixConverter.ToPix(bitmap))
                    using (var page = engine.Process(pix))
                    {
                        return page.GetText().Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                return $"[OCR ERROR] {ex.Message}";
            }
        }
    }
}
