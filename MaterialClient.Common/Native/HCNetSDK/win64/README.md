Place the following Hikvision SDK x64 native DLLs in this directory so they are copied to build output:

- HCNetSDK.dll
- HCCore.dll
- PlayCtrl.dll
- hpr.dll
- hlog.dll
- zlib1.dll
- libcrypto-1_1-x64.dll
- libssl-1_1-x64.dll
- (optional if required by your device/features) GdiPlus.dll, SuperRender.dll, YUVProcess.dll, AudioRender.dll, NPQos.dll, HXVA.dll, MP_Render.dll

These files can be found under your SDK folder, e.g.:
E:\SDK\CH-HCNetSDKV6.1.9.48_build20230410_win64\库文件\

Commit these DLLs to your repository if your policy allows checking in third-party binaries.

