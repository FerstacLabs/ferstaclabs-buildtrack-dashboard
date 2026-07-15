# Dahua Active Register Real Flow Investigation

Date: 2026-07-14

## Context

Previous real testing proved this sequence:

1. `CLIENT_ListenServer` receives `DH_DVR_SERIAL_RETURN` and `DH_DVR_SERIAL_RETURN_EX`.
2. `CLIENT_ResponseDevReg` returns success.
3. The `fServiceCallBack` first parameter (`lHandle`) is non-zero.
4. Calling `CLIENT_StartListenEx(lHandle)` fails with `0x80000004`.

Conclusion from the real test: the service callback `lHandle` is not a valid `CLIENT_StartListenEx` login/session handle.

This investigation looked only at the official SDK files under `backend/vendor/dahua-netsdk/`. No local IP / DirectSdkHost / CGI polling path is used for Active Register.

## Files searched

- `backend/vendor/dahua-netsdk/include/dhnetsdk.h`
- `backend/vendor/dahua-netsdk/include/dhconfigsdk.h`
- `backend/vendor/dahua-netsdk/full-sdk/Include/Common/dhnetsdk.h`
- `backend/vendor/dahua-netsdk/full-sdk/Demo/`
- `backend/vendor/dahua-netsdk/samples/Demo/`
- `backend/vendor/dahua-netsdk/full-sdk/Doc/`

The SDK package includes `NetSDK Programming Manual (Active Registration).pdf`, but the current local environment does not have `pdftotext` or Python PDF extraction libraries installed, so the concrete implementation evidence below comes from headers and shipped samples.

## Exact SDK findings

### Active Register callback commands

`backend/vendor/dahua-netsdk/include/dhnetsdk.h` around lines `13221-13251` defines the `CLIENT_ListenServer` callback command set:

- `DH_DVR_DISCONNECT = -1`: disconnect during verification.
- `DH_DVR_SERIAL_RETURN = 1`: device sends serial number as `char* szDevSerial`.
- `NET_DEV_AUTOREGISTER_RETURN`: active-register callback with serial number and token, corresponding to `NET_CB_AUTOREGISTER`.
- `NET_DEV_NOTIFY_IP_RETURN`: reports IP only, not active registration.
- `NET_DEV_AUTOREGISTER_PRIMARY_BACKUP`: primary/backup scheme, corresponding to `NET_CB_AUTOREGISTER_PRIMARY_BACKUP_INFO`.
- `DH_DVR_SERIAL_RETURN_EX`: callback together with `DH_DVR_SERIAL_RETURN`, carrying serial/redirection info, corresponding to `NET_CB_SERIAL_RETURN_INFO`.

Structs found there:

- `NET_CB_SERIAL_RETURN_INFO`
  - `char szDevSerial[64]`
  - `BOOL bSupportRedirection`
  - reserved bytes
- `NET_CB_AUTOREGISTER_PRIMARY_BACKUP_INFO`
  - `char szDevSerial[64]`
  - `int nType`
- `NET_CB_AUTOREGISTER`
  - `DWORD dwSize`
  - `char szDevSerial[DH_DEV_SERIALNO_LEN]`
  - `char szToken[MAX_PATH]`

No field in these structs is documented as a usable `lLoginID` for `CLIENT_StartListenEx`.

### ListenServer / ResponseDevReg APIs

`backend/vendor/dahua-netsdk/include/dhnetsdk.h` around lines `82142-82160` defines:

- `CLIENT_SetServerParam(NET_ACTIVE_RIGST_PARAMS* pParam)`
  - `NET_ACTIVE_RIGST_PARAMS.bManualLogout`: whether to log out manually. SDK auto logout is default after active registered devices disconnect.
- `CLIENT_ListenServer(char* ip, WORD port, int nTimeout, fServiceCallBack cbListen, LDWORD dwUserData)`
  - comment: actively registration function, enable service.
- `CLIENT_StopListenServer(LLONG lServerHandle)`
- `CLIENT_ResponseDevReg(char *devSerial, char* ip, WORD port, BOOL bAccept)`
  - comment: respond to the registration request from the device.

This confirms `ListenServer` is the correct listener/acceptance mechanism, but not itself the alarm subscription login handle provider.

### Alarm subscription API requires login handle

`backend/vendor/dahua-netsdk/include/dhnetsdk.h` around lines `82134-82139` defines:

- `CLIENT_StartListen(LLONG lLoginID)`
- `CLIENT_StartListenEx(LLONG lLoginID)`
- `CLIENT_StopListen(LLONG lLoginID)`

The shipped alarm sample `backend/vendor/dahua-netsdk/samples/Demo/04.Alarm/dialog.cpp` around line `442` calls:

```cpp
CLIENT_StartListenEx(m_lLoginId);
```

`m_lLoginId` is the result of a normal login in that sample, not the `CLIENT_ListenServer` callback handle.

