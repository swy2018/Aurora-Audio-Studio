# Aurora Audio Studio

Aurora Audio Studio is a Windows desktop launcher for a local AI audio production workflow. It gives non-programmers a single, clean interface for opening and managing music generation, AI voice, singing voice conversion, vocal separation, MIDI transcription, and subtitle tools.

The app is intentionally a local-first shell: models and third-party AI tools live on the user's own machine, and Aurora helps users choose an install location, launch tools, and organize outputs.

## Features

- Music generation workspace with ACE-Step 1.5 as the default local model
- AI dubbing / text-to-speech launcher with IndexTTS2 as the default entry
- Singing voice conversion launcher with Seed-VC as the default entry
- Vocal separation / accompaniment extraction entry for audio-separator style workflows
- MIDI transcription entry for Basic Pitch style workflows
- Subtitle workflow entry for Subtitle Edit / Faster-Whisper style usage
- Chinese-first UI for creators who do not want to use command-line tools
- Config stored under the user's local app data, not beside the executable
- User-selectable model install location and output location
- No startup auto-run and no background model loading when the app is closed

## What Aurora is not

Aurora does not include model weights or redistribute third-party AI projects. The app creates launch/install scripts for supported tools, and users are responsible for following the licenses and usage terms of the models and tools they install.

## Default folder behavior

- Settings: `%LOCALAPPDATA%\\Aurora Audio Studio\\settings.json`
- Default output folder: `Desktop\\AI工作流`
- Suggested model root after choosing an install location: `<selected folder>\\LocalAI`

Users can change the output folder and the model install location from inside the app.

## Requirements

- Windows 10/11 x64
- .NET 10 Desktop Runtime, or use a self-contained release build
- Microsoft Edge WebView2 Runtime
- NVIDIA GPU recommended for local AI model workflows
- Git and Python are required by many optional model installers

## Build from source

```powershell
dotnet restore .\\work\\audio-studio\\AIAudioStudio.csproj
dotnet build .\\work\\audio-studio\\AIAudioStudio.csproj -c Release
```

Self-contained single-file publish:

```powershell
dotnet publish .\\work\\audio-studio\\AIAudioStudio.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\\publish\\Aurora
```

## Repository layout

```text
work/audio-studio/
  Program.cs                 Main Windows Forms application
  AIAudioStudio.csproj        Project file
  app.manifest                Windows app manifest
  assets/                     App icon and UI artwork
```

## Public release checklist

Before publishing a binary release, verify:

- The app starts on a clean Windows user account
- Settings are created in `%LOCALAPPDATA%`
- Output directory selection works
- Model install location selection creates a `LocalAI` folder under the selected directory
- Closing the app does not leave model processes running unexpectedly
- The release package does not include local model weights, caches, personal paths, or generated outputs

## License

MIT License. See [LICENSE](LICENSE).
