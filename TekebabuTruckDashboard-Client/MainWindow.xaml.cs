using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using WMPLib;
using System.Net;

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

        public static string FilePath_Settings = Path.Combine(AppContext.BaseDirectory, "Settings", "settings.json");

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
            try
            {
                await client.ConnectAsync();
            }
            catch (Exception ex)
            {
                console.Error($"WebSocket Client Connection Failed: {ex.Message}", nameof(MainWindow));
                return;
            }

            console.Info($"Connected", nameof(MainWindow));
        }

        private void LoadSettings()
        {
            softwareSettings = new Software();

            JsonManager jsonL = new JsonManager();
            jsonL.JsonFilePath = FilePath_Settings;

            // ファイルの存在を確認し、存在したらロード、存在しなければ初期設定を作成
            if (!File.Exists(FilePath_Settings))
            {
                jsonL.LoadJson<Software>(true); // ファイルがなければ作成

                // 設定をコンソールで聞き出す
                console.Info("初回起動のため、Software設定を作成します。", nameof(MainWindow));
                console.Info("WebSocketサーバーのアドレスを入力してください (例:localhost, 127.0.0.1)", nameof(MainWindow));

                string address = "";

                while (string.IsNullOrEmpty(address))
                {
                    console.Info("WebSocket Address: ", nameof(MainWindow));
                    address = Console.ReadLine();

                    if (address.ToLower() == "localhost" || address == "*" || IPAddress.TryParse(address, out IPAddress ip))
                    {
                        softwareSettings.WebSocket.Address = address;
                    }
                    else if (string.IsNullOrEmpty(address))
                    {
                        address = "";
                    }
                    else
                    {
                        console.Error("無効なアドレスです。再度入力してください。", nameof(MainWindow));
                        address = "";
                    }
                }

                console.Info(@"WebSocketのポート番号を入力してください。: ", nameof(MainWindow));
                console.Info("デフォルト: 22100 ", nameof(MainWindow));

                string sport = "";

                while (string.IsNullOrEmpty(sport))
                {
                    console.Info("WebSocket Port (空白でデフォルト値): ", nameof(MainWindow));
                    sport = Console.ReadLine();

                    if (string.IsNullOrEmpty(sport))
                    {
                        sport = "22100";
                    }

                    if (int.TryParse(sport, out int port))
                    {
                        if (port < 1 || port > 65535)
                        {
                            console.Warning("ポート番号は1から65535の間で指定してください。再度入力してください。", nameof(MainWindow));
                            sport = "";
                            continue;
                        }
                        softwareSettings.WebSocket.Port = port;
                    }
                    else if (!string.IsNullOrEmpty(sport))
                    {
                        console.Warning("無効なポート番号です。再度入力してください。", nameof(MainWindow));
                        sport = "";
                    }
                }

                jsonL.SaveJson<Software>(softwareSettings);
            }
            else
            {
                // 設定ファイルを読み込む
                try
                {
                    softwareSettings = jsonL.LoadJson<Software>(false);

                    if (softwareSettings.WebSocket.Port == -1 || softwareSettings.WebSocket.Port < 1 || softwareSettings.WebSocket.Port > 65535)
                    {
                        console.Error("Software設定のWebSocket Portが不正です。設定ファイルを削除しますか？", nameof(MainWindow));
                        console.Error("y/n: ", nameof(MainWindow));
                        string input2 = Console.ReadLine();
                        if (input2 != null)
                        {
                            if (input2.ToLower() == "y" || input2.ToLower() == "yes")
                            {
                                File.Delete(FilePath_Settings);
                                Application.Current.Shutdown();

                            }
                            else if (input2.ToLower() == "n" || input2.ToLower() == "no")
                            {
                                console.Error("プログラムを終了します。", nameof(MainWindow));
                                Application.Current.Shutdown();
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(softwareSettings.WebSocket.Address))
                    {
                        if (softwareSettings.WebSocket.Address.ToLower() != "localhost" && softwareSettings.WebSocket.Address.ToLower() != "*" && !IPAddress.TryParse(softwareSettings.WebSocket.Address, out IPAddress ip))
                        {
                            console.Error("Software設定のWebSocket Addressが不正です。設定ファイルを削除しますか？", nameof(MainWindow));
                            console.Error("y/n: ", nameof(MainWindow));
                            string input3 = Console.ReadLine();
                            if (input3 != null)
                            {
                                if (input3.ToLower() == "y" || input3.ToLower() == "yes")
                                {
                                    File.Delete(FilePath_Settings);
                                    Application.Current.Shutdown();
                                }
                                else if (input3.ToLower() == "n" || input3.ToLower() == "no")
                                {
                                    console.Error("プログラムを終了します。", nameof(MainWindow));
                                    Application.Current.Shutdown();
                                }
                            }
                        }
                    }

                    console.Info("Software設定を読み込みました。", nameof(MainWindow));
                }
                catch (Exception ex)
                {
                    console.Error($"Software設定の読み込みに失敗しました: {ex.Message}", nameof(MainWindow));
                }
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
