# Dahua Active Register NetSDK Research Report

Date: 2026-07-08

## Current Real Test Result

- Dahua terminal reaches the VPS listener through Active Register.
- `DH_DVR_SERIAL_RETURN` (`Command=1`) and `DH_DVR_SERIAL_RETURN_EX` (`Command=5`) are received.
- `RegisterId=BT-API-TEST-001` is parsed.
- `CLIENT_ResponseDevReg` succeeds.
- `fServiceCallBack` first parameter `lHandle` is non-zero.
- Controlled test `CLIENT_StartListenEx(lHandle)` fails with `ErrorSigned=-2147483644`, `ErrorHex=0x80000004`.
- No `DH_ALARM_ACCESS_CTL_EVENT = 0x3181` alarm callback arrives.

Conclusion from the test: the `fServiceCallBack lHandle` must not be treated as a confirmed `CLIENT_StartListenEx` login handle for this active-register flow.

## Files Inspected

- `backend/vendor/dahua-netsdk/include/dhnetsdk.h`
- `backend/vendor/dahua-netsdk/full-sdk/Include/Common/dhnetsdk.h` (same SDK header family)
- `backend/vendor/dahua-netsdk/full-sdk/Doc/NetSDK Programming Manual (Active Registration).pdf`
- `backend/vendor/dahua-netsdk/full-sdk/Demo/**`
- `backend/vendor/dahua-netsdk/samples/Demo/**`

The Active Registration PDF exists and has 12 pages, but text extraction is mostly unusable because the PDF text layer appears incomplete/encoded. The available C++ demos do not include a dedicated Active Register / AutoRegister alarm-event sample; they include normal login-based alarm listening.

## Exact Header Findings

### Active Register listener callback commands

`dhnetsdk.h:13222-13227` defines the service callback command enum:

- `DH_DVR_DISCONNECT = -1`: device disconnect during verification period.
- `DH_DVR_SERIAL_RETURN = 1`: device sends serial number as `char* szDevSerial`.
- `NET_DEV_AUTOREGISTER_RETURN`: device registering with serial and token, corresponding to `NET_CB_AUTOREGISTER`.
- `NET_DEV_NOTIFY_IP_RETURN`: equipment only reports IP, not used for active registration.
- `NET_DEV_AUTOREGISTER_PRIMARY_BACKUP`: primary/backup active registration scheme.
- `DH_DVR_SERIAL_RETURN_EX`: callback together with `DH_DVR_SERIAL_RETURN`, carrying serial number and redirection info, corresponding to `NET_CB_SERIAL_RETURN_INFO`.

### Active Register callback structs

`dhnetsdk.h:13229-13235`:

```c
NET_CB_SERIAL_RETURN_INFO
char szDevSerial[64];
BOOL bSupportRedirection;
char szReserved[1020];
```

`dhnetsdk.h:13237-13243`:

```c
NET_CB_AUTOREGISTER_PRIMARY_BACKUP_INFO
char szDevSerial[64];
int nType;
char szReserved[1020];
```

`dhnetsdk.h:13245-13251`:

```c
NET_CB_AUTOREGISTER
DWORD dwSize;
char szDevSerial[DH_DEV_SERIALNO_LEN];
char szToken[MAX_PATH];
```

None of these structs exposes a normal alarm/login session handle.

### Service callback signature

`dhnetsdk.h:80659`:

```c
typedef int (CALLBACK *fServiceCallBack)(LLONG lHandle, char *pIp, WORD wPort, LONG lCommand, void *pParam, DWORD dwParamLen, LDWORD dwUserData);
```

The header names the first parameter `lHandle`, not `lLoginID`, and does not document it as a login/session handle for `CLIENT_StartListenEx`.

### Alarm message callback signature

`dhnetsdk.h:80627`:

```c
typedef BOOL (CALLBACK *fMessCallBack)(LONG lCommand, LLONG lLoginID, char *pBuf, DWORD dwBufLen, char *pchDVRIP, LONG nDVRPort, LDWORD dwUser);
```

`dhnetsdk.h:80640-80643` says `NET_MESSAGE_CALLBACK_INFO.nAlarmType` is the same as `lCommand`, and `pBuf` must be cast to the corresponding struct, for example `ALARM_ACCESS_CTL_EVENT_INFO`.

### Normal alarm subscription APIs

`dhnetsdk.h:82131-82134`:

```c
CLIENT_StartListen(LLONG lLoginID);
CLIENT_StartListenEx(LLONG lLoginID);
```

These signatures explicitly require `lLoginID`.

