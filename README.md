# CloudRFPlugin for MACE

This is a MACE plugin that posts an area coverage request to the CloudRF API using the currently selected MACE entity as the transmitter, downloads the returned GeoTIFF, and imports it into MACE through `Mission.Map.LayerManager.AddRasterLayerFromFile(...)`.

## Build

The installed MACE assemblies on this machine target .NET Framework 4.8.1. The Visual Studio project targets `v4.8.1`, so the normal build path is:

```powershell
msbuild CloudRFPlugin.sln /p:Configuration=Debug /p:Platform=x64
```

If the .NET Framework 4.8.1 developer targeting pack is not installed, use the included compiler build script:

```powershell
.\Build-Compiler.ps1
```

## Runtime

On first run, the plugin creates:

```text
C:\Users\Public\Documents\MACE\CloudRF
```

That folder stores plugin settings, the editable CloudRF area JSON template, raw request/response logs, and downloaded GeoTIFF outputs.

The API key is sent using CloudRF's `key` HTTP header. The default base URL is `https://api.cloudrf.com`.
