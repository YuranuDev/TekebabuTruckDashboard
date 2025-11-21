using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using WMPLib;

namespace TekebabuTruckDashboard_Client
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        internal static ConsoleFormat console = new ConsoleFormat();
        internal static WebSocketClientWrapper client;
        internal static Software softwareSettings = new Software();
        internal static string touchPath = Path.Combine(AppContext.BaseDirectory, "Sounds", "touch.wav");

        public static string FilePath_Settings = Path.Combine(AppContext.BaseDirectory, "Settings", "setttings.json");

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            console.Init();
            console.SetLevel(ConsoleFormat.LogLevel.INFO);

            // Cool thing about console #1: ASCII Art
            Console.WriteLine(@"
             _____     _       _       _       _____             _   ____          _   _                 _ 
            |_   _|___| |_ ___| |_ ___| |_ _ _|_   _|___ _ _ ___| |_|    \ ___ ___| |_| |_ ___ ___ ___ _| |
              | | | -_| '_| -_| . | .'| . | | | | | |  _| | |  _| '_|  |  | .'|_ -|   | . | . | .'|  _| . |
              |_| |___|_,_|___|___|__,|___|___| |_| |_| |___|___|_,_|____/|__,|___|_|_|___|___|__,|_| |___|
             by yuranu                                                                               Client 

            ");

            console.Debug("ConsoleFormat Test! :)", nameof(MainWindow));

            LoadSettings();

            await InitWebSocketClient();
        }

        private async Task InitWebSocketClient()
        {
            // WebSocket Client Init
            string serverUrl = $"http://{softwareSettings.WebSocket.Address}:{softwareSettings.WebSocket.Port}/";

            client = new WebSocketClientWrapper(serverUrl);

            client.Connected += () =>
            {
                console.Info("WebSocket Client Connected!", nameof(MainWindow));
            };

            client.Disconnected += () =>
            {
                console.Info("WebSocket Client Disconnected!", nameof(MainWindow));
            };

            console.Info($"Connecting to WebSocket Server at {serverUrl}", nameof(MainWindow));
            await client.ConnectAsync();
            console.Info($"Connected", nameof(MainWindow));
        }

        private void LoadSettings()
        {
            // Load Software Settings
            try
            {
                JsonManager jsonManager = new JsonManager();
                jsonManager.JsonFilePath = FilePath_Settings;

                softwareSettings = jsonManager.LoadJson<Software>();
                console.Info("Softwareの読み込み完了", nameof(MainWindow));
            }
            catch (Exception ex)
            {
                console.Error($"Error loading Software settings: {ex.Message}", nameof(MainWindow));
            }
        }

        private async void Button_Pressed(object sender, TouchEventArgs e)
        {
            int id = -1;

            if (sender is Button btn)
                id = int.Parse((string)btn.Tag);

            console.Debug($"ButtonPressed, ID:{id}", nameof(MainWindow));

            // Send WebSocket Message
            var buttonStatus = new CButton
            {
                ButtonID = id,
                Pressed = true,
                TimeStamp = DateTime.Now
            };

            string message = Newtonsoft.Json.JsonConvert.SerializeObject(buttonStatus);
            await client.SendAsync(message);

            PlayTouchSound();
        }

        private async void Button_Released(object sender, TouchEventArgs e)
        {
            int id = -1;

            if (sender is Button btn)
                id = int.Parse((string)btn.Tag);

            console.Debug($"ButtonReleased, ID:{id}", nameof(MainWindow));

            // Send WebSocket Message
            var buttonStatus = new CButton
            {
                ButtonID = id,
                Pressed = false,
                TimeStamp = DateTime.Now
            };

            string message = Newtonsoft.Json.JsonConvert.SerializeObject(buttonStatus);
            await client.SendAsync(message);

            PlayTouchSound();
        }

        public static void PlayTouchSound()
        {
            try
            {
                // ファイル存在チェック
                if (string.IsNullOrEmpty(touchPath) || !File.Exists(touchPath))
                {
                    console.Warning($"タッチ音声ファイルが見つかりません: {touchPath}", nameof(MainWindow));
                    return;
                }

                // Windows Media Player インスタンスを作成して再生
                var player = new WindowsMediaPlayer();

                // 必要に応じて音量を調整（0-100）
                try { player.settings.volume = 100; } catch { /* 設定不可でも継続 */ }

                player.URL = touchPath;
                player.controls.play();

                // 再生終了時にリソースを解放するハンドラ
                player.PlayStateChange += (int newState) =>
                {
                    // wmppsMediaEnded や wmppsStopped を検知して後片付け
                    if (newState == (int)WMPPlayState.wmppsMediaEnded || newState == (int)WMPPlayState.wmppsStopped)
                    {
                        try
                        {
                            player.controls.stop();
                        }
                        catch { }
                        try
                        {
                            player.close();
                        }
                        catch { }
                    }
                };

                console.Debug($"Touch sound 再生開始: {touchPath}", nameof(MainWindow));
            }
            catch (Exception ex)
            {
                console.Error($"PlayTouchSound エラー: {ex.Message}", nameof(MainWindow));
            }
        }
    }
}
