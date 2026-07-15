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

## Focused SDK-flow diagnostics phase after 24 failed server-connection login strategies

Real VPS validation with `DAHUA_ACTIVE_REGISTER_PASSWORD_OVERRIDE` confirmed:

- `CLIENT_ListenServer` receives real `DH_DVR_SERIAL_RETURN` / `DH_DVR_SERIAL_RETURN_EX` callbacks from the public Dahua terminal endpoint.
- `RegisterDeviceId = BT-API-TEST-001` is parsed and matched to the BuildTrack `devices` row.
- `CLIENT_ResponseDevReg` succeeds with `ErrorHex=0x00000000`.
- All current `CLIENT_LoginEx`, `CLIENT_LoginEx2`, and `CLIENT_LoginWithHighLevelSecurity` server-connection variants return `LoginHandle=0`, mostly with `0x8000006C`.
- This rules out DB lookup, Docker networking, firewall reachability, and password decryption as the primary blocker.

The implementation should stop adding blind login permutations. The remaining blocker is the exact Dahua AutoRegister SDK session flow for this terminal/firmware.

### Exact local SDK evidence used in this phase

`backend/vendor/dahua-netsdk/include/dhnetsdk.h:13222-13226` documents the service callback commands:

```cpp
DH_DVR_SERIAL_RETURN=1,                         // Device send out SN callback char* szDevSerial
DH_DVR_SERIAL_RETURN_EX, /// Callback together with DH_DVR_SERIAL_RETURN, carrying serial number, redirection and other information, corresponding to NET_CB_SERIAL_RETURN_INFO
```

`backend/vendor/dahua-netsdk/include/dhnetsdk.h:13229-13235` documents the `DH_DVR_SERIAL_RETURN_EX` payload:

```cpp
typedef struct tagNET_CB_SERIAL_RETURN_INFO
{
    char szDevSerial[64];
    BOOL bSupportRedirection;
    char szReserved[1020];
} NET_CB_SERIAL_RETURN_INFO;
```

This confirms the current parser layout for command `5`: serial/register ID at offset `0`, redirection flag at offset `64`, and reserved bytes from offset `68`. No login/session handle field is named in this struct.

`backend/vendor/dahua-netsdk/include/dhnetsdk.h:80851-80868` documents the official server-connection login parameter relationship:

```cpp
// nSpecCap = 2 is login with active registeration, void* pCapParam fill in Actively registered device ID
CLIENT_LoginEx(... int nSpecCap, void* pCapParam, ...)
CLIENT_LoginEx2(... EM_LOGIN_SPAC_CAP_TYPE emSpecCap, void* pCapParam, ...)
```

`backend/vendor/dahua-netsdk/include/dhnetsdk.h:80897-80924` documents `CLIENT_LoginWithHighLevelSecurity`; its `pCapParam` comment explicitly points back to the `CLIENT_LoginEx` relationship.

`backend/vendor/dahua-netsdk/samples/Demo/04.Alarm/dialog.cpp:393` logs in with `CLIENT_LoginWithHighLevelSecurity`, and `backend/vendor/dahua-netsdk/samples/Demo/04.Alarm/dialog.cpp:442` calls `CLIENT_StartListenEx(m_lLoginId)`. This sample demonstrates normal-login alarm subscription, not AutoRegister session acquisition.

`backend/vendor/dahua-netsdk/include/dhnetsdk.h:82154-82160` documents:

```cpp
CLIENT_ListenServer(char* ip, WORD port, int nTimeout, fServiceCallBack cbListen, LDWORD dwUserData)
CLIENT_ResponseDevReg(char *devSerial, char* ip, WORD port, BOOL bAccept)
```

Both functions are confirmed as the Active Register listener/accept path. `CLIENT_ResponseDevReg` returns `BOOL`, not `LLONG`, so it does not itself provide a login handle.

### New raw callback diagnostics

The worker now stores and exposes richer diagnostics for every service callback:

