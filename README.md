# Mental Gymnastics

A minimal native Android application written in C# with .NET for Android.

## Requirements

- .NET SDK 10.0.301 or newer compatible 10.0 SDK
- .NET Android workload
- Android SDK platform tools (`adb`)
- A phone with Developer options and USB debugging enabled for deployment

## Build

```powershell
dotnet build .\MentalGymnastics.sln -c Debug
```

## Deploy to a USB-connected Android phone

1. Enable Developer options on the phone.
2. Enable USB debugging.
3. Connect the phone over USB and accept the RSA debugging prompt.
4. Verify that the device is authorized:

```powershell
adb devices
```

5. Build, install, and launch the app:

```powershell
.\eng\deploy-android.ps1
```

The app package ID is `com.nopara73.mentalgymnastics`.
