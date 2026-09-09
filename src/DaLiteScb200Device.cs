using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Core.Queues;
using PepperDash.Essentials.Core.Shades;

namespace PepperDash.Essentials.Plugin.DaLite.Scb200
{
	/// <summary>
	/// Da-Lite SCB-200 Serial Control Board plugin device
	/// </summary>
	/// <remarks>
	/// <para>
	/// Implements the RS-232 / TCP-IP (via the optional NET-200 daughter board) protocol documented in
	/// "Instruction Book for Serial Control Board SCB-200", Appendix A - Command Reference.
	/// </para>
	/// <para>
	/// Protocol summary: every command starts with "#", values are separated by a single space, and the
	/// command is terminated with a carriage return.  The controller acknowledges with a matching line
	/// prefixed by "!".  Commands addressed to a device ID other than the configured one are ignored.
	/// </para>
	/// <para>
	/// Serial port settings are fixed by the SCB-200 at 9600 8N1, no flow control.  When connecting over
	/// the NET-200 the default endpoint is 192.168.1.100:10001.
	/// </para>
	/// <para>
	/// Shade semantics: this device follows the <see cref="IShadesOpenCloseStop"/> convention where
	/// <see cref="Open"/> retracts the screen (screen up / stowed) and <see cref="Close"/> deploys it
	/// (screen down / ready to project).  <see cref="Raise"/> and <see cref="Lower"/> describe the
	/// physical motion directly and are unambiguous.
	/// </para>
	/// </remarks>
	public class DaLiteScb200Device : EssentialsBridgeableDevice, IShadesOpenClosedFeedback, IShadesStopFeedback,
		IShadesRaiseLowerFeedback, IShadesPosition, IProjectorScreenLiftControl, ICommunicationMonitor, IOnline
	{
		/// <summary>
		/// The SCB-200 terminates every command and acknowledgement with a carriage return
		/// </summary>
		private const string commsDelimiter = "\r";

		/// <summary>
		/// Number of aspect ratio presets defined by the API (A1-A9 and A0)
		/// </summary>
		private const uint aspectRatioPresetCount = 10;

		/// <summary>
		/// Lowest preset number that may be stored to.  A1-A5 are fixed ratios; A6-A9 and A0 are user definable.
		/// </summary>
		private const uint firstStorableAspectRatioPreset = 6;

		/// <summary>
		/// SCB-200 error codes, per Appendix A of the instruction book
		/// </summary>
		private static readonly Dictionary<int, string> errorMessages = new Dictionary<int, string>
		{
			{ 13, "Command Timed Out" },
			{ 14, "Busy Calibrating" },
			{ 15, "Requires Rotary Sensor" },
			{ 16, "Requires Calibration" },
			{ 17, "Already Calibrated" },
			{ 18, "Motor OverCurrent Fault" },
			{ 19, "Motor Encoder Fault" },
			{ 20, "Supported Only From NET-200" },
			{ 21, "Requires NET-200" },
			{ 22, "Supported Only At Master" },
			{ 23, "No Slave Response" },
			{ 24, "Slave Response Timeout" },
			{ 25, "Slave Response Error" },
			{ 26, "Slave Expected Continue Command" },
			{ 27, "Slave Received Partial Command Record" },
			{ 28, "Expected WebComm Init" },
			{ 29, "Command First Event" }
		};

		private readonly IBasicCommunication comms;
		private readonly GenericCommunicationMonitor commsMonitor;
		private readonly CommunicationGather commsGather;
		private readonly GenericQueue receiveQueue;
		private readonly System.Timers.Timer movingPollTimer;
		private readonly object movingPollLock = new object();

		private eScb200RelayState relayState;
		private eScb200CalibrationState calibrationState;
		private bool rotarySensorEnabled;
		private bool inUpPosition;
		private int upperLimit;
		private int lowerLimit;
		private int targetPosition = -1;
		private int positionInchesHundredths;
		private int positionMm;
		private int acCurrentTenths;
		private int viewingAreaWidthMm;
		private int viewingAreaHeightMm;
		private int lastErrorCode;
		private uint lastAspectRatioPreset;
		private string firmwareVersion = string.Empty;
		private string lastErrorMessage = string.Empty;

		#region Properties

		/// <summary>
		/// RS-485 device ID this instance addresses, 0-7
		/// </summary>
		public uint DeviceId { get; private set; }

		/// <summary>
		/// Connects/disconnects the comms of the plugin device
		/// </summary>
		/// <remarks>
		/// Triggers comms.Connect/Disconnect as well as the comms monitor start/stop
		/// </remarks>
		public bool Connect
		{
			get { return comms.IsConnected; }
			set
			{
				if (value)
				{
					comms.Connect();
					commsMonitor.Start();
				}
				else
				{
					comms.Disconnect();
					commsMonitor.Stop();
				}
			}
		}

		/// <summary>
		/// Reports connect feedback through the bridge
		/// </summary>
		public BoolFeedback ConnectFeedback { get; private set; }

		/// <summary>
		/// Reports online feedback through the bridge
		/// </summary>
		public BoolFeedback IsOnline { get; private set; }

		/// <summary>
		/// Reports socket status feedback through the bridge
		/// </summary>
		public IntFeedback StatusFeedback { get; private set; }

		/// <inheritdoc/>
		public StatusMonitorBase CommunicationMonitor { get { return commsMonitor; } }

		/// <summary>
		/// True while the screen is retracting (relay status "UP")
		/// </summary>
		public BoolFeedback ShadeIsRaisingFeedback { get; private set; }

		/// <summary>
		/// True while the screen is deploying (relay status "DN")
		/// </summary>
		public BoolFeedback ShadeIsLoweringFeedback { get; private set; }

		/// <summary>
		/// True while the motor relays are open (relay status "ST")
		/// </summary>
		public BoolFeedback IsStoppedFeedback { get; private set; }

		/// <summary>
		/// True when the screen is fully retracted, at the upper limit
		/// </summary>
		public BoolFeedback ShadeIsOpenFeedback { get; private set; }

		/// <summary>
		/// True when the screen is fully deployed, at the lower limit
		/// </summary>
		public BoolFeedback ShadeIsClosedFeedback { get; private set; }

		/// <inheritdoc/>
		public BoolFeedback IsInUpPosition { get; private set; }

		/// <summary>
		/// True when the screen is fully deployed, at the lower limit
		/// </summary>
		public BoolFeedback IsInDownPosition { get; private set; }

		/// <summary>
		/// True when the SCB-200 reports it has been calibrated
		/// </summary>
		public BoolFeedback IsCalibratedFeedback { get; private set; }

		/// <summary>
		/// True while the SCB-200 reports a calibration is in progress
		/// </summary>
		public BoolFeedback IsCalibratingFeedback { get; private set; }

		/// <summary>
		/// True when rotary sensor support is enabled on the SCB-200
		/// </summary>
		public BoolFeedback RotarySensorEnabledFeedback { get; private set; }

		/// <summary>
		/// Screen position scaled from 0 (upper limit) to 65535 (lower limit)
		/// </summary>
		/// <remarks>
		/// Reports 0 until the lower limit has been read back from the device
		/// </remarks>
		public IntFeedback PositionFeedback { get; private set; }

		/// <summary>
		/// Raw encoder target position (TA), between the upper and lower limits
		/// </summary>
		public IntFeedback TargetPositionFeedback { get; private set; }

		/// <summary>
		/// Screen position in hundredths of an inch (IN)
		/// </summary>
		public IntFeedback PositionInchesFeedback { get; private set; }

		/// <summary>
		/// Screen position in millimeters (MM)
		/// </summary>
		public IntFeedback PositionMmFeedback { get; private set; }

		/// <summary>
		/// Upper limit encoder counter value (UL)
		/// </summary>
		public IntFeedback UpperLimitFeedback { get; private set; }

		/// <summary>
		/// Lower limit encoder counter value (LL)
		/// </summary>
		public IntFeedback LowerLimitFeedback { get; private set; }

		/// <summary>
		/// AC current drawn through the relay, in tenths of an amp (AC)
		/// </summary>
		public IntFeedback AcCurrentFeedback { get; private set; }

		/// <summary>
		/// Viewing area width in millimeters (SW)
		/// </summary>
		public IntFeedback ViewingAreaWidthFeedback { get; private set; }

		/// <summary>
		/// Viewing area height in millimeters (SH)
		/// </summary>
		public IntFeedback ViewingAreaHeightFeedback { get; private set; }

		/// <summary>
		/// Most recent SCB-200 error code, 0 when no error has been reported
		/// </summary>
		public IntFeedback ErrorCodeFeedback { get; private set; }

		/// <summary>
		/// SCB-200 firmware version (SV)
		/// </summary>
		public StringFeedback FirmwareVersionFeedback { get; private set; }

		/// <summary>
		/// Most recent SCB-200 error message, empty when no error has been reported
		/// </summary>
		public StringFeedback ErrorMessageFeedback { get; private set; }

		/// <summary>
		/// Fires whenever the most recently recalled aspect ratio preset changes
		/// </summary>
		public event EventHandler<EventArgs> AspectRatioPresetChanged;

		/// <inheritdoc/>
		public event EventHandler<EventArgs> PositionChanged;

		/// <inheritdoc/>
		public bool InUpPosition { get { return inUpPosition; } }

		/// <inheritdoc/>
		public string DisplayDeviceKey { get; private set; }

		/// <inheritdoc/>
		public eScreenLiftControlType Type { get; private set; }

		#endregion

		/// <summary>
		/// Plugin device constructor
		/// </summary>
		/// <param name="key">device key</param>
		/// <param name="name">device name</param>
		/// <param name="config">device properties configuration</param>
		/// <param name="comms">communication object built by the factory</param>
		public DaLiteScb200Device(string key, string name, DaLiteScb200Config config, IBasicCommunication comms)
			: base(key, name)
		{
			this.LogInformation("Constructing new {0} instance", name);

			this.comms = comms;

			DeviceId = config.DeviceId > 7 ? 0 : config.DeviceId;
			if (config.DeviceId > 7)
			{
				this.LogWarning("Configured deviceId {id} is outside the valid range of 0-7, defaulting to 0", config.DeviceId);
			}

			DisplayDeviceKey = config.DisplayDeviceKey;
			Type = string.Equals(config.ScreenLiftType, "lift", StringComparison.OrdinalIgnoreCase)
				? eScreenLiftControlType.lift
				: eScreenLiftControlType.screen;

			movingPollTimer = new System.Timers.Timer(config.MovingPollTimeMs > 0 ? config.MovingPollTimeMs : 1000)
			{
				AutoReset = true
			};
			movingPollTimer.Elapsed += MovingPollTimer_Elapsed;

			receiveQueue = new GenericQueue(key + "-rxqueue");

			commsMonitor = new GenericCommunicationMonitor(
				this,
				this.comms,
				config.PollTimeMs > 0 ? config.PollTimeMs : 30000,
				config.WarningTimeoutMs > 0 ? config.WarningTimeoutMs : 180000,
				config.ErrorTimeoutMs > 0 ? config.ErrorTimeoutMs : 300000,
				Poll);

			BuildFeedbacks();

			var socket = this.comms as ISocketStatus;
			if (socket != null)
			{
				socket.ConnectionChange += Socket_ConnectionChange;
			}

			// The SCB-200 API is ASCII with a carriage return delimiter
			commsGather = new CommunicationGather(this.comms, commsDelimiter);
			commsGather.LineReceived += Handle_LineReceived;
		}

		private void BuildFeedbacks()
		{
			ConnectFeedback = new BoolFeedback("connect", () => Connect);
			IsOnline = new BoolFeedback("online", () => commsMonitor.IsOnline);
			StatusFeedback = new IntFeedback("status", () => (int)commsMonitor.Status);

			ShadeIsRaisingFeedback = new BoolFeedback("isRaising", () => relayState == eScb200RelayState.Up);
			ShadeIsLoweringFeedback = new BoolFeedback("isLowering", () => relayState == eScb200RelayState.Down);
			IsStoppedFeedback = new BoolFeedback("isStopped", () => relayState == eScb200RelayState.Stopped);

			IsInUpPosition = new BoolFeedback("isInUpPosition", () => inUpPosition);
			IsInDownPosition = new BoolFeedback("isInDownPosition", () => IsAtLowerLimit());
			ShadeIsOpenFeedback = new BoolFeedback("isOpen", () => inUpPosition);
			ShadeIsClosedFeedback = new BoolFeedback("isClosed", () => IsAtLowerLimit());

			IsCalibratedFeedback = new BoolFeedback("isCalibrated", () => calibrationState == eScb200CalibrationState.Calibrated);
			IsCalibratingFeedback = new BoolFeedback("isCalibrating", () => calibrationState == eScb200CalibrationState.BusyCalibrating);
			RotarySensorEnabledFeedback = new BoolFeedback("rotarySensorEnabled", () => rotarySensorEnabled);

			PositionFeedback = new IntFeedback("position", GetScaledPosition);
			TargetPositionFeedback = new IntFeedback("targetPosition", () => targetPosition < 0 ? 0 : targetPosition);
			PositionInchesFeedback = new IntFeedback("positionInches", () => positionInchesHundredths);
			PositionMmFeedback = new IntFeedback("positionMm", () => positionMm);
			UpperLimitFeedback = new IntFeedback("upperLimit", () => upperLimit);
			LowerLimitFeedback = new IntFeedback("lowerLimit", () => lowerLimit);
			AcCurrentFeedback = new IntFeedback("acCurrent", () => acCurrentTenths);
			ViewingAreaWidthFeedback = new IntFeedback("viewingAreaWidth", () => viewingAreaWidthMm);
			ViewingAreaHeightFeedback = new IntFeedback("viewingAreaHeight", () => viewingAreaHeightMm);
			ErrorCodeFeedback = new IntFeedback("errorCode", () => lastErrorCode);

			FirmwareVersionFeedback = new StringFeedback("firmwareVersion", () => firmwareVersion);
			ErrorMessageFeedback = new StringFeedback("errorMessage", () => lastErrorMessage);
		}

		/// <inheritdoc/>
		protected override void Initialize()
		{
			commsMonitor.IsOnlineFeedback.OutputChange += (o, a) =>
			{
				IsOnline.FireUpdate();
				StatusFeedback.FireUpdate();

				// The SCB-200 refuses all I/O while running its power up diagnostic routine, so the
				// static values are queried once the device is reachable rather than at construction.
				if (a.BoolValue) QueryDeviceInfo();
			};

			// Also starts the communication monitor
			Connect = true;
		}

		private void Socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
		{
			ConnectFeedback?.FireUpdate();
			StatusFeedback?.FireUpdate();
		}

		#region Command building and transmission

		/// <summary>
		/// Wraps a command body in the SCB-200 framing and sends it
		/// </summary>
		/// <param name="commandBody">command text following the device ID, for example "SE RE UP"</param>
		private void SendCommand(string commandBody)
		{
			SendText(string.Format("# {0} {1}", DeviceId, commandBody));
		}

		/// <summary>
		/// Sends text to the device plugin comms, appending the API delimiter
		/// </summary>
		/// <remarks>
		/// Can be used to test commands with the device plugin using the DEVPROPS and DEVJSON console commands
		/// </remarks>
		/// <param name="text">Command to be sent, without the trailing carriage return</param>
		public void SendText(string text)
		{
			if (string.IsNullOrEmpty(text)) return;

			this.LogVerbose("Tx: {command}", text);
			comms.SendText(text + commsDelimiter);
		}

		private static string GetSetModeToken(eScb200SetMode mode)
		{
			switch (mode)
			{
				case eScb200SetMode.Increment:
					return "INC";
				case eScb200SetMode.Decrement:
					return "DEC";
				default:
					return "FIX";
			}
		}

		/// <summary>
		/// Maps a 1-based preset number to the API aspect ratio token
		/// </summary>
		/// <param name="preset">preset number, 1-10</param>
		/// <returns>the token "A1"-"A9" or "A0", or null when the preset is out of range</returns>
		private static string GetAspectRatioToken(uint preset)
		{
			if (preset < 1 || preset > aspectRatioPresetCount) return null;

			return preset == aspectRatioPresetCount ? "A0" : "A" + preset;
		}

		/// <summary>
		/// Maps an API aspect ratio token back to a 1-based preset number
		/// </summary>
		/// <param name="token">the token "A1"-"A9" or "A0"</param>
		/// <returns>the preset number, or 0 when the token is not an aspect ratio</returns>
		private static uint GetAspectRatioPreset(string token)
		{
			if (string.IsNullOrEmpty(token) || token.Length != 2 || token[0] != 'A') return 0;
			if (!uint.TryParse(token.Substring(1), out uint digit)) return 0;

			return digit == 0 ? aspectRatioPresetCount : digit;
		}

		#endregion

		#region Motion control

		/// <summary>
		/// Retracts the screen by closing the "up" relay (# ID SE RE UP)
		/// </summary>
		public void Raise()
		{
			SendCommand("SE RE UP");
		}

		/// <summary>
		/// Deploys the screen by closing the "down" relay (# ID SE RE DN)
		/// </summary>
		public void Lower()
		{
			SendCommand("SE RE DN");
		}

		/// <summary>
		/// Stops the screen by opening both relays (# ID SE RE ST)
		/// </summary>
		public void Stop()
		{
			SendCommand("SE RE ST");
		}

		/// <summary>
		/// Retracts the screen.  Equivalent to <see cref="Raise"/>.
		/// </summary>
		public void Open()
		{
			Raise();
		}

		/// <summary>
		/// Deploys the screen.  Equivalent to <see cref="Lower"/>.
		/// </summary>
		public void Close()
		{
			Lower();
		}

		/// <summary>
		/// Drives the screen to the upper limit (# ID SE TA FIX UL)
		/// </summary>
		public void MoveToUpperLimit()
		{
			SendCommand("SE TA FIX UL");
		}

		/// <summary>
		/// Drives the screen to the lower limit (# ID SE TA FIX LL)
		/// </summary>
		public void MoveToLowerLimit()
		{
			SendCommand("SE TA FIX LL");
		}

		/// <summary>
		/// Stops the screen at its current position (# ID SE TA FIX CU)
		/// </summary>
		public void MoveToCurrentPosition()
		{
			SendCommand("SE TA FIX CU");
		}

		/// <summary>
		/// Sets the raw encoder target position (# ID SE TA [FIX, INC, DEC] value)
		/// </summary>
		/// <param name="mode">absolute or relative positioning</param>
		/// <param name="value">encoder counter value, between the upper and lower limits</param>
		public void SetTargetPosition(eScb200SetMode mode, uint value)
		{
			SendCommand(string.Format("SE TA {0} {1}", GetSetModeToken(mode), value));
		}

		/// <summary>
		/// Sets the screen position scaled from 0 (upper limit) to 65535 (lower limit)
		/// </summary>
		/// <remarks>
		/// The scaling requires the lower limit, which is read back from the device on connect.  Calls made
		/// before the lower limit is known are logged and ignored.
		/// </remarks>
		/// <param name="value">scaled position</param>
		public void SetPosition(ushort value)
		{
			if (lowerLimit <= 0)
			{
				this.LogWarning("Unable to set scaled position, lower limit is not known yet.  Polling limits.");
				PollLimits();
				return;
			}

			var counts = (uint)Math.Round((double)value / ushort.MaxValue * lowerLimit);
			SetTargetPosition(eScb200SetMode.Fixed, counts);
		}

		/// <summary>
		/// Sets the amount of viewing area deployed, in inches (# ID SE IN [FIX, INC, DEC] value)
		/// </summary>
		/// <param name="mode">absolute or relative positioning</param>
		/// <param name="inches">position in inches, to hundredths of an inch</param>
		public void SetPositionInches(eScb200SetMode mode, double inches)
		{
			if (inches < 0)
			{
				this.LogWarning("Unable to set position, {inches} inches is negative", inches);
				return;
			}

			SendCommand(string.Format("SE IN {0} {1}", GetSetModeToken(mode),
				inches.ToString("F2", CultureInfo.InvariantCulture)));
		}

		/// <summary>
		/// Sets the amount of viewing area deployed, in millimeters (# ID SE MM [FIX, INC, DEC] value)
		/// </summary>
		/// <param name="mode">absolute or relative positioning</param>
		/// <param name="millimeters">position in millimeters</param>
		public void SetPositionMm(eScb200SetMode mode, uint millimeters)
		{
			SendCommand(string.Format("SE MM {0} {1}", GetSetModeToken(mode), millimeters));
		}

		/// <summary>
		/// Recalls an aspect ratio preset (# ID SE TA FIX A[1-0])
		/// </summary>
		/// <remarks>
		/// Presets 1-5 are the fixed ratios 1:1, 1.25:1, 1.33:1, 1.66:1 and 1.78:1.  Presets 6-10 are the
		/// user definable positions A6-A9 and A0.
		/// </remarks>
		/// <param name="preset">preset number, 1-10</param>
		public void RecallAspectRatio(uint preset)
		{
			var token = GetAspectRatioToken(preset);
			if (token == null)
			{
				this.LogWarning("Unable to recall aspect ratio preset {preset}, valid presets are 1-{count}",
					preset, aspectRatioPresetCount);
				return;
			}

			SendCommand("SE TA FIX " + token);
		}

		/// <summary>
		/// Stores the current screen position to a user definable aspect ratio preset (# ID SE A[6-0])
		/// </summary>
		/// <param name="preset">preset number, 6-10</param>
		public void StoreAspectRatio(uint preset)
		{
			var token = GetAspectRatioToken(preset);
			if (token == null || preset < firstStorableAspectRatioPreset)
			{
				this.LogWarning("Unable to store aspect ratio preset {preset}, only presets {first}-{count} are user definable",
					preset, firstStorableAspectRatioPreset, aspectRatioPresetCount);
				return;
			}

			SendCommand("SE " + token);
		}

		/// <summary>
		/// Resets the SCB-200 firmware (# ID SE RS)
		/// </summary>
		/// <remarks>
		/// Equivalent to rebooting the firmware, but not a hard power reset
		/// </remarks>
		public void Reset()
		{
			SendCommand("SE RS");
		}

		#endregion

		#region Polling

		/// <summary>
		/// Polls the device
		/// </summary>
		/// <remarks>
		/// Used by the communication monitor.  Requests the relay status and the current target position,
		/// which together drive the motion and position feedbacks.
		/// </remarks>
		public void Poll()
		{
			SendCommand("GE RE");
			SendCommand("GE TA");
		}

		/// <summary>
		/// Requests the current position in encoder counts, inches and millimeters
		/// </summary>
		public void PollPosition()
		{
			SendCommand("GE TA");
			SendCommand("GE IN");
			SendCommand("GE MM");
		}

		/// <summary>
		/// Requests the upper and lower limit counter values
		/// </summary>
		public void PollLimits()
		{
			SendCommand("GE UL");
			SendCommand("GE LL");
		}

		/// <summary>
		/// Requests the values that only change on reconfiguration or recalibration
		/// </summary>
		public void QueryDeviceInfo()
		{
			SendCommand("GE SV");
			PollLimits();
			SendCommand("GE SW");
			SendCommand("GE SH");
			SendCommand("GE SE");
			SendCommand("GE CA");
			PollPosition();
			SendCommand("GE RE");
		}

		/// <summary>
		/// Requests every value the plugin tracks, including the ones that rarely change
		/// </summary>
		public void PollAll()
		{
			QueryDeviceInfo();
			SendCommand("GE AC");
			SendCommand("GE RD");
			SendCommand("GE SL");
			SendCommand("GE ST");
			SendCommand("GE TD");
		}

		/// <summary>
		/// Starts or stops the fast position poll used while the motor is running
		/// </summary>
		/// <remarks>
		/// The SCB-200 does not report position changes unsolicited, so position must be polled during motion
		/// </remarks>
		private void SetMovingPoll(bool moving)
		{
			lock (movingPollLock)
			{
				// Leave an already running timer alone so a repeated "in motion" report does not
				// push the next position poll out by another full interval
				if (moving == movingPollTimer.Enabled) return;

				if (moving)
				{
					movingPollTimer.Start();
					return;
				}

				movingPollTimer.Stop();
			}
		}

		private void MovingPollTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs args)
		{
			// System.Timers.Timer raises Elapsed on a thread pool thread and does not swallow
			// exceptions on .NET Core, so an unhandled failure here would take down the program
			try
			{
				PollPosition();
			}
			catch (Exception ex)
			{
				this.LogError(ex, "Error polling position while the screen is in motion");
			}
		}

