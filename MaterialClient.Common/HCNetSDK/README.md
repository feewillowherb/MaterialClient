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


Known issues (Error 107~114)
---------------------------------
If you encounter HCNetSDK errors 107 to 114 when calling the SDK, it usually means the new (V5.x+) componentized SDK is not fully loaded. You must place the `HCNetSDKCom` folder (which contains functional component DLLs) in the SAME directory as `HCNetSDK.dll`. The folder name `HCNetSDKCom` MUST NOT be changed.

Action items:
- Copy the entire `HCNetSDKCom` folder from your SDK package to this directory, so the runtime output contains both `HCNetSDK.dll` and the `HCNetSDKCom` folder side-by-side.
- Do not rename the `HCNetSDKCom` folder.
- Ensure `HCCore.dll` and other dependencies are present as listed above.