### The missing official step: active-register login via CLIENT_LoginEx / CLIENT_LoginEx2

`backend/vendor/dahua-netsdk/include/dhnetsdk.h` around lines `4312-4317` defines `EM_LOGIN_SPAC_CAP_TYPE`:

- `EM_LOGIN_SPEC_CAP_TCP = 0`
- `EM_LOGIN_SPEC_CAP_ANY = 1`
- `EM_LOGIN_SPEC_CAP_SERVER_CONN = 2 // auto sign up login`

`backend/vendor/dahua-netsdk/include/dhnetsdk.h` around lines `80851-80868` documents the login interfaces:

- `CLIENT_LoginEx(...)`
- `CLIENT_LoginEx2(...)`

The comments explicitly say:

- `nSpecCap = 2 is login with active registeration`
- `void* pCapParam fill in Actively registered device ID`

This is the first official SDK evidence of a non-local-IP way to obtain a real login handle for an Active Register device. The active-register device ID is passed through `pCapParam`; the login is not a LAN/direct IP login.

### StartService / Alarm Center / Event Upload

`backend/vendor/dahua-netsdk/include/dhnetsdk.h` around lines `2207+` defines `CLIENT_StartService` upload constants such as:

- `DH_UPLOAD_ALARM = 0x4000`
- `DH_UPLOAD_EVENT = 0x400C`
- `DH_UPLOAD_IVS = 0x400D`

`backend/vendor/dahua-netsdk/include/dhnetsdk.h` around lines `80640-80643` defines `NET_MESSAGE_CALLBACK_INFO`, where:

- `nAlarmType` is an alarm type such as `DH_ALARM_ACCESS_CTL_EVENT`, same as `lCommand` in message callbacks.
- `pBuf` is cast to the corresponding struct such as `ALARM_ACCESS_CTL_EVENT_INFO`.

`CLIENT_StartService` itself is declared around lines `82296-82299`:

- `CLIENT_StartService(WORD wPort, char *pIp = NULL, fServiceCallBack pfscb = NULL, DWORD dwTimeOut = 0xffffffff, LDWORD dwUserData = 0)`
- `CLIENT_StopService(LLONG lHandle)`

However, the SDK samples bundled here do not include a working Active Register or Alarm Center sample for access-control face events. Existing code keeps StartService as experimental diagnostics only.

### Access-control event constants and struct

`backend/vendor/dahua-netsdk/include/dhnetsdk.h` around line `1586` defines:

- `DH_ALARM_ACCESS_CTL_EVENT = 0x3181`
- comment: access event, struct `ALARM_ACCESS_CTL_EVENT_INFO`

`backend/vendor/dahua-netsdk/include/dhnetsdk.h` around lines `11635-11770` defines `ALARM_ACCESS_CTL_EVENT_INFO`, including fields already mapped in BuildTrack:

- `szUserID`
- `szCardName`
- `szCitizenName`
- `bUseCardNameEx`
- `szCardNameEx`
- `stuTime`
- `bRealUTC`
- `RealUTC`
- `emEventType`
- `bStatus`
- `emOpenMethod`
- `szSnapURL`
- `nPunchingRecNo`
- `szDeviceID`
- `szUserUniqueID`
- `nScore`
- `nSimilarity`
- `nAliveFlag`

## Correct flow inferred from SDK headers

The strongest official flow found is:

1. Start NetSDK Active Register service with `CLIENT_ListenServer`.
2. Device connects and callback receives `DH_DVR_SERIAL_RETURN` / `DH_DVR_SERIAL_RETURN_EX` / possibly `NET_DEV_AUTOREGISTER_RETURN`.
3. Parse register device ID from the callback payload.
4. Match BuildTrack `Device.RegisterDeviceId`.
5. Call `CLIENT_ResponseDevReg(registerDeviceId, remoteIp, remotePort, TRUE)`.
6. After successful response, call `CLIENT_LoginEx` or `CLIENT_LoginEx2` with:
   - `nSpecCap` / `emSpecCap = EM_LOGIN_SPEC_CAP_SERVER_CONN` (`2`)
   - `pCapParam = actively registered device ID`
   - admin username/password from the configured device
   - no local device IP required by the business model
7. If that login returns a non-zero `lLoginID`, call `CLIENT_StartListenEx(lLoginID)`.
8. Access events should arrive through the configured `CLIENT_SetDVRMessCallBack` callback.
9. Only `lCommand == DH_ALARM_ACCESS_CTL_EVENT (0x3181)` should create canonical Dahua access records.
10. Canonical records should go through `IDahuaAccessRecordIngestionPipeline` with source `dahua_active_register`.

## What is not supported by evidence

- Using `fServiceCallBack` `lHandle` directly with `CLIENT_StartListenEx` is not supported by headers and failed in real testing with `0x80000004`.
- No official sample in this SDK package demonstrates `CLIENT_ListenServer` + `CLIENT_ResponseDevReg` + `CLIENT_StartListenEx` directly.
- No bundled sample demonstrates `CLIENT_StartService` receiving `DH_ALARM_ACCESS_CTL_EVENT` for DHI-ASI6213J-MW face terminal events.
- No local IP fallback is needed or implemented for this Active Register path.

