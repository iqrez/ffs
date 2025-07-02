using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RawMouseToXbox360
{
    public static class RawInputHandler
    {
        public const int WM_INPUT = 0x00FF;

        public delegate void MouseDeltaEventHandler(int dx, int dy);
        public static event MouseDeltaEventHandler OnMouseDelta;

        public delegate void MouseButtonEventHandler(MouseButtons button, bool pressed);
        public static event MouseButtonEventHandler OnMouseButton;

        public delegate void MouseWheelEventHandler(int delta);
        public static event MouseWheelEventHandler OnMouseWheel;

        private const uint RID_INPUT = 0x10000003;
        private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        private const ushort HID_USAGE_GENERIC_MOUSE = 0x02;
        private const uint RIDEV_INPUTSINK = 0x00000100;

        [DllImport("User32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("User32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWMOUSE
        {
            public ushort usFlags;
            public uint ulButtons;
            public ushort usButtonFlags;
            public ushort usButtonData;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUT
        {
            public RAWINPUTHEADER header;
            public RAWMOUSE mouse;
        }

        public static void RegisterMouse(IntPtr hwnd)
        {
            var rid = new RAWINPUTDEVICE[1];
            rid[0].usUsagePage = HID_USAGE_PAGE_GENERIC;
            rid[0].usUsage = HID_USAGE_GENERIC_MOUSE;
            rid[0].dwFlags = RIDEV_INPUTSINK;
            rid[0].hwndTarget = hwnd;

            if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
            {
                int error = Marshal.GetLastWin32Error();
                throw new ApplicationException("Failed to register raw input devices. Error code: " + error);
            }
        }

        public static void ProcessRawInput(IntPtr lParam)
        {
            uint dwSize = 0;
            if (GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) == 0xFFFFFFFF)
                return;

            IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
            try
            {
                uint result = GetRawInputData(lParam, RID_INPUT, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
                if (result == 0xFFFFFFFF) return;

                RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);

                if (raw.header.dwType == 0) // Mouse
                {
                    const ushort MOUSE_MOVE_RELATIVE = 0x0000;
                    if ((raw.mouse.usFlags & 0x0001) == MOUSE_MOVE_RELATIVE)
                    {
                        int dx = raw.mouse.lLastX;
                        int dy = raw.mouse.lLastY;
                        OnMouseDelta?.Invoke(dx, dy);
                    }

                    ushort bf = raw.mouse.usButtonFlags;
                    // Left Button
                    if ((bf & 0x0001) != 0) OnMouseButton?.Invoke(MouseButtons.Left, true);
                    if ((bf & 0x0002) != 0) OnMouseButton?.Invoke(MouseButtons.Left, false);
                    // Right Button
                    if ((bf & 0x0004) != 0) OnMouseButton?.Invoke(MouseButtons.Right, true);
                    if ((bf & 0x0008) != 0) OnMouseButton?.Invoke(MouseButtons.Right, false);
                    // Middle Button
                    if ((bf & 0x0010) != 0) OnMouseButton?.Invoke(MouseButtons.Middle, true);
                    if ((bf & 0x0020) != 0) OnMouseButton?.Invoke(MouseButtons.Middle, false);
                    // XButton1
                    if ((bf & 0x0040) != 0) OnMouseButton?.Invoke(MouseButtons.XButton1, true);
                    if ((bf & 0x0080) != 0) OnMouseButton?.Invoke(MouseButtons.XButton1, false);
                    // XButton2
                    if ((bf & 0x0100) != 0) OnMouseButton?.Invoke(MouseButtons.XButton2, true);
                    if ((bf & 0x0200) != 0) OnMouseButton?.Invoke(MouseButtons.XButton2, false);

                    // Mouse Wheel
                    if ((bf & 0x0400) != 0)
                    {
                        short delta = (short)raw.mouse.usButtonData;
                        OnMouseWheel?.Invoke(delta);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
