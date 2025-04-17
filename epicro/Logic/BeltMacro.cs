using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace epicro.Logic
{
    public class BeltMacro
    {
        private bool running = false;
        private bool stopRequested = false; // 🔹 중지 요청 플래그 추가
        private IntPtr targetWindow; // 🔹 메인폼에서 전달받은 타겟 윈도우
        private double beltSpeed; // 벨트 속도 (초)
        private int heroNumber; // 영웅 번호
        private int bagNumber; // 배낭 번호
        private int beltNumber; // 벨트 번호 (넘버패드)
        private bool isSaveEnabled;
        private bool isBumEnabled;
        private bool isHeroSelectEnabled;
        private DateTime lastHeroKeyPressTime;
        private Thread macroThread;
        private readonly Action<string> log;

        // Windows API - 키 입력 (SendMessage)
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        static extern void PostMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_CHAR = 0x0102;
        private const int VK_RETURN = 0x0D;

        // 숫자 키 매핑 (0~9 키보드 숫자)
        private static readonly Dictionary<int, int> NUMKEY_CODE = new Dictionary<int, int>
        {
            { 0, 0x30 }, { 1, 0x31 }, { 2, 0x32 }, { 3, 0x33 }, { 4, 0x34 },
            { 5, 0x35 }, { 6, 0x36 }, { 7, 0x37 }, { 8, 0x38 }, { 9, 0x39 }
        };

        // 넘버패드 키 매핑 (넘버패드 키)
        private static readonly Dictionary<string, int> NUMPAD_CODES = new Dictionary<string, int>()
        {
            { "넘버패드1", (int)Keys.NumPad1 },
            { "넘버패드2", (int)Keys.NumPad2 },
            { "넘버패드3", (int)Keys.NumPad3 },
            { "넘버패드4", (int)Keys.NumPad4 },
            { "넘버패드5", (int)Keys.NumPad5 },
            { "넘버패드6", (int)Keys.NumPad6 },
            { "넘버패드7", (int)Keys.NumPad7 },
            { "넘버패드8", (int)Keys.NumPad8 },
            { "넘버패드9", (int)Keys.NumPad9 },
            { "넘버패드0", (int)Keys.NumPad0 }
        };

        public BeltMacro(Action<string> log, IntPtr hwnd)
        {
            this.log = log;
            targetWindow = hwnd; // 🔹 mainform에서 설정한 targetWindow 사용
            LoadSettings();
        }

        private void LoadSettings()
        {
            Properties.Settings.Default.Reload();
            // 🔹 저장된 값 불러오기
            beltSpeed = Properties.Settings.Default.BeltSpeed;
            heroNumber = Properties.Settings.Default.HeroNum;
            bagNumber = Properties.Settings.Default.BagNum;
            // 🔹 벨트 번호 변환 (저장된 문자열을 넘버패드 키 값으로 변환)
            beltNumber = NUMPAD_CODES.TryGetValue(Properties.Settings.Default.BeltNum, out int beltKey)
                ? beltKey
                : (int)Keys.NumPad2; // 기본값: 넘버패드2
            // 🔹 체크박스 값 불러오기 (bool 변수로 저장)
            isSaveEnabled = Properties.Settings.Default.SaveEnabled;
            isBumEnabled = Properties.Settings.Default.PickupEnabled;
            isHeroSelectEnabled = Properties.Settings.Default.HeroSelectEnabled;
        }

        public void StartMacro()
        {
            if (running) return;
            if (targetWindow == IntPtr.Zero) return;

            running = true;
            stopRequested = false; // 🔹 중지 요청 초기화
            lastHeroKeyPressTime = DateTime.Now;

            macroThread = new Thread(RunMacro);
            macroThread.Start();
        }

        public void StopMacro()
        {
            stopRequested = true; // 🔹 중지 요청 플래그 활성화
            running = false;

            if (macroThread != null && macroThread.IsAlive)
            {
                macroThread.Join(); // 🔹 스레드가 안전하게 종료될 때까지 대기
                Console.WriteLine("매크로가 중지되었습니다.");
            }

        }

        private void RunMacro()
        {

            DateTime lastHourlyTaskTime = DateTime.Now; // 🔹 마지막으로 1시간마다 실행된 시간

            while (running && !stopRequested)
            {
                if (targetWindow == IntPtr.Zero)
                {
                    log("타겟 윈도우가 종료됨, 매크로 자동 중지");
                    StopMacro();
                    return;
                }

                DateTime currentTime = DateTime.Now;

                if ((currentTime - lastHourlyTaskTime).TotalSeconds >= 3600)
                {
                    PerformHourlyTasks();
                    lastHourlyTaskTime = DateTime.Now;
                }

                if (isHeroSelectEnabled && (currentTime - lastHeroKeyPressTime).TotalSeconds >= 10)
                {
                    if (heroNumber == 0)
                    {
                        SendKey(targetWindow, (int)Keys.F1); // F1 키 직접 전송
                        lastHeroKeyPressTime = DateTime.Now;
                    }
                    else if (NUMKEY_CODE.TryGetValue(heroNumber, out int heroKey))
                    {
                        SendKey(targetWindow, heroKey);
                        lastHeroKeyPressTime = DateTime.Now;
                    }
                    /*
                    if (NUMKEY_CODE.TryGetValue(heroNumber, out int heroKey))
                    {
                        SendKey(targetWindow, heroKey);
                        lastHeroKeyPressTime = DateTime.Now;
                    }
                    */
                }

                if (beltNumber != 0)
                {
                    SendKey(targetWindow, beltNumber);
                }

                Thread.Sleep((int)(beltSpeed * 1000));
            }
        }

        /// <summary>
        /// 🔹 1시간마다 수행되는 작업 (범줍, 저장 등)
        /// </summary>
        private void PerformHourlyTasks()
        {
            if (targetWindow == IntPtr.Zero) return;

            //running = false; // 🔹 매크로 실행 중지

            Console.WriteLine("1시간마다 수행되는 작업 시작!");

            // 🔹 범줍 기능 실행
            if (isBumEnabled)
            {
                Thread.Sleep(1000); // 1초 대기
                if (bagNumber != 0)
                {
                    if (NUMKEY_CODE.TryGetValue(bagNumber, out int bagKey))
                    {
                        SendKey(targetWindow, bagKey);
                    }
                }
                else if(bagNumber == 0)
                {
                    SendKey(targetWindow, (int)Keys.F8);
                }

                    Thread.Sleep(200); // 0.2초 대기
                SendChar(targetWindow, 'q'); // 'q' 입력
                Thread.Sleep(200);
                SendChar(targetWindow, 'g'); // 'g' 입력
                Thread.Sleep(200);

                if(heroNumber == 0)
                {
                    SendKey(targetWindow, (int)Keys.F1); // F1 키 직접 전송
                }
                else if (NUMKEY_CODE.TryGetValue(heroNumber, out int heroKey))
                {
                    SendKey(targetWindow, heroKey);
                }
                Console.WriteLine("🔹 창고 범줍 완료!");
            }

            Thread.Sleep(500); // 0.5초 대기

            // 🔹 저장 기능 실행
            if (isSaveEnabled)
            {
                SendKey(targetWindow, VK_RETURN); // 엔터 입력
                SendString(targetWindow, "-save"); // "-save" 입력
                SendKey(targetWindow, VK_RETURN); // 엔터 입력
                log("세이브 완료!");
                Thread.Sleep(500);
            }

            //running = true; // 🔹 매크로 실행 재개
        }

        private void PressEscAfterDelay()
        {
            Thread.Sleep(5000); // 5초 대기 후 ESC 입력
            SendKey(targetWindow, (int)Keys.Escape);
        }

        private void SendKey(IntPtr hwnd, int keyCode)
        {
            // 🔹 키 누르기
            SendMessage(hwnd, WM_KEYDOWN, keyCode, 0);
            Thread.Sleep(50);
            // 🔹 키 떼기
            SendMessage(hwnd, WM_KEYUP, keyCode, 0);
        }

        /// <summary>
        /// 🔹 단일 문자 입력 (한 글자씩)
        /// </summary>
        /// <param name="hwnd">입력할 대상 윈도우 핸들</param>
        /// <param name="ch">입력할 문자</param>
        private void SendChar(IntPtr hwnd, char ch)
        {
            PostMessage(hwnd, WM_CHAR, ch, 0);
            Thread.Sleep(50);
        }

        /// <summary>
        /// 🔹 문자열 입력 (한 글자씩 반복 입력)
        /// </summary>
        /// <param name="hwnd">입력할 대상 윈도우 핸들</param>
        /// <param name="text">입력할 문자열</param>
        private void SendString(IntPtr hwnd, string text)
        {
            foreach (char ch in text)
            {
                PostMessage(hwnd, WM_CHAR, ch, 0);
                Thread.Sleep(50);
            }
        }
    }
}
