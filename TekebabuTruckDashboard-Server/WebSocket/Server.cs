using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class WebSocketServer
{
    private readonly HttpListener listener;
    private CancellationTokenSource cts = new CancellationTokenSource();
    private bool running = false;

    internal event Action<WebSocket>? ClientConnected;
    internal event Action<WebSocket, string>? MessageReceived;
    internal event Action<WebSocket>? ClientDisconnected;

    internal ConsoleFormat console = new ConsoleFormat();

    public WebSocketServer(string url)
    {
        listener = new HttpListener();
        listener.Prefixes.Add(url);

        console.Info($"WebSocket Server initialized at {url}", nameof(WebSocketServer));
    }

    public async Task StartAsync()
    {
        running = true;
        listener.Start();

        console.Info("WebSocket Server started.", nameof(WebSocketServer));

        try
        {
            while (running)
            {
                HttpListenerContext context;

                try
                {
                    // ★ キャンセル可能な待機に変更
                    context = await listener.GetContextAsync().WaitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // StopAsync() による正常終了
                    break;
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 995)
                {
                    // I/O中断 → listener.Stop() による終了
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // listener.Close() による終了
                    break;
                }

                if (context.Request.IsWebSocketRequest)
                {
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    var socket = wsContext.WebSocket;

                    ClientConnected?.Invoke(socket);

                    _ = Task.Run(() => HandleClientAsync(socket));
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }
        finally
        {
            listener.Stop();
            listener.Close(); // 完全クローズ
            console.Info("WebSocket Server stopped.", nameof(WebSocketServer));
        }
    }

    private async Task HandleClientAsync(WebSocket socket)
    {
        var buffer = new byte[16384];

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                MessageReceived?.Invoke(socket, msg);
            }
        }
        finally
        {
            ClientDisconnected?.Invoke(socket);

            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
            }
            catch { }
        }
    }

    public Task StopAsync()
    {
        running = false;
        cts.Cancel();   // ★ GetContextAsync をキャンセル
        return Task.CompletedTask;
    }
}
