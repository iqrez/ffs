using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RawMouseToXbox360
{
    public class MainForm : Form
    {
        private ViGEmClient client;
        private IXbox360Controller controller;
        private float sensitivityX = 0.06f;
        private float sensitivityY = 0.06f;
        private short stickX = 0;
        private short stickY = 0;
        private DateTime lastMove = DateTime.MinValue;
        private StreamWriter diag;
        private Timer updateTimer;

        // Raw input constants and structs
        private const int WM_INPUT = 0x00FF;
        private const int RID_INPUT = 0x10000003;
        private const int RIM_TYPEMOUSE = 0;

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWMOUSE
        {
            public ushort usFlags;
            public uint ulButtons;
            public ushort usButtonFlags;
            public ushort usButtonData;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct RAWINPUT
        {
            [FieldOffset(0)]
            public RAWINPUTHEADER header;
            [FieldOffset(16)]
            public RAWMOUSE mouse;
        }

        [DllImport("User32.dll", SetLastError = true)]
        static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("User32.dll")]
        static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        public MainForm()
        {
            this.Text = "RawMouseToXbox360";
            this.Width = 350;
            this.Height = 100;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            diag = new StreamWriter("diagnostic.txt", false) { AutoFlush = true };
            diag.WriteLine($"--- RawMouseToXbox360 Started {DateTime.Now} ---");

            client = new ViGEmClient();
            controller = client.CreateXbox360Controller();
            controller.Connect();
            diag.WriteLine("ViGEm controller created and connected.");

            // REGISTER RAW INPUT!
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01; // Generic desktop controls
            rid[0].usUsage = 0x02;     // Mouse
            rid[0].dwFlags = 0;
            rid[0].hwndTarget = this.Handle;
            RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
            diag.WriteLine("Registered for raw mouse input.");

            // Timer updates controller state every 20ms
            updateTimer = new Timer();
            updateTimer.Interval = 20;
            updateTimer.Tick += (s, e) => UpdateStick();
            updateTimer.Start();
        }

        private void ProcessRawInput(IntPtr lParam)
        {
            uint dwSize = 0;
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
            IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
            try
            {
                GetRawInputData(lParam, RID_INPUT, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
                RAWINPUT raw = (RAWINPUT)Marshal.PtrToStructure(buffer, typeof(RAWINPUT));
                if (raw.header.dwType == RIM_TYPEMOUSE)
                {
                    int dx = raw.mouse.lLastX;
                    int dy = raw.mouse.lLastY;

                    if (dx != 0 || dy != 0)
                    {
                        stickX = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, dx * sensitivityX * short.MaxValue));
                        stickY = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, dy * sensitivityY * short.MaxValue));
                        lastMove = DateTime.Now;
                        diag.WriteLine($"MouseDelta: dx={dx}, dy={dy} -> stickX={stickX}, stickY={stickY}");
                    }

                    // Mouse buttons
                    ushort flags = raw.mouse.usButtonFlags;
                    if ((flags & 0x0001) != 0) controller.SetButtonState(Xbox360Button.A, true);   // Left Down
                    if ((flags & 0x0002) != 0) controller.SetButtonState(Xbox360Button.A, false);  // Left Up
                    if ((flags & 0x0004) != 0) controller.SetButtonState(Xbox360Button.B, true);   // Right Down
                    if ((flags & 0x0008) != 0) controller.SetButtonState(Xbox360Button.B, false);  // Right Up
                    if ((flags & 0x0010) != 0) controller.SetButtonState(Xbox360Button.X, true);   // Middle Down
                    if ((flags & 0x0020) != 0) controller.SetButtonState(Xbox360Button.X, false);  // Middle Up
                    if ((flags & 0x0040) != 0) controller.SetButtonState(Xbox360Button.Y, true);   // XButton1 Down
                    if ((flags & 0x0080) != 0) controller.SetButtonState(Xbox360Button.Y, false);  // XButton1 Up
                    if ((flags & 0x0100) != 0) controller.SetButtonState(Xbox360Button.LeftShoulder, true); // XButton2 Down
                    if ((flags & 0x0200) != 0) controller.SetButtonState(Xbox360Button.LeftShoulder, false);// XButton2 Up

                    controller.SubmitReport();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private void UpdateStick()
        {
            TimeSpan sinceMove = DateTime.Now - lastMove;
            // Hold stick value for 100ms after last movement, then center
            short sendX = sinceMove.TotalMilliseconds < 100 ? stickX : (short)0;
            short sendY = sinceMove.TotalMilliseconds < 100 ? stickY : (short)0;
            controller.SetAxisValue(Xbox360Axis.RightThumbX, sendX);
            controller.SetAxisValue(Xbox360Axis.RightThumbY, (short)-sendY); // Invert Y
            controller.SubmitReport();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_INPUT)
                ProcessRawInput(m.LParam);
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            updateTimer?.Stop();
            diag?.Close();
            controller?.Disconnect();
            client?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
