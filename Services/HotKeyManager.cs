using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Munyu.Services
{
    public class HotKeyManager : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_M = 0x4D;
        private const int HOTKEY_ID = 9000;

        private IntPtr _hWnd;
        private HwndSource? _source;
        private readonly Action _onHotKeyPressed;
        private bool _isRegistered;

        public HotKeyManager(Action onHotKeyPressed)
        {
            _onHotKeyPressed = onHotKeyPressed;
        }

        public bool Register(IntPtr hWnd)
        {
            _hWnd = hWnd;
            _source = HwndSource.FromHwnd(_hWnd);
            _source?.AddHook(WndProc);

            // Register Ctrl + Shift + M
            _isRegistered = RegisterHotKey(_hWnd, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_M);
            return _isRegistered;
        }

        public void Unregister()
        {
            if (_isRegistered && _hWnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hWnd, HOTKEY_ID);
                _isRegistered = false;
            }

            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                _onHotKeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
        }
    }
}
