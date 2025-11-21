using System;

public class CButton
{
    public int ButtonID { get; set; } = -1;
    public bool Pressed { get; set; } = false;
    public DateTime TimeStamp { get; set; } = DateTime.Now;
}