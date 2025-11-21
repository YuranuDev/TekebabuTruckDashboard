using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal class Software
{
    public SWebSocket WebSocket { get; set; } = new SWebSocket();
    public vJoySettings vJoy { get; set; } = new vJoySettings();
}

internal class SWebSocket
{
    public string Address { get; set; } = "localhost";
    public int Port { get; set; } = 22100;
}

internal class vJoySettings
{
    [Range(1, 16, ErrorMessage = "vJoy Device ID must be between 1 and 16.")]
    public int DeviceID { get; set; } = 1;
}
