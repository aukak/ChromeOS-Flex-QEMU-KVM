<p align="center">
  <img src="https://truffled.lol/png/chromeemu.png" width="180" alt="logo">
</p>

<h1 align="center">ChromeOS Flex Emulator</h1>

<p align="center">Run ChromeOS Flex on Windows through WSL2 and QEMU.</p>

## Downloads

[Windows 11](https://www.microsoft.com/software-download/windows11)  
[WSL2 and Ubuntu](https://learn.microsoft.com/en-us/windows/wsl/install)  
[Hardware virtualization setup](https://support.microsoft.com/en-US/Windows/Experience/enable-virtualization-on-windows)  
[.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)  
[TigerVNC](https://github.com/TigerVNC/tigervnc/releases)  
[ChromeOS Flex image](https://support.google.com/chromeosflex/answer/11541904?hl=en)

## Files

ChromeOS disk: `C:\ChromeOSLab\flex\chromeos-flex-compressed.qcow2`

VNC Viewer: `C:\ChromeOSLab\tools\vncviewer.exe`

## Setup

Open PowerShell in the project folder.

```powershell
$windowsPath = (Get-Location).Path.Replace('\', '/')
$repo = (wsl.exe -d Ubuntu -- wslpath -a "$windowsPath").Trim()
wsl.exe -d Ubuntu -- bash "$repo/scripts/install.sh"
wsl.exe --shutdown
```

## Build

```powershell
dotnet publish .\src\ChromeOSEmu\ChromeOSEmu.csproj `
  -c Release -r win-x64 --self-contained false `
  -o "$env:USERPROFILE\Downloads\chromeosemu"
```

## Run

Open `Downloads\chromeosemu\ChromeOSEmu.exe`.

Keep the four published files together. The VM disk and published files are not stored in Git.

## Contributing

If you find a problem or have an improvement, open an issue or pull request! All help is needed.
