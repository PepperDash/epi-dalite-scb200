using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Plugin.DaLite.Scb200
{
	/// <summary>
	/// Da-Lite SCB-200 plugin device factory
	/// </summary>
	public class DaLiteScb200DeviceFactory : EssentialsPluginDeviceFactory<DaLiteScb200Device>
	{
		/// <summary>
		/// Plugin device factory constructor
		/// </summary>
		public DaLiteScb200DeviceFactory()
		{
			MinimumEssentialsFrameworkVersion = "2.12.1";

			TypeNames = new List<string> { "dalitescb200", "dalitescreen", "scb200" };
		}

		/// <summary>
		/// Builds and returns an instance of <see cref="DaLiteScb200Device"/>
		/// </summary>
		/// <param name="dc">device configuration</param>
		/// <returns>plugin device or null</returns>
		/// <seealso cref="PepperDash.Core.eControlMethod"/>
		public override EssentialsDevice BuildDevice(DeviceConfig dc)
		{
			Debug.LogVerbose("[{key}] Factory Attempting to create new device from type: {type}", dc.Key, dc.Type);

			var propertiesConfig = dc.Properties.ToObject<DaLiteScb200Config>();
			if (propertiesConfig == null)
			{
				Debug.LogError("[{key}] Factory: failed to read properties config for {name}", dc.Key, dc.Name);
				return null;
			}

			// The SCB-200 speaks RS-232 directly, or TCP/IP on port 10001 through an optional NET-200 daughter board
			var comms = CommFactory.CreateCommForDevice(dc);
			if (comms == null)
			{
				Debug.LogError("[{key}] Factory Notice: No control object present for device {name}", dc.Key, dc.Name);
				return null;
			}

			return new DaLiteScb200Device(dc.Key, dc.Name, propertiesConfig, comms);
		}
	}
}