## Implementation decision

Implemented a guarded `LoginExServerConn` strategy:

- Triggered only when `DAHUA_ACTIVE_REGISTER_INGESTION_ENABLED=true`.
- Runs only after successful `CLIENT_ResponseDevReg`.
- Calls the existing native wrapper `TryLoginActiveRegister`, which uses `CLIENT_LoginEx` with `EM_LOGIN_SPEC_CAP_SERVER_CONN = 2` and `pCapParam = RegisterDeviceId`.
- If login succeeds, the returned handle is used with `CLIENT_StartListenEx`.
- If login fails, diagnostics are set to `ServerConnLoginFailed` and no fake session handle is used.
- Service callback `lHandle` is still treated as tested-failed and is not retried as a subscription handle.

## Remaining TODO

If `LoginExServerConn` fails on the real device, request the official Dahua AutoRegister demo/sample or vendor confirmation for this exact model/firmware. The missing sample should demonstrate how to obtain `lLoginID` after `CLIENT_ResponseDevReg` without direct local IP login.

## Remote endpoint LoginEx strategy expansion

Real VPS validation showed `CLIENT_ResponseDevReg` can succeed for `DH_DVR_SERIAL_RETURN_EX`, while `CLIENT_LoginEx` / `CLIENT_LoginEx2` with `EM_LOGIN_SPEC_CAP_SERVER_CONN` still returned a zero login handle and `0x8000006C` for the original empty-IP / register-id strategies.

Relevant SDK header findings remain unchanged:

- `CLIENT_LoginEx(const char *pchDVRIP, WORD wDVRPort, const char *pchUserName, const char *pchPassword, int nSpecCap, void* pCapParam, LPNET_DEVICEINFO lpDeviceInfo, int *error = 0)`.
- `CLIENT_LoginEx2(const char *pchDVRIP, WORD wDVRPort, const char *pchUserName, const char *pchPassword, EM_LOGIN_SPAC_CAP_TYPE emSpecCap, void* pCapParam, LPNET_DEVICEINFO_Ex lpDeviceInfo, int *error = 0)`.
- Header comment: `nSpecCap = 2 is login with active registeration, void* pCapParam fill in Actively registered device ID`.
- No SDK header/sample in the current package shows a different active-register `pCapParam` struct for this flow.

The worker now also tries remote callback endpoint variants after `CLIENT_ResponseDevReg` succeeds:

- `LoginExRemoteEndpoint`: `ip = callback remote IP`, `port = callback remote source port`, `pCapParam = RegisterDeviceId`.
- `LoginEx2RemoteEndpoint`: same with `CLIENT_LoginEx2`.
- `LoginExDeviceIdWithRemotePort`: `ip = RegisterDeviceId`, `port = callback remote source port`, `pCapParam = RegisterDeviceId`.
- `LoginEx2DeviceIdWithRemotePort`: same with `CLIENT_LoginEx2`.

This is still not a local/LAN fallback. It only uses the remote endpoint supplied by the real Active Register callback.

## High-level login diagnostics phase

After real VPS testing, the original seven `CLIENT_LoginEx` / `CLIENT_LoginEx2` Active Register server-connection strategies still returned a zero login handle. The SDK exposes `CLIENT_LoginWithHighLevelSecurity` with:

- `NET_IN_LOGIN_WITH_HIGHLEVEL_SECURITY.dwSize = sizeof(NET_IN_LOGIN_WITH_HIGHLEVEL_SECURITY)`
- fixed ANSI fields `szIP[64]`, `szUserName[64]`, `szPassword[64]`
- `emSpecCap = EM_LOGIN_SPEC_CAP_SERVER_CONN` (`2`)
- `pCapParam` documented the same way as `CLIENT_LoginEx`: active-register device ID
- `NET_OUT_LOGIN_WITH_HIGHLEVEL_SECURITY.nError` as the native login error output

No bundled sample shows Active Register with a different `pCapParam` struct. Samples use high-level login for normal TCP login with `EM_LOGIN_SPEC_CAP_TCP`.

The worker now keeps the original seven raw-register-id strategies first, then adds:

- null-terminated `RegisterDeviceId` variants for existing `CLIENT_LoginEx` / `CLIENT_LoginEx2` strategies
- high-level login variants for empty IP, register-id-as-IP, remote callback IP, remote endpoint IP:port, and register-id with remote source port
- raw and null-terminated `RegisterDeviceId` `pCapParam` variants for high-level login

A diagnostics-only password override is available as `DAHUA_ACTIVE_REGISTER_PASSWORD_OVERRIDE`. It is used only for Active Register login attempts, never logged as a value, and logs only `PasswordSource` plus password length.
