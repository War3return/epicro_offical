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
            txt_ChatId.Text = Properties.Settings.Default.TelegramChatIds;
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
            // 입력된 값으로 임시 업데이트 후 테스트 (저장 전이어도 전송 가능)
            _botService?.UpdateChatIds(input);
            await _botService?.BroadcastAsync("🔔 epicro 테스트 메시지입니다.");
            btnTest.IsEnabled = true;
            MessageBox.Show("테스트 메시지를 전송했습니다.", "완료");
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
