using KeyCast.App.Services;

namespace KeyCast.App;

public class MainForm : Form
{
    private readonly KeyboardHookService _keyboardHookService;
    private readonly TcpListenerService _tcpListenerService;
    private readonly NotifyIcon _notifyIcon;
    private readonly ListBox _clientList;
    private readonly Label _statusLabel;

    public MainForm(KeyboardHookService keyboardHook, TcpListenerService tcpListener)
    {
        _keyboardHookService = keyboardHook;
        _tcpListenerService = tcpListener;

        Text = "KeyCast Server";
        Size = new Size(400, 320);
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        
        try 
        {
            if (File.Exists("icon.ico"))
            {
                Icon = new Icon("icon.ico");
            }
            else 
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
        }
        catch
        {
            Icon = SystemIcons.Application; 
        }

        // UI Initialization
        _statusLabel = new Label { Location = new Point(10, 10), AutoSize = true, Text = "Status: Initializing..." };
        var portLabel = new Label { Location = new Point(10, 35), AutoSize = true, Text = $"TCP Port: {_tcpListenerService.Port}" };
        var clientsLabel = new Label { Location = new Point(10, 65), AutoSize = true, Text = "Connected Clients:" };
        
        _clientList = new ListBox { Location = new Point(10, 90), Size = new Size(360, 180) };

        Controls.Add(_statusLabel);
        Controls.Add(portLabel);
        Controls.Add(clientsLabel);
        Controls.Add(_clientList);

        _notifyIcon = new NotifyIcon
        {
            Icon = Icon,
            Visible = true,
            Text = "KeyCast Server"
        };
        _notifyIcon.DoubleClick += (s, e) => ShowForm();

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Open", null, (s, e) => ShowForm());
        contextMenu.Items.Add("Exit", null, (s, e) => 
        {
            _notifyIcon.Visible = false;
            Application.Exit();
        });
        _notifyIcon.ContextMenuStrip = contextMenu;

        // Events
        Load += MainForm_Load;
        Resize += MainForm_Resize;

        // Service Integration
        _tcpListenerService.ClientConnected += (s, endpoint) => 
            InvokeSafe(() => _clientList.Items.Add(endpoint));
            
        _tcpListenerService.ClientDisconnected += (s, endpoint) => 
            InvokeSafe(() => _clientList.Items.Remove(endpoint));
            
        _keyboardHookService.KeyPressed += (s, key) => 
        { /* Optional: Status flash */ };
    }

    private void InvokeSafe(Action action)
    {
        if (IsHandleCreated) Invoke(action);
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        _statusLabel.Text = "Status: Active (Keyboard Hook running)";
        _statusLabel.ForeColor = Color.Green;
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
            _notifyIcon.ShowBalloonTip(1000, "KeyCast", "Running in background.", ToolTipIcon.Info);
        }
    }

    private void ShowForm()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }
}