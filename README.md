
<img src="Banner.png">

# UnGate11

UnGate11 is a full GUI program that bypasses Windows 11 installation restrictions and helps you install/manage Windows 11 on unsupported hardware.

## Features

- **Patch/Unpatch System:** Bypasses requirements such as TPM, Secure Boot, RAM, and CPU, by modifying registry and system files related to `SetupHost.exe`.
- **Refresh Windows Update:** Reset Windows Update components, clear caches, and remove forced upgrade assistants.
- **Advanced Tools (Beta):**
  - Change Windows edition (Professional, Enterprise, etc.)
  - Check activation status (Windows & Office)
  - HWID Activation (digital license)

## How It Works

UnGate11 modifies the registry and creates a script (`get11.cmd`) to:
- Bypass hardware checks (TPM, Secure Boot, CPU)
- Enable Windows 11 installation via Windows Update or ISO
- Use "Skip TPM Check on Dynamic Update" by AveYo

## Usage

### Install/Update to Windows 11
1. Run UnGate11 as administrator
2. Click **Patch** to apply modifications
3. Install via:
   - **Windows Update:** Settings > Windows Update
   - **ISO:** Mount/extract ISO and run `setup.exe`

### Refresh Windows Update
1. Click **Refresh Windows Update**
2. Wait for completion
3. Try setup again or restart if needed

### Advanced Tools (Beta)
- Change Windows edition (requires valid product key)
- Check activation status (Windows/Office)
- HWID Activation (digital license)

*Advanced Tools are experimental and subject to change.*

## System Requirements

- Windows 10 version 2004 (build 19041) or later
- All Windows 11 versions

*For updating existing Windows installations only.*

## Fresh Installation

For clean installs on unsupported hardware, use [Rufus](https://rufus.ie/) to create a bootable USB that:
- Bypasses TPM, Secure Boot, RAM requirements
- Removes Microsoft account requirement
- Disables BitLocker and telemetry
- Offers other customizations

## Tips & Troubleshooting

- Complete/refresh Windows Update before setup
- Use ISO method for latest updates
- If setup errors occur, refresh Windows Update, restart, and try again

## Download

Get the pre-built executable from [Releases](https://github.com/DynamiByte/UnGate11/releases).

**No installation needed** – just download and run!

**Current Version:** v1.0.0 (Advanced Tools Beta 1)

## License & Credits

Open source and free to use. Please star the repo if you find it useful!

**Credits:**
- `SetupHost.exe` patch from on "Skip TPM Check on Dynamic Update V13" by AveYo
