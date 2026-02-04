using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeyCast.Service.Services
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
            // Capture the native thread ID of this thread
            _threadId = GetCurrentThreadId();

            _hookCallback = HookCallback;
            _hookId = SetHook(_hookCallback);

            if (_hookId == IntPtr.Zero)
            {
                _logger.LogError("Could not install keyboard hook. Administrator privileges required.");
                return;
            }

            _logger.LogInformation("Keyboard hook installed (ID: {HookId}) on Thread {ThreadId}", _hookId, _threadId);

            // Windows Message Loop
            MSG msg;
            // GetMessage pumps the message loop. It returns 0 when WM_QUIT is received.
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
                char key = ConvertVirtualKeyToChar(vkCode);
                
                if (key != '\0')
                {
                    _logger.LogDebug("Key pressed: {Key} (VK: {VkCode})", key, vkCode);
                    KeyPressed?.Invoke(this, key);
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private static char ConvertVirtualKeyToChar(int vkCode)
        {
            // Check if Shift is pressed
            bool shiftPressed = (GetKeyState(0x10) & 0x8000) != 0;
            bool capsLock = (GetKeyState(0x14) & 0x0001) != 0;
            bool upperCase = shiftPressed ^ capsLock;

            return vkCode switch
            {
                // Letters (A-Z)
                >= 0x41 and <= 0x5A => upperCase ? (char)vkCode : (char)(vkCode + 32),
                
                // Numbers and special characters
                0x30 => shiftPressed ? ')' : '0',
                0x31 => shiftPressed ? '!' : '1',
                0x32 => shiftPressed ? '@' : '2',
                0x33 => shiftPressed ? '#' : '3',
                0x34 => shiftPressed ? '$' : '4',
                0x35 => shiftPressed ? '%' : '5',
                0x36 => shiftPressed ? '^' : '6',
                0x37 => shiftPressed ? '&' : '7',
                0x38 => shiftPressed ? '*' : '8',
                0x39 => shiftPressed ? '(' : '9',
                
                // Numpad
                >= 0x60 and <= 0x69 => (char)('0' + (vkCode - 0x60)),
                
                // Special keys
                0x0D => '\n',  // Enter
                0x20 => ' ',   // Space
                0xBE => shiftPressed ? '>' : '.',
                0xBC => shiftPressed ? '<' : ',',
                0xBD => shiftPressed ? '_' : '-',
                0xBB => shiftPressed ? '+' : '=',
                
                _ => '\0'
            };
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            
            // Stop message loop
            if (_messageLoopThread?.IsAlive == true && _threadId != 0)
            {
                // Send WM_QUIT (0x0012) to the specific native thread
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