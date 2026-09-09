namespace PepperDash.Essentials.Plugin.DaLite.Scb200
{
	/// <summary>
	/// Relay/motion state reported by the SCB-200 <c>RE</c> value
	/// </summary>
	/// <remarks>
	/// Per the SCB-200 command reference: "RE: Relay status, where UP indicates position 1 is shorted,
	/// DN indicates position 2 is shorted, and ST indicates both positions are open."
	/// </remarks>
	public enum eScb200RelayState
	{
		/// <summary>
		/// State has not been reported by the device yet
		/// </summary>
		Unknown = 0,

		/// <summary>
		/// Screen is stopped (API value "ST")
		/// </summary>
		Stopped = 1,

		/// <summary>
		/// Screen is raising (API value "UP")
		/// </summary>
		Up = 2,

		/// <summary>
		/// Screen is lowering (API value "DN")
		/// </summary>
		Down = 3
	}

	/// <summary>
	/// Calibration state reported by the SCB-200 <c>CA</c> value
	/// </summary>
	public enum eScb200CalibrationState
	{
		/// <summary>
		/// State has not been reported by the device yet
		/// </summary>
		Unknown = 0,

		/// <summary>
		/// Calibrated (API value "ON")
		/// </summary>
		Calibrated = 1,

		/// <summary>
		/// Busy calibrating (API value "BC")
		/// </summary>
		BusyCalibrating = 2,

		/// <summary>
		/// Not calibrated (API value "OF")
		/// </summary>
		NotCalibrated = 3
	}

	/// <summary>
	/// Modifier used by the SCB-200 position "SET" commands
	/// </summary>
	/// <remarks>
	/// Per the SCB-200 command reference: "Position setting values can be set as FIXed, INCrement, or
	/// DECrement. All numeric values following shall be positive integers."
	/// </remarks>
	public enum eScb200SetMode
	{
		/// <summary>
		/// Absolute position ("FIX")
		/// </summary>
		Fixed = 0,

		/// <summary>
		/// Relative move further down ("INC")
		/// </summary>
		Increment = 1,

		/// <summary>
		/// Relative move further up ("DEC")
		/// </summary>
		Decrement = 2
	}
}
