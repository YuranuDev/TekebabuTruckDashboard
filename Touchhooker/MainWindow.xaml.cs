using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace TouchHold
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);

        const uint LEFTDOWN = 0x0002;
        const uint LEFTUP = 0x0004;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_TouchDown(object sender, TouchEventArgs e)
        {
            var pos = Mouse.GetPosition(null);
            mouse_event(LEFTDOWN, (int)pos.X, (int)pos.Y, 0, UIntPtr.Zero);
        }

        private void Window_TouchUp(object sender, TouchEventArgs e)
        {
            var pos = Mouse.GetPosition(null);
            mouse_event(LEFTUP, (int)pos.X, (int)pos.Y, 0, UIntPtr.Zero);
        }
    }
}