- first 256 bytes as hex (`LastServicePayloadFirst256Hex`, raw-event `PayloadFirstBytesHex`)
- parsed `RegisterDeviceId`, `Serial`, their offsets, callback remote IP/port
- parser layout statement for the command (`DH_DVR_SERIAL_RETURN`, `NET_CB_SERIAL_RETURN_INFO`, etc.)
- heuristic possible-handle candidates scanned from reserved bytes, clearly marked as candidates and not trusted session handles

These fields are available through:

- `GET /api/dahua/active-register/status`
- `GET /api/dahua/netsdk/diagnostics`
- `GET /api/dahua/active-register/raw-events?limit=100`

### Controlled experimental subscription flag

A disabled-by-default flag was added:

```text
DAHUA_ACTIVE_REGISTER_EXPERIMENTAL_SERVICE_HANDLE_SUBSCRIBE=false
```

When explicitly enabled, the worker may attempt `CLIENT_StartListenEx` on:

- the `fServiceCallBack` `lHandle`
- any non-zero handle-like candidates found in the `DH_DVR_SERIAL_RETURN_EX` reserved area

Each attempt is logged and persisted as diagnostics only. It does not mark the flow as validated or subscribed unless a real alarm callback later arrives. This preserves the proven conclusion that `fServiceCallBack lHandle` failed as a subscription handle in normal operation (`0x80000004`) while still allowing controlled SDK experiments on a VPS.

### Current conclusion

What is proven working:

- NetSDK native load/init
- `CLIENT_ListenServer` on VPS ports `7000`/`9500`
- real public Active Register callback from the Dahua terminal
- RegisterDeviceId parsing and DB device matching
- `CLIENT_ResponseDevReg` accept
- raw callback persistence and diagnostics

What remains blocked:

- the exact SDK-supported way to obtain a real `lLoginID` / session handle after Active Register acceptance for this device/firmware
- alarm callback delivery for `DH_ALARM_ACCESS_CTL_EVENT = 0x3181`

What should happen next:

- use the enriched payload/offset diagnostics from the real VPS to compare against Dahua's official AutoRegister demo
- request the official Dahua AutoRegister / RegisterServer sample that shows the post-`CLIENT_ResponseDevReg` session acquisition step
- avoid adding more blind `LoginEx` permutations until a sample/header comment identifies a different `pCapParam` struct or API flow

CGI polling remains untouched as the working local/demo fallback and continues to feed the shared attendance/security ingestion pipeline.

## Active Register alarm command identification after successful subscription

Real VPS testing later confirmed the missing session step: `LoginExRemoteEndpoint` succeeds after `CLIENT_ResponseDevReg`, returns a non-zero login handle, and `CLIENT_StartListenEx(loginHandle)` succeeds. This preserves the production Active Register architecture and does not use local IP / CGI polling.

After subscription, the worker received NetSDK alarm callbacks, but the observed commands were not the access-control attendance event `DH_ALARM_ACCESS_CTL_EVENT = 0x3181`:

| Command | SDK constant | Header evidence | Struct | Meaning |
| --- | --- | --- | --- | --- |
| `0x21A9` | `DH_ALARM_AP_CONNECT` | `backend/vendor/dahua-netsdk/include/dhnetsdk.h:1505` | `NET_ALARM_AP_CONNECT_INFO` (`dhnetsdk.h:7768-7777`) | Connection hotspot / Wi-Fi AP connect alarm, not attendance |
| `0x3173` | `DH_ALARM_CHASSISINTRUDED` | `dhnetsdk.h:1578` | `ALARM_CHASSISINTRUDED_INFO` (`dhnetsdk.h:8470-8482`) | Chassis intrusion/tamper alarm |
| `0x3169` | `DH_ALARM_NET_ABORT` | `dhnetsdk.h:1574` | `ALARM_NETABORT_INFO` (`dhnetsdk.h:8192-8200`) | Network fault alarm |
| `0x300C` | `DH_START_LISTEN_FINISH_EVENT` | `dhnetsdk.h:1538` | `START_LISTEN_FINISH_RESULT_INFO` (`dhnetsdk.h:15906-15910`) | Start-listen async completion notification |
| `0x3491` | `DH_ALARM_SIP_REGISTER_RESULT` | `dhnetsdk.h:1981` | `ALARM_SIP_REGISTER_RESULT_INFO` (`dhnetsdk.h:45025-45034`) | SIP register status event |

