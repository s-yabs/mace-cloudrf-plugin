$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$frameworkDir = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$csc = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe"
$maceDir = "C:\Program Files\Battlespace Simulations\MACE"
$outDir = Join-Path $projectDir "bin\x64\Debug"
$outFile = Join-Path $outDir "CloudRFPlugin.dll"

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$args = @(
  "/target:library",
  "/platform:x64",
  "/langversion:7.3",
  "/out:$outFile",
  "/reference:$(Join-Path $frameworkDir "mscorlib.dll")",
  "/reference:$(Join-Path $frameworkDir "System.dll")",
  "/reference:$(Join-Path $frameworkDir "System.Core.dll")",
  "/reference:$(Join-Path $frameworkDir "System.Drawing.dll")",
  "/reference:$(Join-Path $frameworkDir "System.Net.Http.dll")",
  "/reference:$(Join-Path $frameworkDir "System.Web.Extensions.dll")",
  "/reference:$(Join-Path $frameworkDir "System.Windows.Forms.dll")",
  "/reference:$(Join-Path $frameworkDir "System.Xml.dll")",
  "/reference:$(Join-Path $maceDir "BSILib.dll")",
  "/reference:$(Join-Path $maceDir "SimulationLibrary.dll")",
  "/reference:$(Join-Path $maceDir "SignalGenerator.dll")",
  "/resource:$(Join-Path $projectDir "Resources\CloudRF.ico"),CloudRFPlugin.Resources.CloudRF.ico",
  (Join-Path $projectDir "CloudRFClient.cs"),
  (Join-Path $projectDir "CloudRFForm.cs"),
  (Join-Path $projectDir "CloudRFPlugin.cs"),
  (Join-Path $projectDir "CloudRFSettings.cs"),
  (Join-Path $projectDir "JsonTools.cs"),
  (Join-Path $projectDir "Properties\AssemblyInfo.cs")
)

& $csc @args

if ($LASTEXITCODE -ne 0) {
  throw "csc failed with exit code $LASTEXITCODE"
}

Write-Host "Built $outFile"
