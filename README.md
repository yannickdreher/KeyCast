# KeyCast

A Windows Forms application that captures keyboard input and streams it to TCP clients in real-time.

## Overview

KeyCast is a desktop application that:
- 🎹 Hooks into the Windows keyboard input system
- 🌐 Listens for incoming TCP connections (default port: 5000)
- 📡 Streams all pressed keys to connected TCP clients
- 🔄 Supports multiple simultaneous client connections
- 🖥️ Runs as a visible Windows application

## Installation

### Prerequisites
- Windows 10/11 (64-bit)
- .NET 10 Desktop Runtime
- Administrator privileges (required for global keyboard hooks)

### Getting Started

1. Download the latest `KeyCast.exe` release.
2. Place the executable in a folder of your choice.

## Usage

### Running the Application

1. Right-click `KeyCast.exe`.
2. Select **Run as administrator**.
   > **Note:** Administrator privileges are required to capture keyboard input from other elevated applications and system menus.
3. The application will start and begin listening for TCP connections.

### Connecting via TCP

Once the application is running, connect to it using any TCP client:

#### Using Telnet (Windows)
```cmd
telnet localhost 5000
```

#### Using PowerShell
```powershell
$client = New-Object System.Net.Sockets.TcpClient("localhost", 5000)
$stream = $client.GetStream()
$reader = New-Object System.IO.StreamReader($stream)

while ($true) {
    $key = $reader.ReadLine()
    Write-Host "Key pressed: $key"
}
```

#### Using Python
```python
import socket

with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
    s.connect(('localhost', 5000))
    while True:
        data = s.recv(1024)
        if data:
            print(f"Key pressed: {data.decode('utf-8').strip()}")
```

#### Using netcat
```bash
nc localhost 5000
```

### Data Format

Each key press is sent as ASCII character code followed by a line feed:

```
ASCII_Code\n
```

Examples:
- `65\n` - Letter 'A' pressed (ASCII 65)
- `13\n` - Enter key pressed (ASCII 13)
- `32\n` - Spacebar pressed (ASCII 32)
- Key information is transmitted as ASCII code with line ending `\n`

## Configuration

### Change TCP Port

Create or edit `appsettings.json` in the same directory as the executable:
```json
{
  "KeyStream": {
    "TcpListenerPort": 5000
  }
}
```

Restart the application to apply changes.

## Uninstallation

Simply delete the `KeyCast.exe` file and any associated configuration files.

## Security Considerations

⚠️ **Important Security Notes:**
- This application captures **ALL keyboard input** system-wide while running.
- TCP connections are **unencrypted** (plaintext).
- By default, it listens on `localhost`.
- Use only in trusted environments.
- Consider firewall rules if exposing to the network.

## Troubleshooting

### Application won't start
1. Verify that the .NET 10 Desktop Runtime is installed.
2. Ensure you are running as Administrator.

### No keys are being captured
- The application must run with administrator privileges to hook global input reliably.
- Check if another application with exclusive keyboard hooks is conflicting.

### TCP clients can't connect
- Check your local firewall settings to ensure the port (default: 5000) is allowed.
- Verify the port number in `appsettings.json` matches your client connection.

## Technical Details

- **Framework:** .NET 10
- **Application Type:** Windows Forms Application
- **Architecture:** x64

## Building from Source

```cmd
REM Clone repository
git clone <repository-url>

REM Navigate to project
cd KeyStream

REM Build solution
dotnet build -c Release

REM Publish executable
dotnet publish KeyStream.Service/KeyStream.Service.csproj -c Release -p:PublishProfile=win-x64
```
---

**Note:** This software is intended for development, debugging, and monitoring purposes. Ensure compliance with local laws and regulations regarding keyboard monitoring.
