using epicro.Helpers;
using System.Windows;
using System.Windows.Media;

namespace epicro
{
    public partial class TelegramSettingWindow : Window
    {
        private readonly TelegramBotService _botService;

        public TelegramSettingWindow(TelegramBotService botService)
        {
            InitializeComponent();
            _botService = botService;
            txt_ChatId.Text = Properties.Settings.Default.TelegramChatIds;
            RefreshToggleButton();
        }

        private void RefreshToggleButton()
        {
            bool enabled = _botService?.IsEnabled ?? true;
            if (enabled)
            {
                btnToggle.Content = "🔔 알림 켜짐  (클릭하면 끄기)";
                btnToggle.Background = new SolidColorBrush(Color.FromRgb(198, 239, 206));
            }
            else
            {
                btnToggle.Content = "🔕 알림 꺼짐  (클릭하면 켜기)";
                btnToggle.Background = new SolidColorBrush(Color.FromRgb(255, 199, 206));
            }
        }

        private void btnToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_botService == null) return;
            _botService.IsEnabled = !_botService.IsEnabled;
            Properties.Settings.Default.TelegramEnabled = _botService.IsEnabled;
            Properties.Settings.Default.Save();
            RefreshToggleButton();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            var input = txt_ChatId.Text.Trim();
            Properties.Settings.Default.TelegramChatIds = input;
            Properties.Settings.Default.Save();
            _botService?.UpdateChatIds(input);
            MessageBox.Show("저장되었습니다.", "완료");
        }

        private async void btnTest_Click(object sender, RoutedEventArgs e)
        {
            var input = txt_ChatId.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                MessageBox.Show("Chat ID를 먼저 입력하고 저장하세요.", "알림");
                return;
            }

            btnTest.IsEnabled = false;
            _botService?.UpdateChatIds(input);
            await _botService?.BroadcastAsync("🔔 epicro 테스트 메시지입니다.");
            btnTest.IsEnabled = true;
            MessageBox.Show("테스트 메시지를 전송했습니다.", "완료");
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