The sample `samples/Demo/04.Alarm/dialog.cpp:393` logs in with `CLIENT_LoginWithHighLevelSecurity`, stores the returned value in `m_lLoginId`, and `samples/Demo/04.Alarm/dialog.cpp:442` calls `CLIENT_StartListenEx(m_lLoginId)`. This sample supports the conclusion that `CLIENT_StartListenEx` is intended for a normal login handle, not automatically for the `fServiceCallBack lHandle`.

### Active Register listener APIs

`dhnetsdk.h:82154`:

```c
CLIENT_ListenServer(char* ip, WORD port, int nTimeout, fServiceCallBack cbListen, LDWORD dwUserData);
```

Header comment: “actively registration function. enable service. nTimeout is invalid.”

`dhnetsdk.h:82160`:

```c
CLIENT_ResponseDevReg(char *devSerial, char* ip, WORD port, BOOL bAccept);
```

Header comment: “Respond the registration requestion from the device.”

`dhnetsdk.h:82142-82151` defines `NET_ACTIVE_RIGST_PARAMS` and `CLIENT_SetServerParam`. It includes `bManualLogout`, described as whether to logout manually; default SDK behavior is automatic logout after active registered devices disconnect. This confirms there is an active-registration server mode, but the header still does not show a login handle being returned by `CLIENT_ResponseDevReg`.

### Alarm upload center APIs

`dhnetsdk.h:2207` introduces `CLIENT_StartService NEW_ALARM_UPLOAD` event constants.

`dhnetsdk.h:6922-6935` defines `NEW_ALARM_UPLOAD`, described as “Alarm information of alarm upload function.”

`dhnetsdk.h:82296-82299`:

```c
CLIENT_StartService(WORD wPort, char *pIp = NULL, fServiceCallBack pfscb = NULL, DWORD dwTimeOut = 0xffffffff, LDWORD dwUserData = 0);
CLIENT_StopService(LLONG lHandle);
```

Header comment: “Alarm upload function. Enable service. dwTimeOut parameter is invalid.”

This is a separate service-listener API from `CLIENT_ListenServer`. It may be relevant if Dahua access terminals push alarm/event payloads to an alarm center rather than requiring `CLIENT_StartListenEx` after active registration.

### Access-control event decoder target

`dhnetsdk.h:1586`:

```c
#define DH_ALARM_ACCESS_CTL_EVENT 0x3181 // Access event(struct ALARM_ACCESS_CTL_EVENT_INFO)
```

`dhnetsdk.h:11635-11770` defines `ALARM_ACCESS_CTL_EVENT_INFO`. Relevant fields include:

- `stuTime`
- `emEventType`
- `bStatus`
- `emOpenMethod`
- `szUserID`
- `szSnapURL`
- `nPunchingRecNo`
- `szCitizenName`
- `szCardName`
- `bRealUTC`
- `RealUTC`
- `szDeviceID`
- `szUserUniqueID`
- `bUseCardNameEx`
- `szCardNameEx`
- `nSimilarity`
- `nAliveFlag`

Direction enum lines:

- `dhnetsdk.h:11551` `NET_ACCESS_CTL_EVENT_ENTRY`
- `dhnetsdk.h:11552` `NET_ACCESS_CTL_EVENT_EXIT`

Face method lines:

- `dhnetsdk.h:10027` `NET_ACCESS_DOOROPEN_METHOD_FACE_RECOGNITION = 16`
- `dhnetsdk.h:10028` `NET_ACCESS_DOOROPEN_METHOD_FACEIDCARD = 18`
- `dhnetsdk.h:10033` `NET_ACCESS_DOOROPEN_METHOD_FACE_AND_PWD = 23`
- Other face-related enum values also exist, such as 27, 41, 42, 51, 54, 56, 59, 88, 91, 93, 96, 121, 122.

## Meaning of Callback Handles

- `fMessCallBack(... LLONG lLoginID ...)` uses a parameter explicitly named `lLoginID`; it is the normal device login handle used by alarm callbacks.
- `CLIENT_StartListenEx(LLONG lLoginID)` explicitly requires a login handle.
- `fServiceCallBack(LLONG lHandle, ...)` uses a generic `lHandle`, and nearby Active Register callback structs do not document it as `lLoginID`.
- The real test proves that calling `CLIENT_StartListenEx` with this `lHandle` returns SDK error `0x80000004` and does not produce alarm callbacks.

## Does Any Found API Return a Real Login/Session Handle?

From the inspected header sections:

- `CLIENT_ResponseDevReg` returns `BOOL`, not a handle.
- `CLIENT_ListenServer` returns the server/listener handle, not per-device login handle.
- `fServiceCallBack` provides an `LLONG lHandle`, but the header does not state it is a login handle, and real testing shows it fails with `CLIENT_StartListenEx`.
- No inspected Active Register struct (`NET_CB_SERIAL_RETURN_INFO`, `NET_CB_AUTOREGISTER`, `NET_CB_AUTOREGISTER_PRIMARY_BACKUP_INFO`) contains a login/session handle.

