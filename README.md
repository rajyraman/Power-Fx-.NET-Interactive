PowerFx Kernel for Polyglot Notebooks (previous known as .NET Interactive Notebooks)
=====================================

This is a kernel for [Polyglot Notebooks](https://github.com/dotnet/interactive) to help people learn [Power Fx](https://github.com/microsoft/Power-Fx).

## Install .NET and Tools

1. [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)
2. [Polyglot Notebooks Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.dotnet-interactive-vscode)
3. [Data Table Extension](https://marketplace.visualstudio.com/items?itemName=RandomFractalsInc.vscode-data-table) - Optional

## Building the repo

Add PowerFx Daily Build Nuget information by creating NuGet.Config on the src/ folder
https://github.com/microsoft/Power-Fx?tab=readme-ov-file#daily-builds
https://github.com/microsoft/Power-Fx/blob/main/dailyBuilds.md#connect-to-the-feed

You can then build the csproj like any other dotnet project in Release or Debug mode.

```
dotnet build src/PowerFxDotnetInteractive.csproj -c Release
dotnet build src/PowerFxDotnetInteractive.csproj -c Debug
```

## Useful videos to watch regarding .NET Interactive Notebooks

1. [Learn C# with Interactive Notebooks](https://www.youtube.com/watch?v=xdmdR2JfKfM)
2. [NET Interactive Notebooks with C#/F# in VS Code](https://www.youtube.com/watch?v=DMYtIJT1OeU)
3. [.NET Everywhere - Windows, Linux, and Beyond](https://www.youtube.com/watch?v=ZM6OO2lkxA4)

## Samples

You can find the [Power Fx Notebook](./notebooks/PowerFx.ipynb) in the notebooks folder. You can open the notebook in [Dev Containers](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers) with Docker, Podman or locally.

## Credits

1. [Power Fx Host Sample](https://github.com/microsoft/power-fx-host-samples) for the parsing code.