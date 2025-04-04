using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace WPFCaptureSample.Helpers
{
    public static class InputHelper
    {
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_CHAR = 0x0102;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        public static void SendKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!keyMap.TryGetValue(key.ToUpper(), out int keyCode)) return;

            var hwnd = MainWindow.TargetWindow.Handle;
            if (hwnd == IntPtr.Zero) return;

            SendMessage(hwnd, WM_KEYDOWN, keyCode, 0);
            Thread.Sleep(50);
            SendMessage(hwnd, WM_KEYUP, keyCode, 0);
        }

        public static void SendChar(char ch)
        {
            var hwnd = MainWindow.TargetWindow.Handle;
            if (hwnd == IntPtr.Zero) return;

            PostMessage(hwnd, WM_CHAR, ch, 0);
            Thread.Sleep(50);
        }

        private static readonly Dictionary<string, int> keyMap = new Dictionary<string, int>
        {
            { "Q", 0x51 },
            { "W", 0x57 },
            { "E", 0x45 },
            { "R", 0x52 },
            { "A", 0x41 },
            { "S", 0x53 },
            { "D", 0x44 },
            { "F", 0x46 }
            // 필요한 키코드 추가 가능
        };
    }
}
