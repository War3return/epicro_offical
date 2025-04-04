using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace WPFCaptureSample.Helpers
{
    public static class ParseRoiHelper
    {
        public static Int32Rect ParseRectFromSettings(string roiSetting)
        {
            var parts = roiSetting.Split(',');
            return new Int32Rect(
                int.Parse(parts[0]),
                int.Parse(parts[1]),
                int.Parse(parts[2]),
                int.Parse(parts[3])
            );
        }
    }
}
