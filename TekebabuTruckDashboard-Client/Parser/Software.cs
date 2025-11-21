internal class Software
{
    public SWebSocket WebSocket { get; set; } = new SWebSocket();
    public vJoySettings vJoy { get; set; } = new vJoySettings();
}

internal class SWebSocket
{
    public string Address { get; set; } = "";
    public int Port { get; set; } = -1;
}

internal class vJoySettings
{
    public int DeviceID { get; set; } = 1;
}
