using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class WebSocketClientWrapper
{
    private ClientWebSocket _ws;
    private Uri _serverUri;
    private CancellationTokenSource _cts = new CancellationTokenSource();

    public event Action<string> MessageReceived;
    public event Action Connected;
    public event Action Disconnected;

    public WebSocketClientWrapper(string serverUrl)
    {
        // サーバー URL を ws:// 形式に変換
        _serverUri = new Uri(serverUrl.Replace("http://", "ws://").Replace("https://", "wss://"));
        _ws = new ClientWebSocket();
    }

    // サーバーに接続
    public async Task ConnectAsync()
    {
        try
        {
            await _ws.ConnectAsync(_serverUri, _cts.Token);
            Connected?.Invoke();

            // 受信ループ開始
            _ = Task.Run(ReceiveLoop);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex}");
            throw;
        }
    }

    // メッセージ送信
    public async Task SendAsync(string message)
    {
        if (_ws.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket is not connected.");

        byte[] buffer = Encoding.UTF8.GetBytes(message);
        await _ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cts.Token);
    }

    // 接続を閉じる
    public async Task CloseAsync()
    {
        _cts.Cancel();

        if (_ws.State == WebSocketState.Open)
        {
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
        }

        Disconnected?.Invoke();
    }

    // 受信ループ
    private async Task ReceiveLoop()
    {
        var buffer = new byte[4096];

        try
        {
            while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", CancellationToken.None);
                    Disconnected?.Invoke();
                    break;
                }

                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                MessageReceived?.Invoke(message);
            }
        }
        catch (OperationCanceledException)
        {
            // キャンセルされた場合は無視
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Receive error: {ex}");
            Disconnected?.Invoke();
        }
    }
}
