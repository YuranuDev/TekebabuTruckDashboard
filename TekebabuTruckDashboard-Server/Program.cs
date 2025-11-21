using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Data;
using System.Net;
using System.Net.WebSockets;
using System.Xml.Linq;
using vJoyInterfaceWrap;
using WMPLib;

namespace TekebabuTruckDashboard_Server
{
    internal class Program
    {
        public static vJoy vjoy = new vJoy();
        public static WebSocketServer server;
        public static ConsoleFormat console = new ConsoleFormat();
        public static Software softwareSettings = new Software();

        public static string FilePath_Settings = Path.Combine(AppContext.BaseDirectory, "Settings", "settings.json");
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
            softwareSettings = new Software();

            JsonManager jsonL = new JsonManager();
            jsonL.JsonFilePath = FilePath_Settings;

            // ファイルの存在を確認し、存在したらロード、存在しなければ初期設定を作成
            if (!File.Exists(FilePath_Settings))
            {
                jsonL.LoadJson<Software>(true); // ファイルがなければ作成

                // 設定をコンソールで聞き出す
                console.Info("初回起動のため、Software設定を作成します。", nameof(Program));
                console.Info("WebSocketサーバーのアドレスを入力してください (例:localhost, 127.0.0.1)", nameof(Program));
                console.Info("(「*」を使用するとLAN内の他クライアントPCがこのPCに接続できるようになります)", nameof(Program));
                console.Info("デフォルト: * ", nameof(Program));

                string address = "";

                while (string.IsNullOrEmpty(address))
                {
                    console.Info("WebSocket Address (空白でデフォルト値): ", nameof(Program));
                    address = Console.ReadLine();

                    if (address.ToLower() == "localhost" || address == "*" || IPAddress.TryParse(address, out IPAddress? ip))
                    {
                        softwareSettings.WebSocket.Address = address;
                    }
                    else if (string.IsNullOrEmpty(address))
                    {
                        address = "*";
                        softwareSettings.WebSocket.Address = address;
                    }
                    else
                    {
                        console.Error("無効なアドレスです。再度入力してください。", nameof(Program));
                        address = "";
                    }
                }

                console.Info(@"WebSocketのポート番号を入力してください。: ", nameof(Program));
                console.Info("デフォルト: 22100 ", nameof(Program));

                string sport = "";

                while (string.IsNullOrEmpty(sport))
                {
                    console.Info("WebSocket Port (空白でデフォルト値): ", nameof(Program));
                    sport = Console.ReadLine();

                    if (string.IsNullOrEmpty(sport))
                    {
                        sport = "22100";
                    }

                    if (int.TryParse(sport, out int port))
                    {
                        if (port < 1 || port > 65535)
                        {
                            console.Warning("ポート番号は1から65535の間で指定してください。再度入力してください。", nameof(Program));
                            sport = "";
                            continue;
                        }
                        softwareSettings.WebSocket.Port = port;
                    }
                    else if (!string.IsNullOrEmpty(sport))
                    {
                        console.Warning("無効なポート番号です。再度入力してください。", nameof(Program));
                        sport = "";
                    }
                }

                jsonL.SaveJson<Software>(softwareSettings);

                if (address == "*")
                {
                    console.Warning("外部PCがアクセスする場合、ファイアーウォール、URLACLの許可が必要になる可能性があります。", nameof(Program));
                    console.Warning("ファイアーウォール,、URLACLの設定を変更しますか？", nameof(Program));
                    console.Warning("(許可しない場合、LAN内の他PCから接続できない可能性があります。)", nameof(Program));

                    console.Warning("y/n: ", nameof(Program));
                    string fwinput = Console.ReadLine();

                    if (!string.IsNullOrEmpty(fwinput))
                    {
                        if (fwinput.ToLower() == "y" || fwinput.ToLower() == "yes")
                        {
                            try
                            {
                                string ruleName = "TekebabuTruckDashboard-Server WebSocket";
                                string portStr = softwareSettings.WebSocket.Port.ToString();

                                // もとからルールがあるか確認
                                System.Diagnostics.ProcessStartInfo checkPsi = new System.Diagnostics.ProcessStartInfo("netsh", $"advfirewall firewall show rule name=\"{ruleName}\"")
                                {
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                };

                                System.Diagnostics.Process checkProc = System.Diagnostics.Process.Start(checkPsi);
                                checkProc.WaitForExit();

                                string output = "";

                                try
                                {
                                    output = checkProc.StandardOutput.ReadToEnd();
                                    console.Debug($"ファイアーウォールルール確認出力: {output}", nameof(Program));
                                }
                                catch (Exception ex)
                                {
                                    console.Error($"ファイアーウォールルール確認出力の取得に失敗しました: {ex.Message}", nameof(Program));
                                }

                                if (output.Contains("No rules match the specified criteria"))
                                {
                                    console.Info("既存のファイアーウォールルールは見つかりませんでした。新規作成を続行します。", nameof(Program));

                                    // ファイアーウォールのルールを追加
                                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("netsh", $"advfirewall firewall add rule name = \"{ruleName}\" dir=in action=allow protocol=TCP localport=22100")
                                    {
                                        Verb = "runas",
                                        CreateNoWindow = true,
                                        UseShellExecute = true
                                    };
                                    System.Diagnostics.Process proc = System.Diagnostics.Process.Start(psi);
                                    proc.WaitForExit();

                                    try
                                    {
                                        string output2 = proc.StandardOutput.ReadToEnd();
                                        console.Debug($"ファイアーウォールルール追加出力: {output2}", nameof(Program));
                                    }
                                    catch (Exception ex)
                                    {
                                        console.Error($"ファイアーウォールルール追加出力の取得に失敗しました: {ex.Message}", nameof(Program));
                                    }

                                    console.Info("ファイアーウォールの設定を追加しました。", nameof(Program));
                                }
                                else
                                {
                                    console.Info("既存のファイアーウォールルールが見つかりました。削除、新規作成します。", nameof(Program));
                                    // 既存のルールを削除
                                    System.Diagnostics.ProcessStartInfo delPsi = new System.Diagnostics.ProcessStartInfo("netsh", $"advfirewall firewall delete rule name=\"{ruleName}\"")
                                    {
                                        Verb = "runas",
                                        CreateNoWindow = true,
                                        UseShellExecute = true
                                    };
                                    System.Diagnostics.Process delProc = System.Diagnostics.Process.Start(delPsi);
                                    delProc.WaitForExit();

                                    try
                                    {
                                        string delOutput = delProc.StandardOutput.ReadToEnd();
                                        console.Debug($"ファイアーウォールルール削除出力: {delOutput}", nameof(Program));
                                    }
                                    catch (Exception ex)
                                    {
                                        console.Error($"ファイアーウォールルール削除出力取得に失敗しました: {ex.Message}", nameof(Program));
                                    }

                                    console.Info("既存のファイアーウォールルールを削除しました。", nameof(Program));

                                    // 新規作成
                                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("netsh", $"advfirewall firewall add rule name = \"{ruleName}\" dir=in action=allow protocol=TCP localport={portStr}")
                                    {
                                        Verb = "runas",
                                        CreateNoWindow = true,
                                        UseShellExecute = true
                                    };
                                    System.Diagnostics.Process proc = System.Diagnostics.Process.Start(psi);
                                    proc.WaitForExit();

                                    try 
                                    {
                                        string output2 = proc.StandardOutput.ReadToEnd();
                                        console.Debug($"ファイアーウォールルール追加出力: {output2}", nameof(Program));
                                    }
                                    catch (Exception ex)
                                    {
                                        console.Error($"ファイアーウォールルール追加出力の取得に失敗しました: {ex.Message}", nameof(Program));
                                    }

                                    console.Info("ファイアーウォールの設定を追加しました。", nameof(Program));
                                }
                            }
                            catch (Exception ex)
                            {
                                console.Error($"ファイアーウォールの設定に失敗しました: {ex.Message}", nameof(Program));
                            }

                            // URLACLの設定を追加
                            try
                            {
                                string url = $"http://*:{softwareSettings.WebSocket.Port}/";
                                System.Diagnostics.ProcessStartInfo psi2 = new System.Diagnostics.ProcessStartInfo("netsh", $"http add urlacl url={url} user=Everyone")
                                {
                                    Verb = "runas",
                                    CreateNoWindow = true,
                                    UseShellExecute = true
                                };

                                System.Diagnostics.Process proc2 = System.Diagnostics.Process.Start(psi2);
                                proc2.WaitForExit();

                                try 
                                { 
                                    string output3 = proc2.StandardOutput.ReadToEnd();
                                    console.Debug($"URLACL追加出力: {output3}", nameof(Program));
                                }
                                catch (Exception ex)
                                {
                                    console.Error($"URLACL追加出力の取得に失敗しました: {ex.Message}", nameof(Program));
                                }

                                console.Info("URLACLの設定を追加しました。", nameof(Program));
                            }
                            catch (Exception ex)
                            {
                                console.Error($"URLACLの設定に失敗しました: {ex.Message}", nameof(Program));
                            }
                        }
                        else if (fwinput.ToLower() == "n" || fwinput.ToLower() == "no")
                        {
                            console.Info("ファイアーウォール、URLACLの設定をスキップしました。", nameof(Program));
                        }
                    }

                    console.Info("Software設定の作成が完了しました。", nameof(Program));
                }
            }
            else
            {
                // 設定ファイルを読み込む
                try
                {
                    softwareSettings = jsonL.LoadJson<Software>(false);

                    // 値が適切でない場合、ファイルを削除して再起動(初期設定)
                    if (softwareSettings.vJoy.DeviceID == -1 || softwareSettings.vJoy.DeviceID < 1 || softwareSettings.vJoy.DeviceID > 16)
                    {
                        console.Error("Software設定のvJoy DeviceIDが不正です。設定ファイルを削除しますか？", nameof(Program));
                        console.Error("y/n: ", nameof(Program));
                        string input = Console.ReadLine();

                        if (input != null)
                        {
                            if (input.ToLower() == "y" || input.ToLower() == "yes")
                            {
                                File.Delete(FilePath_Settings);
                                Environment.Exit(1);
                            }
                            else if (input.ToLower() == "n" || input.ToLower() == "no")
                            {
                                console.Error("プログラムを終了します。", nameof(Program));
                                Environment.Exit(1);
                            }

                        }
                    }

                    if (softwareSettings.WebSocket.Port == -1 || softwareSettings.WebSocket.Port < 1 || softwareSettings.WebSocket.Port > 65535)
                    {
                        console.Error("Software設定のWebSocket Portが不正です。設定ファイルを削除しますか？", nameof(Program));
                        console.Error("y/n: ", nameof(Program));
                        string input2 = Console.ReadLine();
                        if (input2 != null)
                        {
                            if (input2.ToLower() == "y" || input2.ToLower() == "yes")
                            {
                                File.Delete(FilePath_Settings);
                                Environment.Exit(1);

                            }
                            else if (input2.ToLower() == "n" || input2.ToLower() == "no")
                            {
                                console.Error("プログラムを終了します。", nameof(Program));
                                Environment.Exit(1);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(softwareSettings.WebSocket.Address))
                    {
                        if (softwareSettings.WebSocket.Address.ToLower() != "localhost" && softwareSettings.WebSocket.Address.ToLower() != "*" && !IPAddress.TryParse(softwareSettings.WebSocket.Address, out IPAddress? ip))
                        {
                            console.Error("Software設定のWebSocket Addressが不正です。設定ファイルを削除しますか？", nameof(Program));
                            console.Error("y/n: ", nameof(Program));
                            string input3 = Console.ReadLine();
                            if (input3 != null)
                            {
                                if (input3.ToLower() == "y" || input3.ToLower() == "yes")
                                {
                                    File.Delete(FilePath_Settings);
                                    Environment.Exit(1);
                                }
                                else if (input3.ToLower() == "n" || input3.ToLower() == "no")
                                {
                                    console.Error("プログラムを終了します。", nameof(Program));
                                    Environment.Exit(1);
                                }
                            }
                        }
                    }

                    console.Info("Software設定を読み込みました。", nameof(Program));
                }
                catch (Exception ex)
                {
                    console.Error($"Software設定の読み込みに失敗しました: {ex.Message}", nameof(Program));
                }
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
