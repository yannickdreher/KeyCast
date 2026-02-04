param(
    [string]$Server = "localhost",
    [int]$Port = 5000,
    [string]$LogFile = "tcp-client-log.txt"
)

$host.UI.RawUI.WindowTitle = "TCP Client - ${Server}:${Port} (ASCII Protocol)"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   TCP Test Client (ASCII Protocol)" -ForegroundColor Cyan
Write-Host "   Server: $Server`:$Port" -ForegroundColor Cyan
Write-Host "   Log: $LogFile" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Initialisiere Log-Datei
"[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] TCP Client started - connecting to ${Server}:${Port}" | Out-File -FilePath $LogFile -Encoding UTF8

try {
    $client = New-Object System.Net.Sockets.TcpClient
    Write-Host "Connecting to ${Server}:${Port}..." -ForegroundColor Yellow
    $client.Connect($Server, $Port)
    
    if ($client.Connected) {
        Write-Host "✓ Connected!" -ForegroundColor Green
        "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Connected successfully" | Out-File -FilePath $LogFile -Append -Encoding UTF8
        
        Write-Host "Waiting for keyboard input (ASCII decimal format)..." -ForegroundColor Yellow
        Write-Host "Press Ctrl+C to exit`n" -ForegroundColor Gray
        
        $stream = $client.GetStream()
        $reader = New-Object System.IO.StreamReader($stream)
        
        $receivedChars = @()
        $messageCount = 0
        
        while ($client.Connected) {
            if ($stream.DataAvailable) {
                $asciiLine = $reader.ReadLine()
                if ($asciiLine) {
                    try {
                        $charValue = [int]$asciiLine
                        $char = [char]$charValue
                        
                        # Log to file
                        "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')] RX: $asciiLine → '$char'" | Out-File -FilePath $LogFile -Append -Encoding UTF8
                        
                        # Console output
                        Write-Host "RX: " -NoNewline -ForegroundColor Cyan
                        Write-Host "$asciiLine".PadLeft(3) -NoNewline -ForegroundColor Yellow -BackgroundColor DarkBlue
                        Write-Host " → " -NoNewline -ForegroundColor DarkGray
                        
                        switch ($charValue) {
                            10 { 
                                Write-Host "[LF/Enter]" -ForegroundColor Magenta
                                if ($receivedChars.Count -gt 0) {
                                    $message = $receivedChars -join ''
                                    $messageCount++
                                    Write-Host "  Message #$messageCount`: " -NoNewline -ForegroundColor DarkCyan
                                    Write-Host $message -ForegroundColor White
                                    "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Message #$messageCount`: $message" | Out-File -FilePath $LogFile -Append -Encoding UTF8
                                    $receivedChars = @()
                                }
                            }
                            32 { 
                                Write-Host "[Space]" -ForegroundColor Magenta
                                $receivedChars += ' '
                            }
                            default {
                                if ($charValue -lt 32) {
                                    Write-Host "[CTRL+$([char]($charValue + 64))]" -ForegroundColor Red
                                }
                                else {
                                    Write-Host "'$char'" -ForegroundColor Green
                                    $receivedChars += $char
                                }
                            }
                        }
                        
                        if ($receivedChars.Count -gt 0 -and $charValue -ne 10) {
                            Write-Host "  Buffer: " -NoNewline -ForegroundColor DarkGray
                            Write-Host ($receivedChars -join '') -ForegroundColor Gray
                        }
                    }
                    catch {
                        Write-Host "Invalid ASCII: $asciiLine" -ForegroundColor Red
                        "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] ERROR: Invalid ASCII value: $asciiLine" | Out-File -FilePath $LogFile -Append -Encoding UTF8
                    }
                }
            }
            Start-Sleep -Milliseconds 50
        }
    }
}
catch {
    Write-Host ""
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
    "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] ERROR: $($_.Exception.Message)" | Out-File -FilePath $LogFile -Append -Encoding UTF8
}
finally {
    if ($reader) { $reader.Close() }
    if ($stream) { $stream.Close() }
    if ($client) { $client.Close() }
    Write-Host ""
    Write-Host "Disconnected." -ForegroundColor Gray
    Write-Host "Log saved to: $LogFile" -ForegroundColor DarkGray
    "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Disconnected" | Out-File -FilePath $LogFile -Append -Encoding UTF8
}