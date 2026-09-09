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

| Setting       | Value     |
| ------------- | --------- |
| Baud rate     | 9600      |
| Data bits     | 8         |
| Stop bits     | 1         |
| Parity        | None      |
| Flow control  | None      |

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
    "errorTimeoutMs": 300000
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

| Property            | Type   | Default    | Description                                                                                       |
| ------------------- | ------ | ---------- | ------------------------------------------------------------------------------------------------- |
| `control`           | object | required   | Standard Essentials control object                                                                 |
| `deviceId`          | number | `0`        | RS-485 device ID, 0-7, matching the board's DIP switches. Values outside the range fall back to 0. |
| `pollTimeMs`        | number | `30000`    | Communication monitor poll interval                                                                |
| `warningTimeoutMs`  | number | `180000`   | Communication monitor warning timeout                                                              |
| `errorTimeoutMs`    | number | `300000`   | Communication monitor error timeout                                                                |
| `movingPollTimeMs`  | number | `1000`     | Position poll interval used while the motor is running                                             |
| `displayDeviceKey`  | string | `null`     | Key of the associated display, for `IProjectorScreenLiftControl`                                   |
| `screenLiftType`    | string | `"screen"` | `"screen"` or `"lift"`                                                                             |

The SCB-200 sends no unsolicited position updates, so the plugin polls position every `movingPollTimeMs`
while the relay reports motion, and stops once the relay reports `ST`.

## Screen Semantics

The plugin implements `IProjectorScreenLiftControl` and the Essentials shade interfaces. `Raise()` and
`Lower()` describe the physical motion and are unambiguous. The shade methods follow the shade convention,
where the covering retracts to "open":

* `Open()` == `Raise()` — screen up / stowed
* `Close()` == `Lower()` — screen down / deployed

## Bridge Join Map

### Digital

| Join | Type          | Description                                            |
| ---- | ------------- | ------------------------------------------------------ |
| 1    | To SIMPL      | Is Online                                              |
| 2    | To/From SIMPL | Connect / Disconnect & feedback                        |
| 3    | To/From SIMPL | Raise screen & is-raising feedback                     |
| 4    | To/From SIMPL | Lower screen & is-lowering feedback                    |
| 5    | To/From SIMPL | Stop screen & is-stopped feedback                      |
| 6    | To SIMPL      | Screen is at the upper limit                           |
| 7    | To SIMPL      | Screen is at the lower limit                           |
| 8    | To SIMPL      | Screen is calibrated                                   |
| 9    | To SIMPL      | Screen is busy calibrating                             |
| 10   | To SIMPL      | Rotary sensor support is enabled                       |
| 11   | From SIMPL    | Reset the SCB-200 firmware                             |
| 12   | From SIMPL    | Poll every supported value                             |
| 21-30| To/From SIMPL | Recall aspect ratio preset 1-10 (A1-A9, A0) & feedback |
| 31-40| From SIMPL    | Store current position to preset 6-10 (A6-A9, A0)      |

### Analog

| Join | Type          | Description                                             |
| ---- | ------------- | ------------------------------------------------------- |
| 1    | To SIMPL      | Socket status                                           |
| 2    | To/From SIMPL | Position, 0 (upper limit) to 65535 (lower limit)        |
| 3    | To/From SIMPL | Position in hundredths of an inch                       |
| 4    | To/From SIMPL | Position in millimeters                                 |
| 5    | To/From SIMPL | Raw encoder target position (TA)                        |
| 6    | To SIMPL      | Upper limit encoder counter value (UL)                  |
| 7    | To SIMPL      | Lower limit encoder counter value (LL)                  |
| 8    | To SIMPL      | AC current through the relay, in tenths of an amp       |
| 9    | To SIMPL      | Viewing area width, mm                                  |
| 10   | To SIMPL      | Viewing area height, mm                                 |
| 11   | To SIMPL      | Most recent error code, 0 when none reported            |
| 12   | To SIMPL      | Configured RS-485 device ID                             |

### Serial

| Join | Type     | Description              |
| ---- | -------- | ------------------------ |
| 1    | To SIMPL | Device name              |
| 2    | To SIMPL | SCB-200 firmware version |
| 3    | To SIMPL | Most recent error message|

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
