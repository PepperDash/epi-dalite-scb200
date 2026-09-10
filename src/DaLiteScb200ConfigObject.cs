using Newtonsoft.Json;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Plugin.DaLite.Scb200
{
	/// <summary>
	/// Da-Lite SCB-200 plugin device configuration object
	/// </summary>
	/// <example>
	/// <code>
	/// {
	///		"key": "screen-1",
	///		"name": "Main Screen",
	///		"type": "daLiteScb200",
	///		"group": "shades",
	///		"properties": {
	///			"control": {
	///				"method": "com",
	///				"controlPortDevKey": "processor",
	///				"controlPortNumber": 1,
	///				"comParams": {
	///					"baudRate": 9600,
	///					"dataBits": 8,
	///					"stopBits": 1,
	///					"parity": "None",
	///					"protocol": "RS232",
	///					"hardwareHandshake": "None",
	///					"softwareHandshake": "None"
	///				}
	///			},
	///			"deviceId": 0,
	///			"pollTimeMs": 30000,
	///			"warningTimeoutMs": 180000,
	///			"errorTimeoutMs": 300000
	///		}
	/// }
	/// </code>
	/// </example>
	[ConfigSnippet("\"properties\":{\"control\":{},\"deviceId\":0}")]
	public class DaLiteScb200Config
	{
		/// <summary>
		/// JSON control object
		/// </summary>
		/// <remarks>
		/// The SCB-200 RS-232 ports are fixed at 9600 baud, 8 data bits, 1 stop bit, no parity, no flow control.
		/// When the optional NET-200 Ethernet daughter board is installed, connect over TCP/IP to port 10001.
		/// </remarks>
		/// <example>
		/// <code>
		/// "control": {
		///		"method": "tcpIp",
		///		"tcpSshProperties": {
		///			"address": "192.168.1.100",
		///			"port": 10001,
		///			"autoReconnect": true,
		///			"autoReconnectIntervalMs": 10000
		///		}
		///	}
		/// </code>
		/// </example>
		[JsonProperty("control")]
		public EssentialsControlPropertiesConfig Control { get; set; }

		/// <summary>
		/// RS-485 device ID of the SCB-200 being controlled
		/// </summary>
		/// <remarks>
		/// Per the SCB-200 command reference: "ID: Device ID, with a value range of 0-7, default is set to 0
		/// using DIP switches".  ID 0 is the master.  Values outside 0-7 are clamped by the device class.
		/// </remarks>
		/// <example>
		/// <code>
		/// "properties": {
		///		"deviceId": 0
		/// }
		/// </code>
		/// </example>
		[JsonProperty("deviceId")]
		public uint DeviceId { get; set; }

		/// <summary>
		/// Communication monitor poll interval, in milliseconds
		/// </summary>
		/// <value>
		/// Defaults to 30000 when unset or zero
		/// </value>
		[JsonProperty("pollTimeMs")]
		public long PollTimeMs { get; set; }

		/// <summary>
		/// Communication monitor warning timeout, in milliseconds
		/// </summary>
		/// <value>
		/// Defaults to 180000 when unset or zero
		/// </value>
		[JsonProperty("warningTimeoutMs")]
		public long WarningTimeoutMs { get; set; }

		/// <summary>
		/// Communication monitor error timeout, in milliseconds
		/// </summary>
		/// <value>
		/// Defaults to 300000 when unset or zero
		/// </value>
		[JsonProperty("errorTimeoutMs")]
		public long ErrorTimeoutMs { get; set; }

		/// <summary>
		/// Interval used to poll screen position while the screen is in motion, in milliseconds
		/// </summary>
		/// <remarks>
		/// The SCB-200 does not send unsolicited position updates, so position must be polled while the
		/// motor is running to keep position feedback current.  Polling stops when the relay reports "ST".
		/// </remarks>
		/// <value>
		/// Defaults to 1000 when unset or zero
		/// </value>
		[JsonProperty("movingPollTimeMs")]
		public long MovingPollTimeMs { get; set; }

		/// <summary>
		/// Key of the display device this screen is associated with
		/// </summary>
		/// <remarks>
		/// Consumed by <see cref="PepperDash.Essentials.Core.DeviceTypeInterfaces.IProjectorScreenLiftControl.DisplayDeviceKey"/>
		/// </remarks>
		[JsonProperty("displayDeviceKey")]
		public string DisplayDeviceKey { get; set; }

		/// <summary>
		/// Whether this device is reported to Essentials as a "screen" or a "lift"
		/// </summary>
		/// <remarks>
		/// Valid values are "screen" and "lift".  Defaults to "screen" when unset or unrecognized.
		/// </remarks>
		[JsonProperty("screenLiftType")]
		public string ScreenLiftType { get; set; }

		/// <summary>
		/// When true, this screen does NOT automatically lower when its assigned display powers on (warms up).
		/// Manual Raise/Lower/Stop still work, and the power-off auto-raise is unaffected.
		/// </summary>
		/// <remarks>
		/// Mirrors <see cref="PepperDash.Essentials.Devices.Common.Shades.ScreenLiftControllerConfigProperties.DisableAutoLowerOnPowerOn"/>.
		/// </remarks>
		[JsonProperty("disableAutoLowerOnPowerOn")]
		public bool DisableAutoLowerOnPowerOn { get; set; }

		/// <summary>
		/// When true, this screen does NOT automatically raise when its assigned display powers off (cools down).
		/// Manual Raise/Lower/Stop still work, and the power-on auto-lower is unaffected.
		/// </summary>
		/// <remarks>
		/// Mirrors <see cref="PepperDash.Essentials.Devices.Common.Shades.ScreenLiftControllerConfigProperties.DisableAutoRaiseOnPowerOff"/>.
		/// </remarks>
		[JsonProperty("disableAutoRaiseOnPowerOff")]
		public bool DisableAutoRaiseOnPowerOff { get; set; }
	}
}