The actual attendance command is still documented as:

- `DH_ALARM_ACCESS_CTL_EVENT = 0x3181` at `dhnetsdk.h:1586`
- struct `ALARM_ACCESS_CTL_EVENT_INFO` at `dhnetsdk.h:11635-11770`

Implementation update:

- all alarm callbacks are now saved to `dahua_active_register_raw_events` with command hex/name, first 256 bytes of payload, decoded JSON, and decode status
- `/api/dahua/active-register/status` and `/api/dahua/netsdk/diagnostics` expose last alarm command/name/payload/decode status/decoded JSON
- known non-attendance alarms above are decoded as diagnostics only and do not create attendance/security business records
- `0x21A9` is decoded as `NET_ALARM_AP_CONNECT_INFO`; it has channel/action/time/MAC/IP fields and no user/person/access result fields
- the existing `0x3181` access-control decoder and shared ingestion pipeline remain unchanged and will create attendance/security events when that real access command arrives

Next runtime step: after face recognition, verify whether the terminal emits `0x3181`, `0x3186`, `0x3435`, or another access/person command. If it emits a different person-recognition command, use the raw-event payload and SDK header struct for that command as the next decoder target.

## Additional production alarm commands from VPS payload samples

Follow-up VPS samples produced three more alarm commands around face/access testing. These were resolved against the local Dahua SDK header and added to BuildTrack alarm diagnostics:

| Command | SDK constant | Header evidence | Struct | Runtime payload size | Attendance decision |
| --- | --- | --- | --- | --- | --- |
| `0x3185` / `12677` | `DH_ALARM_ACCESS_CTL_STATUS` | `backend/vendor/dahua-netsdk/include/dhnetsdk.h:1590` | `ALARM_ACCESS_CTL_STATUS_INFO` (`dhnetsdk.h:8918-8927`) | `332` | diagnostic-only |
| `0x218F` / `8591` | `DH_EVENT_MOTIONDETECT` | `dhnetsdk.h:1480` | `ALARM_MOTIONDETECT_INFO` (`dhnetsdk.h:56574-56591`) | `20472` | diagnostic-only |
| `0x3475` / `13429` | `DH_ALARM_SCREENSAVER` | `dhnetsdk.h:1952` | `ALARM_SCREENSAVER_INFO` (`dhnetsdk.h:45920-45927`) | `144` | diagnostic-only |

`0x3185` looks access-related because it is an access-control status event. The SDK struct confirms it contains door number, time, status, serial number, and UTC fields only. It does not contain `UserID`, `CardName`, open method, recognition result, or snapshot/person fields, so it cannot be used as an attendance record.

`0x218F` is a large video motion-detection event. The payload size matches `ALARM_MOTIONDETECT_INFO` because the struct includes 32 motion regions, event extension data, detect types, and GPS status. It does not contain worker/person recognition fields.

`0x3475` is a screen-saver status event and is unrelated to attendance.

Implementation update:

- command-name mappings were added for `0x3185`, `0x218F`, and `0x3475`
- struct decoders were added for `ALARM_ACCESS_CTL_STATUS_INFO`, `ALARM_MOTIONDETECT_INFO`, and `ALARM_SCREENSAVER_INFO`
- decoded fields are stored in raw diagnostics JSON for operator/vendor review
- none of these commands route to attendance/session/security ingestion
- the working Active Register login and subscription path remains unchanged
