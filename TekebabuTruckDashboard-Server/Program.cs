using WMPLib;
using System.Net.WebSockets;
using vJoyInterfaceWrap;
using Newtonsoft.Json;

namespace TekebabuTruckDashboard_Server
{
    internal class Program
    {
        public static vJoy vjoy = new vJoy();
        public static WebSocketServer server;
        public static ConsoleFormat console = new ConsoleFormat();
        public static Software softwareSettings = new Software();

        public static string FilePath_Settings = Path.Combine(AppContext.BaseDirectory, "Settings", "setttings.json");
        public static string touchPath = Path.Combine(AppContext.BaseDirectory, "Sounds", "touch.wav");

        static void Main(string[] args)
        {
            // Close Eventをキャッチ
            AppDomain.CurrentDomain.ProcessExit += (s, ex) =>
            {
                ReleasevJoyDevice();
                CloseWebSocketServer();
            };

            // Cool thing about console #1: ASCII Art
            Console.WriteLine(@"
             _____     _       _       _       _____             _   ____          _   _                 _ 
            |_   _|___| |_ ___| |_ ___| |_ _ _|_   _|___ _ _ ___| |_|    \ ___ ___| |_| |_ ___ ___ ___ _| |
              | | | -_| '_| -_| . | .'| . | | | | | |  _| | |  _| '_|  |  | .'|_ -|   | . | . | .'|  _| . |
              |_| |___|_,_|___|___|__,|___|___| |_| |_| |___|___|_,_|____/|__,|___|_|_|___|___|__,|_| |___|
             by yuranu                                                                               Server 

            ");

            console.SetLevel(ConsoleFormat.LogLevel.INFO);
            console.Debug("ConsoleFormat Test! :)", nameof(ConsoleFormat));

            // Init VJoy
            InitializeVJoy();

            // Load Settings
            LoadSettings();

            // Check vJoy Device
            CheckvJoyDevice();

            // WebSocket Server Start
            StartWebSocketServer();

            // Wait for Exit
            WaitforExit();
        }

        static void InitializeVJoy()
        {
            // vJoyドライバが有効か確認
            if (!vjoy.vJoyEnabled())
            {
                console.Error("vJoyドライバが使用できません。ドライバを確認してください。", nameof(Program));
                Environment.Exit(1);
            }
            console.Debug("vJoy Driver: Enabled", nameof(Program));

            // vJoyのバージョン情報を取得
            short vJoyVersion = vjoy.GetvJoyVersion();
            console.Info($"vJoyドライバ バージョン: {vJoyVersion}", nameof(Program));
        }

        static void LoadSettings()
        {
            // Load Software Settings
            try
            {
                JsonManager jsonManager = new JsonManager();
                jsonManager.JsonFilePath = FilePath_Settings;

                softwareSettings = jsonManager.LoadJson<Software>();
                console.Info("Softwareの読み込み完了", nameof(Program));
            }
            catch (Exception ex)
            {
                console.Error($"Error loading Software settings: {ex.Message}", nameof(Program));
            }
        }

        static void CheckvJoyDevice()
        {
            // vJoyの状態をチェック
            VjdStat status = vjoy.GetVJDStatus((uint)softwareSettings.vJoy.DeviceID);

            // 空いてない場合は終了
            if (status != VjdStat.VJD_STAT_FREE)
            {
                console.Error($"vJoyデバイス ID{softwareSettings.vJoy.DeviceID} は使用できません。Status: {status}", nameof(Program));
                Environment.Exit(1);
            }
            console.Info($"vJoyデバイス ID{softwareSettings.vJoy.DeviceID} 準備完了", nameof(Program));
            console.Debug($"vjoy Status: {status}", nameof(Program));

            // vJoyデバイスを取得
            if (!vjoy.AcquireVJD((uint)softwareSettings.vJoy.DeviceID))
            {
                console.Error($"vJoyデバイス ID{softwareSettings.vJoy.DeviceID} の取得に失敗しました。", nameof(Program));
                Environment.Exit(1);
            }
            console.Info($"vJoyデバイス ID{softwareSettings.vJoy.DeviceID} を取得しました。", nameof(Program));
        }

        public async static void StartWebSocketServer()
        {
            string url = $"http://{softwareSettings.WebSocket.Address}:{softwareSettings.WebSocket.Port}/";
            server = new WebSocketServer(url);

            server.ClientConnected += (socket) =>
            {
                console.Info("WebSocketクライアントが接続しました。", nameof(Program));
            };
            server.MessageReceived += (socket, message) =>
            {
                WebSocket_OnMessageReceived(socket, message);
            };
            server.ClientDisconnected += (socket) =>
            {
                console.Info("WebSocketクライアントが切断しました。", nameof(Program));
            };

            await server.StartAsync();
        }

        // ---------------------------------------------
        // WebSocket Controllers
        // ---------------------------------------------
        public static void WebSocket_OnMessageReceived(WebSocket socket, string message)
        {
            console.Debug($"WebSocket受信: {message}", nameof(Program));

            // メッセージの解析とvJoyへの反映
            CButton status = JsonConvert.DeserializeObject<CButton>(message);

            int buttonID = status.ButtonID;
            bool Pressed = status.Pressed;
            DateTime timestamp = status.TimeStamp;

            if (buttonID == 0) {
                console.Warning("ButtonIDが0です。無効なIDのため処理をスキップします。", nameof(Program));
                return;
            }
            // ボタンの状態をvJoyに反映
            vjoy.SetBtn(Pressed, (uint)softwareSettings.vJoy.DeviceID, (uint)buttonID);

            console.Debug($"Button ID{buttonID} ({Pressed}) at {timestamp}", nameof(Program));

            PlayTouchSound();
        }

        public static void PlayTouchSound()
        {
            try
            {
                // ファイル存在チェック
                if (string.IsNullOrEmpty(touchPath) || !File.Exists(touchPath))
                {
                    console.Warning($"タッチ音声ファイルが見つかりません: {touchPath}", nameof(Program));
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

                console.Debug($"Touch sound 再生開始: {touchPath}", nameof(Program));
            }
            catch (Exception ex)
            {
                console.Error($"PlayTouchSound エラー: {ex.Message}", nameof(Program));
            }
        }

        public static void WaitforExit()
        {
            console.Info("終了待機中... (Ctrl+Cで終了)", nameof(Program));
            // Conosole Close Eventをキャッチ
            Console.CancelKeyPress += (s, ex) =>
            {
                ReleasevJoyDevice();
                CloseWebSocketServer();
            };

            // 無限ループで待機
            while (true)
            {
                System.Threading.Thread.Sleep(1000);
            }
        }

        public static void ReleasevJoyDevice()
        {
            console.Info("vjoy解放中...", nameof(Program));

            // vJoyデバイスの解放
            if (softwareSettings.vJoy.DeviceID != null)
            {
                vjoy.RelinquishVJD((uint)softwareSettings.vJoy.DeviceID);
                console.Info("vJoyデバイスを解放しました。", nameof(Program));
            }
        }

        public async static void CloseWebSocketServer()
        {
            if (server == null) return;

            console.Info("WebSocketサーバーを停止中...", nameof(Program));
            await server.StopAsync();
            server = null;
            console.Info("WebSocketサーバーを停止しました。", nameof(Program));
        }
    }
}