## Is CLIENT_StartListenEx Intended for Active Register Sessions?

Based on the header and samples available in this SDK package, `CLIENT_StartListenEx` is documented and sampled for normal `lLoginID` sessions. The sample obtains `m_lLoginId` from `CLIENT_LoginWithHighLevelSecurity` and then calls `CLIENT_StartListenEx(m_lLoginId)`. It is not shown with `fServiceCallBack lHandle`.

Therefore, the current evidence says: do not use `fServiceCallBack lHandle` as a confirmed `CLIENT_StartListenEx` login handle.

## Do Active Register Devices Push Alarm/Events Through Service Callback?

Current real callbacks only show `Command=1` and `Command=5`, which are registration serial callbacks, not access events. Header command list around `EM_DEV_INFO` does not identify `DH_ALARM_ACCESS_CTL_EVENT` as a `CLIENT_ListenServer` service callback command.

However, `CLIENT_StartService` is explicitly described as an alarm upload function and uses `fServiceCallBack`. This suggests a possible separate alarm-upload center flow, but no provided sample proves access-control terminal attendance events are delivered there.

## Should CLIENT_StartService Replace CLIENT_ListenServer?

Not enough evidence yet to replace it blindly.

- `CLIENT_ListenServer` is explicitly “actively registration function” and already matches the terminal’s Active Register connection behavior.
- `CLIENT_StartService` is explicitly “Alarm upload function,” not “active registration.”
- The next likely SDK-level experiment is to run `CLIENT_StartService` on a separate test port or on the configured alarm-center port only if the Dahua terminal has an Alarm Center/Event Upload configuration pointing to that port.

This should not replace the Active Register listener until Dahua documentation or a real test confirms that the access terminal sends `DH_ALARM_ACCESS_CTL_EVENT` or equivalent upload payloads through `CLIENT_StartService`.

## Missing Official Sample

The official package in this repository lacks a C++/C#/Java sample that demonstrates the full flow:

`CLIENT_ListenServer -> CLIENT_ResponseDevReg -> obtain real per-device login/session handle -> subscribe/access alarm callback`

The available `04.Alarm` sample demonstrates only normal direct login then `CLIENT_StartListenEx`.

## Recommended Next Implementation Path

Recommended path: **D) Declare missing vendor docs/sample for the ListenServer-to-login-handle flow, while preparing C as the next controlled SDK experiment.**

Concrete next steps:

1. Keep `CLIENT_ListenServer` + `CLIENT_ResponseDevReg` for Active Register device presence and online status.
2. Keep `fServiceCallBack lHandle` strategy marked as tested and failed; do not retry the same handle/session.
3. Ask Dahua/vendor for the exact AutoRegister sample or documentation that shows how to obtain a valid `lLoginID` after `CLIENT_ResponseDevReg`, if such a flow exists.
4. In parallel, inspect terminal configuration for an “Alarm Center,” “Event Upload,” or similar server setting. If the device can be configured to upload alarms/events to a server port, test `CLIENT_StartService` on that port as a separate event-upload listener.
5. Keep attendance insertion restricted to real access-control events decoded as `DH_ALARM_ACCESS_CTL_EVENT = 0x3181` / `ALARM_ACCESS_CTL_EVENT_INFO`.

This preserves the final production architecture: Dahua terminal connects outbound to the VPS; no DirectSdkHost, no local IP login, no LAN fallback.

## CLIENT_StartService / Alarm Center / Event Upload Investigation

This section was added after the real `ServiceCallbackLHandle -> CLIENT_StartListenEx` experiment failed with `0x80000004`.

### Confirmed negative result: ServiceCallbackLHandle is not a StartListenEx login handle

- `CLIENT_ListenServer` receives `Command=1` and `Command=5`.
- `CLIENT_ResponseDevReg` succeeds.
- `fServiceCallBack lHandle` is non-zero.
- `CLIENT_StartListenEx(lHandle)` fails with `ErrorSigned=-2147483644`, `ErrorHex=0x80000004`.
- BuildTrack now treats `ServiceCallbackLHandle` as tested and failed for alarm subscription and does not retry it.

### CLIENT_StartService / CLIENT_StopService

Header findings:

- `dhnetsdk.h:82296`:

```c
CLIENT_NET_API LLONG CALL_METHOD CLIENT_StartService(WORD wPort, char *pIp = NULL, fServiceCallBack pfscb = NULL, DWORD dwTimeOut = 0xffffffff, LDWORD dwUserData = 0);
```

