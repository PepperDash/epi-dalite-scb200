using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Plugin.DaLite.Scb200
{
	/// <summary>
	/// Da-Lite SCB-200 plugin device Bridge Join Map
	/// </summary>
	/// <see cref="PepperDash.Essentials.Core.Bridges"/>
	public class DaLiteScb200BridgeJoinMap : JoinMapBaseAdvanced
	{
		/// <summary>
		/// Number of aspect ratio presets exposed on the bridge (A1-A9, A0)
		/// </summary>
		public const uint AspectRatioPresetCount = 10;

		#region Digital

		[JoinName("IsOnline")]
		public JoinDataComplete IsOnline = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 1,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Is Online",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("Connect")]
		public JoinDataComplete Connect = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 2,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Connect (Held)/Disconnect (Release) & corresponding feedback",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("Raise")]
		public JoinDataComplete Raise = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 3,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Raise screen (# ID SE RE UP) & is-raising feedback",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("Lower")]
		public JoinDataComplete Lower = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 4,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Lower screen (# ID SE RE DN) & is-lowering feedback",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("Stop")]
		public JoinDataComplete Stop = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 5,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Stop screen (# ID SE RE ST) & is-stopped feedback",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("IsInUpPosition")]
		public JoinDataComplete IsInUpPosition = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 6,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Screen is at the upper limit (UL)",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("IsInDownPosition")]
		public JoinDataComplete IsInDownPosition = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 7,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Screen is at the lower limit (LL)",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("IsCalibrated")]
		public JoinDataComplete IsCalibrated = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 8,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Screen is calibrated (# ID GE CA returns ON)",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("IsCalibrating")]
		public JoinDataComplete IsCalibrating = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 9,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Screen is busy calibrating (# ID GE CA returns BC)",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("RotarySensorEnabled")]
		public JoinDataComplete RotarySensorEnabled = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 10,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Rotary sensor support is on (# ID GE SE returns ON)",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("Reset")]
		public JoinDataComplete Reset = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 11,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Reset the SCB-200 firmware (# ID SE RS)",
				JoinCapabilities = eJoinCapabilities.FromSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("PollAll")]
		public JoinDataComplete PollAll = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 12,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Query every supported value from the SCB-200",
				JoinCapabilities = eJoinCapabilities.FromSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("AspectRatioRecall")]
		public JoinDataComplete AspectRatioRecall = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 21,
				JoinSpan = AspectRatioPresetCount
			},
			new JoinMetadata
			{
				Description = "Recall aspect ratio preset 1-10 (A1-A9, A0) & corresponding feedback",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("AspectRatioStore")]
		public JoinDataComplete AspectRatioStore = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 31,
				JoinSpan = AspectRatioPresetCount
			},
			new JoinMetadata
			{
				Description = "Store the current position to custom aspect ratio preset 6-10 (A6-A9, A0)",
				JoinCapabilities = eJoinCapabilities.FromSIMPL,
				JoinType = eJoinType.Digital
			});

		#endregion


		#region Analog

		[JoinName("Status")]
		public JoinDataComplete Status = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 1,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Socket Status",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Analog
			});

		[JoinName("Position")]
		public JoinDataComplete Position = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 2,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Screen position scaled 0 (upper limit) to 65535 (lower limit) & corresponding feedback",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
				JoinType = eJoinType.Analog
			});

		[JoinName("PositionInches")]
		public JoinDataComplete PositionInches = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 3,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Screen position in hundredths of an inch (IN) & corresponding feedback",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
				JoinType = eJoinType.Analog
			});

		[JoinName("PositionMm")]
		public JoinDataComplete PositionMm = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 4,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Screen position in millimeters (MM) & corresponding feedback",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
				JoinType = eJoinType.Analog
			});

		[JoinName("TargetPosition")]
		public JoinDataComplete TargetPosition = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 5,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Raw encoder target position (TA), 0 to LL & corresponding feedback",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
				JoinType = eJoinType.Analog
			});

		[JoinName("UpperLimit")]
		public JoinDataComplete UpperLimit = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 6,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Upper limit encoder counter value (UL)",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Analog
			});

		[JoinName("LowerLimit")]
		public JoinDataComplete LowerLimit = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 7,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Lower limit encoder counter value (LL)",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Analog
			});

		[JoinName("AcCurrent")]
		public JoinDataComplete AcCurrent = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 8,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "AC current drawn through the relay, in tenths of an amp (AC)",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Analog
			});

		[JoinName("ViewingAreaWidth")]
		public JoinDataComplete ViewingAreaWidth = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 9,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Viewing area width in millimeters (SW)",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Analog
			});

		[JoinName("ViewingAreaHeight")]
		public JoinDataComplete ViewingAreaHeight = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 10,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Viewing area height in millimeters (SH)",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Analog
			});

		[JoinName("ErrorCode")]
		public JoinDataComplete ErrorCode = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 11,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Most recent SCB-200 error code, 0 when no error has been reported",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Analog
			});

		[JoinName("DeviceId")]
		public JoinDataComplete DeviceId = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 12,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Configured RS-485 device ID (0-7)",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Analog
			});

		#endregion


		#region Serial

		[JoinName("DeviceName")]
		public JoinDataComplete DeviceName = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 1,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Device Name",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Serial
			});

		[JoinName("FirmwareVersion")]
		public JoinDataComplete FirmwareVersion = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 2,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "SCB-200 firmware version (SV)",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Serial
			});

		[JoinName("ErrorMessage")]
		public JoinDataComplete ErrorMessage = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 3,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Most recent SCB-200 error message, empty when no error has been reported",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Serial
			});

		#endregion

		/// <summary>
		/// Plugin device BridgeJoinMap constructor
		/// </summary>
		/// <param name="joinStart">This will be the join it starts on the EISC bridge</param>
		public DaLiteScb200BridgeJoinMap(uint joinStart)
			: base(joinStart, typeof(DaLiteScb200BridgeJoinMap))
		{
		}
	}
}