		#endregion

		#region Feedback processing

		private void Handle_LineReceived(object sender, GenericCommMethodReceiveTextArgs args)
		{
			// Enqueues the message to be processed on the dedicated receive thread
			receiveQueue.Enqueue(new ProcessStringMessage(args.Text, ProcessFeedbackMessage));
		}

		/// <summary>
		/// Parses a single acknowledgement line from the SCB-200
		/// </summary>
		/// <remarks>
		/// Acknowledgements take the form "! ID [GE|SE] PARAM [values...]".  The instruction book is not
		/// consistent about whether the GE/SE verb is echoed (compare "! ID GE RE UP" with the worked example
		/// "! 0 RE DN"), so the verb is treated as optional.
		/// </remarks>
		/// <param name="message">a single line of received text, without the delimiter</param>
		private void ProcessFeedbackMessage(string message)
		{
			if (string.IsNullOrEmpty(message)) return;

			var trimmed = message.Trim();
			if (trimmed.Length == 0) return;

			this.LogVerbose("Rx: {response}", trimmed);

			if (!trimmed.StartsWith("!"))
			{
				this.LogDebug("Discarding unexpected response without a '!' prefix: {response}", trimmed);
				return;
			}

			var tokens = trimmed.Substring(1).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length < 2) return;

			if (!uint.TryParse(tokens[0], out uint id))
			{
				this.LogDebug("Discarding response with an unparsable device ID: {response}", trimmed);
				return;
			}