- `dhnetsdk.h:82299`:

```c
CLIENT_NET_API BOOL CALL_METHOD CLIENT_StopService(LLONG lHandle);
```

`CLIENT_StartService` returns an `LLONG` service handle. The callback type is the same `fServiceCallBack` used by `CLIENT_ListenServer`. The header comment says: “Alarm upload function. Enable service. dwTimeOut parameter is invalid.”

### Callback signature

- `dhnetsdk.h:80659`:

```c
typedef int (CALLBACK *fServiceCallBack)(LLONG lHandle, char *pIp, WORD wPort, LONG lCommand, void *pParam, DWORD dwParamLen, LDWORD dwUserData);
```

This callback receives:

- a service/connection handle (`lHandle`)
- remote IP and port
- command/event type (`lCommand`)
- payload pointer (`pParam`) and payload length (`dwParamLen`)

Because it has `lCommand` and `pParam`, it can technically receive command IDs and binary payloads. Whether Dahua access terminals send `DH_ALARM_ACCESS_CTL_EVENT = 0x3181` through this path must be proven with a real Alarm Center/Event Upload configuration.

### Alarm upload structs and constants

- `dhnetsdk.h:2207` marks the constants as `CLIENT_StartService NEW_ALARM_UPLOAD`.
- `dhnetsdk.h:2217` defines `DH_UPLOAD_EVENT = 0x400C` as “Scheduled upload.”
- `dhnetsdk.h:2223-2227` define additional upload alarm constants that map to `NEW_ALARM_UPLOAD`.
- `dhnetsdk.h:6922-6935` defines `NEW_ALARM_UPLOAD`, described as “Alarm information of alarm upload function.” Fields include `dwAlarmType`, `dwAlarmMask`, client IP/domain/port, occurrence time, and related alarm mask fields.

No dedicated `NET_IN_STARTSERVICE` or `NET_OUT_STARTSERVICE` struct was found in `dhnetsdk.h`; `CLIENT_StartService` uses direct scalar parameters and returns a service handle.

### Alarm Center configuration references

- `dhnetsdk.h:1267` defines `DH_DEV_ALARM_CENTER_CFG = 0x0022` as “Alarm center setup.”
- `dhnetsdk.h:14668-14669` include alarm center fields `wHostPort` and `sHostIPAddr`.

This suggests a separate Alarm Center/Event Upload feature exists in the SDK/device ecosystem. It is not the same as `CLIENT_ListenServer` Active Register serial callback.

### Access-control event target remains unchanged

- `dhnetsdk.h:1586` defines `DH_ALARM_ACCESS_CTL_EVENT = 0x3181` and maps it to `ALARM_ACCESS_CTL_EVENT_INFO`.
- `dhnetsdk.h:11635-11770` defines `ALARM_ACCESS_CTL_EVENT_INFO` with fields such as `szUserID`, `szCardName`, `szCitizenName`, `bUseCardNameEx`, `szCardNameEx`, `stuTime`, `RealUTC`, `emEventType`, `bStatus`, `emOpenMethod`, `szSnapURL`, `nPunchingRecNo`, `szDeviceID`, `szUserUniqueID`, `nScore`, `nSimilarity`, and `nAliveFlag`.

### Current implementation decision

BuildTrack now includes a disabled-by-default experimental StartService listener:

- `DAHUA_ACTIVE_REGISTER_SERVICE_MODE=ListenServer` by default.
- `DAHUA_EXPERIMENTAL_START_SERVICE_ENABLED=false` by default.
- To test the experiment: set `DAHUA_ACTIVE_REGISTER_SERVICE_MODE=StartServiceExperimental`, `DAHUA_EXPERIMENTAL_START_SERVICE_ENABLED=true`, and optionally `DAHUA_EXPERIMENTAL_START_SERVICE_PORT=7001`.
- The experimental listener does not insert attendance.
- Every callback logs command, payload length, remote endpoint, and safe first-payload bytes.
- If `lCommand == 0x3181`, it attempts diagnostic-only decode as `ALARM_ACCESS_CTL_EVENT_INFO` and logs the result.

### Recommended next experiment

Use `CLIENT_StartService` only if the Dahua terminal exposes and can be configured with Alarm Center/Event Upload server IP/port. Point that feature to the VPS test port, then check whether callbacks include either:

- `DH_ALARM_ACCESS_CTL_EVENT = 0x3181` directly, or
- an upload wrapper such as `DH_UPLOAD_EVENT` / `NEW_ALARM_UPLOAD` that contains or references access-control event details.

Until that is verified, the official Dahua/vendor AutoRegister sample showing how to obtain a valid `lLoginID` after `CLIENT_ResponseDevReg` is still needed.
