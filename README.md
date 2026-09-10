![PepperDash Essentials Plugin Logo](/images/essentials-plugin-blue.png)

# Da-Lite SCB-200 Essentials Plugin (c) 2026

## License

Provided under MIT license

## Overview

Essentials plugin for the [Da-Lite SCB-200 Serial Control Board](docs/SCB200-Inst.pdf), the RS-232 serial
controller factory-installed in Da-Lite motorized screens.

Beyond basic up / down / stop, the SCB-200 exposes Da-Lite's Screen Positioning System, so this plugin also
supports absolute and relative positioning (encoder counts, inches, millimeters), the ten aspect ratio
presets, and status readback including screen limits, viewing area dimensions, calibration state, AC motor
current and error reporting.

The plugin talks to the controller over either RS-232 port, or over TCP/IP when the optional NET-200 Ethernet
daughter board is installed.

## Dependencies

The [Essentials](https://github.com/PepperDash/Essentials) libraries are required and are referenced via
NuGet. They are restored automatically on build.

## Hardware Connection

The SCB-200 RS-232 ports are fixed and cannot be reconfigured:

| Setting      | Value |
| ------------ | ----- |
| Baud rate    | 9600  |
| Data bits    | 8     |
| Stop bits    | 1     |
| Parity       | None  |
| Flow control | None  |

When connecting through a NET-200, the factory default endpoint is `192.168.1.100` on TCP port `10001`.

Up to eight SCB-200 boards can be daisy-chained over RS-485 and addressed independently by DIP switch, with
ID 0 as the master. Only the master's RS-232 and IR ports are active; configure one Essentials device per
board and set `deviceId` to match each board's DIP switch address.

## Configuration

Device types recognized by the factory: `dalitescb200`, `dalitescreen`, `scb200`.

### Serial

```json
{
  "key": "screen-1",
  "name": "Main Screen",
  "type": "dalitescb200",
  "group": "shades",
  "properties": {
    "control": {
      "method": "com",
      "controlPortDevKey": "processor",
      "controlPortNumber": 1,
      "comParams": {
        "baudRate": 9600,
        "dataBits": 8,
        "stopBits": 1,
        "parity": "None",
        "protocol": "RS232",
        "hardwareHandshake": "None",
        "softwareHandshake": "None"
      }
    },
    "deviceId": 0,
    "pollTimeMs": 30000,
    "warningTimeoutMs": 180000,
    "errorTimeoutMs": 300000,
    "displayDeviceKey": "display-1",
    "screenLiftType": "screen"
  }
}
```

### TCP/IP (via NET-200)

```json
{
  "key": "screen-1",
  "name": "Main Screen",
  "type": "dalitescb200",
  "group": "shades",
  "properties": {
    "control": {
      "method": "tcpIp",
      "tcpSshProperties": {
        "address": "192.168.1.100",
        "port": 10001,
        "autoReconnect": true,
        "autoReconnectIntervalMs": 10000
      }
    },
    "deviceId": 0,
    "displayDeviceKey": "display-1",
    "screenLiftType": "screen"
  }
}
```

### Properties

| Property           | Type   | Default    | Description                                                                                        |
| ------------------ | ------ | ---------- | -------------------------------------------------------------------------------------------------- |
| `control`          | object | required   | Standard Essentials control object                                                                 |
| `deviceId`         | number | `0`        | RS-485 device ID, 0-7, matching the board's DIP switches. Values outside the range fall back to 0. |
| `pollTimeMs`       | number | `30000`    | Communication monitor poll interval                                                                |
| `warningTimeoutMs` | number | `180000`   | Communication monitor warning timeout                                                              |
| `errorTimeoutMs`   | number | `300000`   | Communication monitor error timeout                                                                |
| `movingPollTimeMs` | number | `1000`     | Position poll interval used while the motor is running                                             |
| `displayDeviceKey` | string | `null`     | Key of the associated display, for `IProjectorScreenLiftControl`                                   |
| `screenLiftType`   | string | `"screen"` | `"screen"` or `"lift"`                                                                             |

The SCB-200 sends no unsolicited position updates, so the plugin polls position every `movingPollTimeMs`
while the relay reports motion, and stops once the relay reports `ST`.

## Screen Semantics

The plugin implements `IProjectorScreenLiftControl` and the Essentials shade interfaces. `Raise()` and
`Lower()` describe the physical motion and are unambiguous. The shade methods follow the shade convention,
where the covering retracts to "open":

- `Open()` == `Raise()` — screen up / stowed
- `Close()` == `Lower()` — screen down / deployed

## Bridge Join Map

### Digital

| Join  | Type          | Description                                            |
| ----- | ------------- | ------------------------------------------------------ |
| 1     | To SIMPL      | Is Online                                              |
| 2     | To/From SIMPL | Connect / Disconnect & feedback                        |
| 3     | To/From SIMPL | Raise screen & is-raising feedback                     |
| 4     | To/From SIMPL | Lower screen & is-lowering feedback                    |
| 5     | To/From SIMPL | Stop screen & is-stopped feedback                      |
| 6     | To SIMPL      | Screen is at the upper limit                           |
| 7     | To SIMPL      | Screen is at the lower limit                           |
| 8     | To SIMPL      | Screen is calibrated                                   |
| 9     | To SIMPL      | Screen is busy calibrating                             |
| 10    | To SIMPL      | Rotary sensor support is enabled                       |
| 11    | From SIMPL    | Reset the SCB-200 firmware                             |
| 12    | From SIMPL    | Poll every supported value                             |
| 21-30 | To/From SIMPL | Recall aspect ratio preset 1-10 (A1-A9, A0) & feedback |
| 31-40 | From SIMPL    | Store current position to preset 6-10 (A6-A9, A0)      |

### Analog

| Join | Type          | Description                                       |
| ---- | ------------- | ------------------------------------------------- |
| 1    | To SIMPL      | Socket status                                     |
| 2    | To/From SIMPL | Position, 0 (upper limit) to 65535 (lower limit)  |
| 3    | To/From SIMPL | Position in hundredths of an inch                 |
| 4    | To/From SIMPL | Position in millimeters                           |
| 5    | To/From SIMPL | Raw encoder target position (TA)                  |
| 6    | To SIMPL      | Upper limit encoder counter value (UL)            |
| 7    | To SIMPL      | Lower limit encoder counter value (LL)            |
| 8    | To SIMPL      | AC current through the relay, in tenths of an amp |
| 9    | To SIMPL      | Viewing area width, mm                            |
| 10   | To SIMPL      | Viewing area height, mm                           |
| 11   | To SIMPL      | Most recent error code, 0 when none reported      |
| 12   | To SIMPL      | Configured RS-485 device ID                       |

### Serial

| Join | Type     | Description               |
| ---- | -------- | ------------------------- |
| 1    | To SIMPL | Device name               |
| 2    | To SIMPL | SCB-200 firmware version  |
| 3    | To SIMPL | Most recent error message |

## Protocol Notes

Commands are framed as `# <id> <verb> <parameter> [values]<CR>` and acknowledged as
`! <id> ...<CR>`. Acknowledgements addressed to a different device ID are ignored, so several boards can
share an RS-485 bus. The instruction book is inconsistent about whether the `GE` / `SE` verb is echoed in the
acknowledgement (compare `! ID GE RE UP` with the worked example `! 0 RE DN`), so the parser treats it as
optional. Likewise, set acknowledgements may interleave a status token with the value
(`! 0 SE IN OKC 12.0`), so the first parsable number in the response is used.

Error codes 13-29 from Appendix A are decoded to text on the error message join.

## Generating a NuGet Package

A NuGet package is generated automatically on build. To modify the package details, edit these properties in
`src/epi-dalite-scb200.4Series.csproj`:

1. `PackageId` — the name used to pull the package from NuGet once published
2. `PackageProjectUrl` — the plugin repo URL
3. `AssemblyTitle` — the dll file name shown on a processor when the plugin loads
<!-- START Minimum Essentials Framework Versions -->
### Minimum Essentials Framework Versions

- 2.12.1
<!-- END Minimum Essentials Framework Versions -->
<!-- START Config Example -->
### Config Example

```json
{
    "key": "GeneratedKey",
    "uid": 1,
    "name": "GeneratedName",
    "type": "DaLiteScb200",
    "group": "Group",
    "properties": {
        "control": "SampleValue",
        "deviceId": "SampleValue",
        "pollTimeMs": 0,
        "warningTimeoutMs": 0,
        "errorTimeoutMs": 0,
        "movingPollTimeMs": 0,
        "displayDeviceKey": "SampleString",
        "screenLiftType": "SampleString",
        "disableAutoLowerOnPowerOn": true,
        "disableAutoRaiseOnPowerOff": true
    }
}
```
<!-- END Config Example -->
<!-- START Supported Types -->

<!-- END Supported Types -->
<!-- START Join Maps -->
### Join Maps

#### Digitals

| Join | Type (RW) | Description |
| --- | --- | --- |
| 1 | R | Is Online |
| 2 | R | Connect (Held)/Disconnect (Release) & corresponding feedback |
| 3 | R | Raise screen (# ID SE RE UP) & is-raising feedback |
| 4 | R | Lower screen (# ID SE RE DN) & is-lowering feedback |
| 5 | R | Stop screen (# ID SE RE ST) & is-stopped feedback |
| 6 | R | Screen is at the upper limit (UL) |
| 7 | R | Screen is at the lower limit (LL) |
| 8 | R | Screen is calibrated (# ID GE CA returns ON) |
| 9 | R | Screen is busy calibrating (# ID GE CA returns BC) |
| 10 | R | Rotary sensor support is on (# ID GE SE returns ON) |
| 11 | R | Reset the SCB-200 firmware (# ID SE RS) |
| 12 | R | Query every supported value from the SCB-200 |
| 21 | R | Recall aspect ratio preset 1-10 (A1-A9, A0) & corresponding feedback |
| 31 | R | Store the current position to custom aspect ratio preset 6-10 (A6-A9, A0) |

#### Analogs

| Join | Type (RW) | Description |
| --- | --- | --- |
| 1 | R | Socket Status |
| 2 | R | Screen position scaled 0 (upper limit) to 65535 (lower limit) & corresponding feedback |
| 3 | R | Screen position in hundredths of an inch (IN) & corresponding feedback |
| 4 | R | Screen position in millimeters (MM) & corresponding feedback |
| 5 | R | Raw encoder target position (TA), 0 to LL & corresponding feedback |
| 6 | R | Upper limit encoder counter value (UL) |
| 7 | R | Lower limit encoder counter value (LL) |
| 8 | R | AC current drawn through the relay, in tenths of an amp (AC) |
| 9 | R | Viewing area width in millimeters (SW) |
| 10 | R | Viewing area height in millimeters (SH) |
| 11 | R | Most recent SCB-200 error code, 0 when no error has been reported |
| 12 | R | Configured RS-485 device ID (0-7) |

#### Serials

| Join | Type (RW) | Description |
| --- | --- | --- |
| 1 | R | Device Name |
| 2 | R | SCB-200 firmware version (SV) |
| 3 | R | Most recent SCB-200 error message, empty when no error has been reported |
<!-- END Join Maps -->
<!-- START Interfaces Implemented -->
### Interfaces Implemented

- IShadesOpenClosedFeedback
- IShadesStopFeedback
- IShadesRaiseLowerFeedback
- IShadesPosition
- IProjectorScreenLiftControl
- ICommunicationMonitor
- IOnline
<!-- END Interfaces Implemented -->
<!-- START Base Classes -->
### Base Classes

- JoinMapBaseAdvanced
- EssentialsBridgeableDevice
<!-- END Base Classes -->
<!-- START Public Methods -->
### Public Methods

- public void SendText(string text)
- public void Raise()
- public void Lower()
- public void Stop()
- public void Open()
- public void Close()
- public void MoveToUpperLimit()
- public void MoveToLowerLimit()
- public void MoveToCurrentPosition()
- public void SetTargetPosition(eScb200SetMode mode, uint value)
- public void SetPosition(ushort value)
- public void SetPositionInches(eScb200SetMode mode, double inches)
- public void SetPositionMm(eScb200SetMode mode, uint millimeters)
- public void RecallAspectRatio(uint preset)
- public void StoreAspectRatio(uint preset)
- public void Reset()
- public void Poll()
- public void PollPosition()
- public void PollLimits()
- public void QueryDeviceInfo()
- public void PollAll()
<!-- END Public Methods -->
<!-- START Bool Feedbacks -->
### Bool Feedbacks

- ConnectFeedback
- IsOnline
- ShadeIsRaisingFeedback
- ShadeIsLoweringFeedback
- IsStoppedFeedback
- ShadeIsOpenFeedback
- ShadeIsClosedFeedback
- IsInUpPosition
- IsInDownPosition
- IsCalibratedFeedback
- IsCalibratingFeedback
- RotarySensorEnabledFeedback
<!-- END Bool Feedbacks -->
<!-- START Int Feedbacks -->
### Int Feedbacks

- StatusFeedback
- PositionFeedback
- TargetPositionFeedback
- PositionInchesFeedback
- PositionMmFeedback
- UpperLimitFeedback
- LowerLimitFeedback
- AcCurrentFeedback
- ViewingAreaWidthFeedback
- ViewingAreaHeightFeedback
- ErrorCodeFeedback
<!-- END Int Feedbacks -->
<!-- START String Feedbacks -->
### String Feedbacks

- FirmwareVersionFeedback
- ErrorMessageFeedback
<!-- END String Feedbacks -->
