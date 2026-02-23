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
            txt_BotToken.Text = Properties.Settings.Default.TelegramBotToken;
            lbl_UserCount.Content = $"등록된 사용자: {_botService?.RegisteredCount ?? 0}명";
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.TelegramBotToken = txt_BotToken.Text.Trim();
            Properties.Settings.Default.Save();
            MessageBox.Show("저장되었습니다.\n토큰을 변경한 경우 앱을 재시작해야 적용됩니다.", "저장 완료");
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
