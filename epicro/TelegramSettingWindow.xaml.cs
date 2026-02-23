using epicro.Helpers;
using System.Windows;

namespace epicro
{
    public partial class TelegramSettingWindow : Window
    {
        private readonly TelegramBotService _botService;

        public TelegramSettingWindow(TelegramBotService botService)
        {
            InitializeComponent();
            _botService = botService;
            lbl_UserCount.Content = $"등록된 사용자: {_botService?.RegisteredCount ?? 0}명";
        }

        private async void btnTest_Click(object sender, RoutedEventArgs e)
        {
            if (_botService == null || _botService.RegisteredCount == 0)
            {
                MessageBox.Show("등록된 사용자가 없습니다.\n봇에게 /start 명령을 먼저 보내세요.", "알림");
                return;
            }

            btnTest.IsEnabled = false;
            await _botService.BroadcastAsync("🔔 epicro 테스트 메시지입니다.");
            btnTest.IsEnabled = true;
            MessageBox.Show("테스트 메시지를 전송했습니다.", "완료");
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