			// Multiple SCB-200 devices share the RS-485 bus, so ignore anything addressed elsewhere
			if (id != DeviceId) return;

			var index = 1;
			if (tokens[index] == "GE" || tokens[index] == "SE") index++;
			if (index >= tokens.Length) return;

			var parameter = tokens[index++];
			var values = tokens.Skip(index).ToArray();

			ProcessParameter(parameter, values, trimmed);
		}

		private void ProcessParameter(string parameter, string[] values, string rawMessage)
		{
			switch (parameter)
			{
				case "ERR":
					ProcessError(values);
					break;
				case "RE":
					ProcessRelayState(values);
					break;
				case "TA":
					if (TryGetNumericValue(values, out double target)) SetTargetPositionValue((int)Math.Round(target));
					break;
				case "IN":
					if (TryGetNumericValue(values, out double inches)) SetPositionInchesValue(inches);
					break;
				case "MM":
					if (TryGetNumericValue(values, out double mm)) SetPositionMmValue((int)Math.Round(mm));
					break;
				case "UL":
					if (TryGetNumericValue(values, out double ul)) SetUpperLimitValue((int)Math.Round(ul));
					break;
				case "LL":
					if (TryGetNumericValue(values, out double ll)) SetLowerLimitValue((int)Math.Round(ll));
					break;
				case "AC":
					if (TryGetNumericValue(values, out double amps)) SetAcCurrentValue(amps);
					break;
				case "SW":
					if (TryGetNumericValue(values, out double width)) SetViewingAreaWidthValue((int)Math.Round(width));
					break;
				case "SH":
					if (TryGetNumericValue(values, out double height)) SetViewingAreaHeightValue((int)Math.Round(height));
					break;
				case "SE":
					ProcessRotarySensorState(values);
					break;
				case "CA":
					ProcessCalibrationState(values);
					break;
				case "SV":
					SetFirmwareVersionValue(string.Join(" ", values));
					break;
				case "RS":
					this.LogInformation("SCB-200 acknowledged a firmware reset");
					break;
				case "AL":
					this.LogInformation("SCB-200 reported all settings: {settings}", string.Join(" ", values));
					break;
				default:
					ProcessAspectRatioOrUnhandled(parameter, values, rawMessage);
					break;
			}
		}

		private void ProcessAspectRatioOrUnhandled(string parameter, string[] values, string rawMessage)
		{
			var preset = GetAspectRatioPreset(parameter);
			if (preset == 0)
			{
				// RD, SL, ST, TD and the NET-200 values (MA, DH, IP, SN) are acknowledged but not tracked
				this.LogDebug("Unhandled SCB-200 response: {response}", rawMessage);
				return;
			}

			if (lastAspectRatioPreset == preset) return;

			lastAspectRatioPreset = preset;

			var handler = AspectRatioPresetChanged;
			handler?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Pulls the first numeric token out of a response value list
		/// </summary>
		/// <remarks>
		/// Set acknowledgements interleave a status token with the value, for example "! 0 SE IN OKC 12.0",
		/// so the first parsable number is used rather than assuming a fixed position.
		/// </remarks>
		private static bool TryGetNumericValue(string[] values, out double value)
		{
			foreach (var token in values)
			{
				if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
			}

			value = 0;
			return false;
		}

		private void ProcessError(string[] values)
		{
			if (values.Length == 0 || !int.TryParse(values[0], out int code))
			{
				this.LogWarning("SCB-200 reported an error without a parsable code: {values}", string.Join(" ", values));
				return;
			}

			lastErrorCode = code;
			lastErrorMessage = errorMessages.TryGetValue(code, out string message) ? message : "Unknown Error";

			this.LogWarning("SCB-200 reported error {code}: {message}", code, lastErrorMessage);

			ErrorCodeFeedback.FireUpdate();
			ErrorMessageFeedback.FireUpdate();
		}

		private void ProcessRelayState(string[] values)
		{
			if (values.Length == 0) return;

			eScb200RelayState state;
			switch (values[0])
			{
				case "UP":
					state = eScb200RelayState.Up;
					break;
				case "DN":
					state = eScb200RelayState.Down;
					break;
				case "ST":
					state = eScb200RelayState.Stopped;
					break;
				default:
					this.LogDebug("Unrecognized relay state '{state}'", values[0]);
					return;
			}

			SetMovingPoll(state == eScb200RelayState.Up || state == eScb200RelayState.Down);

			if (relayState == state) return;

			relayState = state;

			ShadeIsRaisingFeedback.FireUpdate();
			ShadeIsLoweringFeedback.FireUpdate();
			IsStoppedFeedback.FireUpdate();

			// The screen has settled, so refresh position once the motor stops
			if (state == eScb200RelayState.Stopped) PollPosition();
		}

		private void ProcessRotarySensorState(string[] values)
		{
			if (values.Length == 0) return;

			var enabled = values[0] == "ON";
			if (rotarySensorEnabled == enabled) return;

			rotarySensorEnabled = enabled;
			RotarySensorEnabledFeedback.FireUpdate();
		}

		private void ProcessCalibrationState(string[] values)
		{
			if (values.Length == 0) return;

			eScb200CalibrationState state;
			switch (values[0])
			{
				case "ON":
					state = eScb200CalibrationState.Calibrated;
					break;
				case "BC":
					state = eScb200CalibrationState.BusyCalibrating;
					break;
				case "OF":
					state = eScb200CalibrationState.NotCalibrated;
					break;
				default:
					this.LogDebug("Unrecognized calibration state '{state}'", values[0]);
					return;
			}

			if (calibrationState == state) return;

			calibrationState = state;

			IsCalibratedFeedback.FireUpdate();
			IsCalibratingFeedback.FireUpdate();
		}

		private void SetTargetPositionValue(int value)
		{
			if (targetPosition == value) return;

			targetPosition = value;

			TargetPositionFeedback.FireUpdate();
			PositionFeedback.FireUpdate();

			UpdatePositionState();
		}

		private void SetPositionInchesValue(double inches)
		{
			var hundredths = (int)Math.Round(inches * 100);
			if (positionInchesHundredths == hundredths) return;

			positionInchesHundredths = hundredths;
			PositionInchesFeedback.FireUpdate();
		}

		private void SetPositionMmValue(int value)
		{
			if (positionMm == value) return;

			positionMm = value;
			PositionMmFeedback.FireUpdate();
		}

		private void SetUpperLimitValue(int value)
		{
			if (upperLimit == value) return;

			upperLimit = value;

			UpperLimitFeedback.FireUpdate();
			PositionFeedback.FireUpdate();

			UpdatePositionState();
		}

		private void SetLowerLimitValue(int value)
		{
			if (lowerLimit == value) return;

			lowerLimit = value;

			LowerLimitFeedback.FireUpdate();
			PositionFeedback.FireUpdate();

			UpdatePositionState();
		}

		private void SetAcCurrentValue(double amps)
		{
			var tenths = (int)Math.Round(amps * 10);
			if (acCurrentTenths == tenths) return;

			acCurrentTenths = tenths;
			AcCurrentFeedback.FireUpdate();
		}

		private void SetViewingAreaWidthValue(int value)
		{
			if (viewingAreaWidthMm == value) return;

			viewingAreaWidthMm = value;
			ViewingAreaWidthFeedback.FireUpdate();
		}

		private void SetViewingAreaHeightValue(int value)
		{
			if (viewingAreaHeightMm == value) return;

			viewingAreaHeightMm = value;
			ViewingAreaHeightFeedback.FireUpdate();
		}

		private void SetFirmwareVersionValue(string value)
		{
			if (firmwareVersion == value) return;

			firmwareVersion = value;
			FirmwareVersionFeedback.FireUpdate();
		}

		/// <summary>
		/// Scales the raw encoder position to 0 (upper limit) through 65535 (lower limit)
		/// </summary>
		private int GetScaledPosition()
		{
			if (targetPosition < 0 || lowerLimit <= upperLimit) return 0;

			var span = lowerLimit - upperLimit;
			var offset = Math.Min(Math.Max(targetPosition - upperLimit, 0), span);

			return (int)Math.Round((double)offset / span * ushort.MaxValue);
		}

		private bool IsAtLowerLimit()
		{
			return targetPosition >= 0 && lowerLimit > upperLimit && targetPosition >= lowerLimit;
		}

		private void UpdatePositionState()
		{
			var atUpperLimit = targetPosition >= 0 && targetPosition <= upperLimit;

			IsInDownPosition.FireUpdate();
			ShadeIsClosedFeedback.FireUpdate();

			if (inUpPosition == atUpperLimit) return;

			inUpPosition = atUpperLimit;

			IsInUpPosition.FireUpdate();
			ShadeIsOpenFeedback.FireUpdate();

			var handler = PositionChanged;
			handler?.Invoke(this, EventArgs.Empty);
		}

		#endregion

		#region Overrides of EssentialsBridgeableDevice

		/// <summary>
		/// Links the plugin device to the EISC bridge
		/// </summary>
		/// <param name="trilist">bridge trilist</param>
		/// <param name="joinStart">bridge join offset</param>
		/// <param name="joinMapKey">custom join map key</param>
		/// <param name="bridge">bridge instance</param>
		public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
		{
			var joinMap = new DaLiteScb200BridgeJoinMap(joinStart);

			bridge?.AddJoinMap(Key, joinMap);

			var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);
			if (customJoins != null)
			{
				joinMap.SetCustomJoinData(customJoins);
			}

			this.LogDebug("Linking to Trilist {id}", trilist.ID.ToString("X"));
			this.LogInformation("Linking to Bridge Type {type}", GetType().Name);

			trilist.SetString(joinMap.DeviceName.JoinNumber, Name);
			trilist.SetUshort(joinMap.DeviceId.JoinNumber, (ushort)DeviceId);

			// comms
			trilist.SetBoolSigAction(joinMap.Connect.JoinNumber, sig => Connect = sig);
			ConnectFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Connect.JoinNumber]);
			IsOnline.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
			StatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.Status.JoinNumber]);

			// motion
			trilist.SetSigTrueAction(joinMap.Raise.JoinNumber, Raise);
			trilist.SetSigTrueAction(joinMap.Lower.JoinNumber, Lower);
			trilist.SetSigTrueAction(joinMap.Stop.JoinNumber, Stop);
			trilist.SetSigTrueAction(joinMap.Reset.JoinNumber, Reset);
			trilist.SetSigTrueAction(joinMap.PollAll.JoinNumber, PollAll);

			ShadeIsRaisingFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Raise.JoinNumber]);
			ShadeIsLoweringFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Lower.JoinNumber]);
			IsStoppedFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Stop.JoinNumber]);
			IsInUpPosition.LinkInputSig(trilist.BooleanInput[joinMap.IsInUpPosition.JoinNumber]);
			IsInDownPosition.LinkInputSig(trilist.BooleanInput[joinMap.IsInDownPosition.JoinNumber]);
			IsCalibratedFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsCalibrated.JoinNumber]);
			IsCalibratingFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsCalibrating.JoinNumber]);
			RotarySensorEnabledFeedback.LinkInputSig(trilist.BooleanInput[joinMap.RotarySensorEnabled.JoinNumber]);

			// aspect ratio presets
			LinkAspectRatioPresetsToApi(trilist, joinMap);

			// position
			trilist.SetUShortSigAction(joinMap.Position.JoinNumber, SetPosition);
			trilist.SetUShortSigAction(joinMap.TargetPosition.JoinNumber,
				value => SetTargetPosition(eScb200SetMode.Fixed, value));
			trilist.SetUShortSigAction(joinMap.PositionInches.JoinNumber,
				value => SetPositionInches(eScb200SetMode.Fixed, value / 100.0));
			trilist.SetUShortSigAction(joinMap.PositionMm.JoinNumber,
				value => SetPositionMm(eScb200SetMode.Fixed, value));

			PositionFeedback.LinkInputSig(trilist.UShortInput[joinMap.Position.JoinNumber]);
			TargetPositionFeedback.LinkInputSig(trilist.UShortInput[joinMap.TargetPosition.JoinNumber]);
			PositionInchesFeedback.LinkInputSig(trilist.UShortInput[joinMap.PositionInches.JoinNumber]);
			PositionMmFeedback.LinkInputSig(trilist.UShortInput[joinMap.PositionMm.JoinNumber]);
			UpperLimitFeedback.LinkInputSig(trilist.UShortInput[joinMap.UpperLimit.JoinNumber]);
			LowerLimitFeedback.LinkInputSig(trilist.UShortInput[joinMap.LowerLimit.JoinNumber]);
			AcCurrentFeedback.LinkInputSig(trilist.UShortInput[joinMap.AcCurrent.JoinNumber]);
			ViewingAreaWidthFeedback.LinkInputSig(trilist.UShortInput[joinMap.ViewingAreaWidth.JoinNumber]);
			ViewingAreaHeightFeedback.LinkInputSig(trilist.UShortInput[joinMap.ViewingAreaHeight.JoinNumber]);
			ErrorCodeFeedback.LinkInputSig(trilist.UShortInput[joinMap.ErrorCode.JoinNumber]);

			FirmwareVersionFeedback.LinkInputSig(trilist.StringInput[joinMap.FirmwareVersion.JoinNumber]);
			ErrorMessageFeedback.LinkInputSig(trilist.StringInput[joinMap.ErrorMessage.JoinNumber]);

			UpdateFeedbacks();

			trilist.OnlineStatusChange += (o, a) =>
			{
				if (!a.DeviceOnLine) return;

				trilist.SetString(joinMap.DeviceName.JoinNumber, Name);
				trilist.SetUshort(joinMap.DeviceId.JoinNumber, (ushort)DeviceId);
				UpdateFeedbacks();
			};
		}

		private void LinkAspectRatioPresetsToApi(BasicTriList trilist, DaLiteScb200BridgeJoinMap joinMap)
		{
			for (uint i = 0; i < joinMap.AspectRatioRecall.JoinSpan; i++)
			{
				var preset = i + 1;

				trilist.SetSigTrueAction(joinMap.AspectRatioRecall.JoinNumber + i, () => RecallAspectRatio(preset));
				trilist.SetSigTrueAction(joinMap.AspectRatioStore.JoinNumber + i, () => StoreAspectRatio(preset));
			}

			AspectRatioPresetChanged += (o, a) => UpdateAspectRatioFeedbacks(trilist, joinMap);
			UpdateAspectRatioFeedbacks(trilist, joinMap);
		}

		private void UpdateAspectRatioFeedbacks(BasicTriList trilist, DaLiteScb200BridgeJoinMap joinMap)
		{
			for (uint i = 0; i < joinMap.AspectRatioRecall.JoinSpan; i++)
			{
				trilist.SetBool(joinMap.AspectRatioRecall.JoinNumber + i, lastAspectRatioPreset == i + 1);
			}
		}

		private void UpdateFeedbacks()
		{
			ConnectFeedback.FireUpdate();
			IsOnline.FireUpdate();
			StatusFeedback.FireUpdate();

			ShadeIsRaisingFeedback.FireUpdate();
			ShadeIsLoweringFeedback.FireUpdate();
			IsStoppedFeedback.FireUpdate();
			IsInUpPosition.FireUpdate();
			IsInDownPosition.FireUpdate();
			ShadeIsOpenFeedback.FireUpdate();
			ShadeIsClosedFeedback.FireUpdate();
			IsCalibratedFeedback.FireUpdate();
			IsCalibratingFeedback.FireUpdate();
			RotarySensorEnabledFeedback.FireUpdate();

			PositionFeedback.FireUpdate();
			TargetPositionFeedback.FireUpdate();
			PositionInchesFeedback.FireUpdate();
			PositionMmFeedback.FireUpdate();
			UpperLimitFeedback.FireUpdate();
			LowerLimitFeedback.FireUpdate();
			AcCurrentFeedback.FireUpdate();
			ViewingAreaWidthFeedback.FireUpdate();
			ViewingAreaHeightFeedback.FireUpdate();
			ErrorCodeFeedback.FireUpdate();

			FirmwareVersionFeedback.FireUpdate();
			ErrorMessageFeedback.FireUpdate();
		}

		#endregion
	}
}
