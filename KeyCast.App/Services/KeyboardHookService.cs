using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyCast.App.Services
{
    public class KeyboardHookService(ILogger<KeyboardHookService> logger) : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private readonly ILogger<KeyboardHookService> _logger = logger;
        private LowLevelKeyboardProc? _hookCallback;
        private IntPtr _hookId = IntPtr.Zero;
        private Thread? _messageLoopThread;
        private volatile bool _isRunning;
        private volatile uint _threadId;

        public event EventHandler<char>? KeyPressed;

        public void Start()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _messageLoopThread = new Thread(MessageLoopThread)
            {
                IsBackground = false,
                Name = "Keyboard Hook Message Loop"
            };
            _messageLoopThread.Start();

            _logger.LogInformation("Keyboard Hook Service started");
        }

        private void MessageLoopThread()
        {
            _threadId = GetCurrentThreadId();
            _hookCallback = HookCallback;
            _hookId = SetHook(_hookCallback);

            if (_hookId == IntPtr.Zero)
            {
                _logger.LogError("Could not install keyboard hook. Administrator privileges required.");
                return;
            }

            _logger.LogInformation("Keyboard hook installed (ID: {HookId}) on Thread {ThreadId}", _hookId, _threadId);

            MSG msg;
            while (_isRunning && GetMessage(out msg, IntPtr.Zero, 0, 0) != 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            UnhookWindowsHookEx(_hookId);
            _logger.LogInformation("Keyboard hook uninstalled");
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var currentProcess = Process.GetCurrentProcess();
            using var currentModule = currentProcess.MainModule;
            
            if (currentModule?.ModuleName == null)
                return IntPtr.Zero;

            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(currentModule.ModuleName), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                int scanCode = Marshal.ReadInt32(lParam, sizeof(int));

                char key = ConvertVirtualKeyToChar(vkCode, scanCode);
                
                if (key != '\0')
                {
                    _logger.LogDebug("Key pressed: {Key} (VK: {VkCode})", key, vkCode);
                    KeyPressed?.Invoke(this, key);
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private char ConvertVirtualKeyToChar(int vkCode, int scanCode)
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            uint foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
            IntPtr keyboardLayout = GetKeyboardLayout(foregroundThreadId);

            byte[] keyState = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                short keyStatus = GetKeyState(i);
                keyState[i] = (byte)((keyStatus >> 8) | (keyStatus & 1));
            }

            StringBuilder sb = new(5);
            int result = ToUnicodeEx((uint)vkCode, (uint)scanCode, keyState, sb, sb.Capacity, 0, keyboardLayout);
            
            if (result > 0 && sb.Length > 0)
            {
                return sb[0];
            }

            if (vkCode == 0x0D) return '\n';

            return '\0';
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            
            if (_messageLoopThread?.IsAlive == true && _threadId != 0)
            {
                PostThreadMessage(_threadId, 0x0012, IntPtr.Zero, IntPtr.Zero); 
                _messageLoopThread.Join(TimeSpan.FromSeconds(5));
            }

            _logger.LogInformation("Keyboard Hook Service stopped");
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        #region P/Invoke Declarations

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ToUnicodeEx(
            uint wVirtKey,
            uint wScanCode,
            byte[] lpKeyState,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff,
            int cchBuff,
            uint wFlags,
            IntPtr dwhkl);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        #endregion
    }
}