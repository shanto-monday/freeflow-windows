# FreeFlow for Windows

Talk, and clean text appears where your cursor is. A small, free Windows app based on [FreeFlow](https://github.com/zachlatta/freeflow) (Mac).

**[Download the latest version](../../releases/latest)**: get `FreeFlow-Windows.zip`, unzip it and double-click `FreeFlow.exe`. It updates itself.

## What it does
- Hold your talk key (default **Right Ctrl**), speak, let go. Your words are pasted, cleaned up.
- Double-tap for hands-free. Esc cancels.
- **Runs on your graphics card.** Pick *My GPU* and speech turns into text on your PC: about 0.4 s for a 15 s clip on an RTX 3060. It works offline and your voice never leaves the PC. NVIDIA cards use CUDA; other PCs use a smaller model on the CPU. One-time download (1.2 GB with NVIDIA, 200 MB without).
- **Sounds like you.** Removes "um" and "uh", keeps your own words, and handles changes of mind ("Thursday, actually no, Friday" becomes "Friday"). Short chat messages stay casual (no full stop at the end, or all lowercase if you like).
- Vocabulary list for names and words it should always spell right.
- About 50 MB of memory. No account, no screenshots, no tracking.

## Needs
Windows 10 or 11 and a microphone. A free [Groq key](https://console.groq.com/keys) for Cloud mode and for clean up. *My GPU* mode works without a key (but then there's no clean up).

## How it works
Mic (16 kHz) → speech to text (Groq `whisper-large-v3`, or local [whisper.cpp](https://github.com/ggml-org/whisper.cpp) `large-v3-turbo` on the GPU) → clean up with Groq `gpt-oss-120b` → pasted with Ctrl+V, then your old clipboard is put back.

## Build
`powershell -ExecutionPolicy Bypass -File build.ps1` uses the C# compiler that comes with Windows. Nothing to install.
- `FreeFlow.exe` = personal build (key from `secret.key`, which is never committed).
- `dist\` = public build + zip.
- Release: `release.ps1 -Version 3.1.0 -Notes "..."` builds, pushes and creates the GitHub release. Installed apps pick it up within 6 hours (tray menu → Check for updates does it now).

QA: `FreeFlow.exe --transcribe in.wav out.txt [local|cloud] [appName]`, `--shot out.png [adv]`, `--shot-pill out.png Listening`.

MIT license. Original FreeFlow by Zach Latta.
