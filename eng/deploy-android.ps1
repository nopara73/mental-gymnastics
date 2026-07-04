param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",

    [string] $Project = ".\src\MentalGymnastics.Android\MentalGymnastics.Android.csproj"
)

$ErrorActionPreference = "Stop"

$devices = & adb devices
$authorizedDevices = $devices | Select-Object -Skip 1 | Where-Object { $_ -match "\tdevice$" }

if (-not $authorizedDevices)
{
    throw "No authorized Android device found. Enable USB debugging, connect the phone, accept the RSA prompt, then run 'adb devices'."
}

dotnet build $Project -f net10.0-android -c $Configuration -t:Run
