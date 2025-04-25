using System;
using System.Text;
using System.Reflection;
using System.Globalization;

namespace MultiplayerSFS.Server
{
	public class ServerConfigVariable : Attribute
	{
		public readonly string[] Comment;

		public ServerConfigVariable(params string[] comment)
		{
			Comment = comment;
		}
	}

	public class ServerSettings
	{

		[ServerConfigVariable(
			"The file path to the multiplayer world's save folder."
		)]
		public string worldSavePath = "/Users/home/Library/Application Support/Steam/steamapps/common/Spaceflight Simulator/SpaceflightSimulatorGame.app/Saving/Worlds/Multiplayer Testing"; // ! TEST VALUE

		[ServerConfigVariable(
			"Port used by the server. Generally should not be changed, as this is also the default port for the client's join menu."
		)]
		public int port = 9806;
		
		[ServerConfigVariable(
			"Password required by players to access the multiplayer server.", 
			"WARNING: Leaving blank can & will allow any player who knows the server's IP address to join!"
		)]
		public string serverPassword = "";

		[ServerConfigVariable(
			"Password used by a player to gain admin privileges, which allows the use of various powerful commands through the in-game multiplayer chat.",
			"Leaving this blank will prevent the use of admin privileges by any connected player.",
			"WARNING: These commands include the ability to destroy any & all rockets, kick any player, and more, so be careful with sharing this password!"
		)]
		public string adminPassword = "ADMIN123"; // ! TEST VALUE

		[ServerConfigVariable(
			"The maximum number of connected players allowed at any one time."
		)]
		public int maxConnections = 16;
		
		[ServerConfigVariable(
			"Prevents players from joining if their username is already in use on the server."
		)]
		public bool blockDuplicatePlayerNames = false; // ! TEST VALUE
		
		[ServerConfigVariable(
			"A time (in milliseconds) after which connected clients will send new `UpdateRocket` packets to the server.",
			"It's generally recommended to keep this value as it is - higher will increase jitter, lower will increase CPU/network load on both client and server side.",
			"Updates per second = 1000 / updateRocketsPeriod"
		)]
		public double updateRocketsPeriod = 20;

		[ServerConfigVariable(
			"Distance used to determine if a player should be given 'update authority' over nearby rockets.",
			"Should always be set above the game's current (un)load distance (1.2 * 5000 iirc)."
		)]
		public double loadRange = 7500;
		
		[ServerConfigVariable(
			"Cooldown time (in seconds) during which a player cannot send another message in the multiplayer chat.",
			"Used to reduce spam and similar issues. Set to 0 if you want to disable the cooldown."
		)]
		public double chatMessageCooldown = 3;

		public string Serialize()
		{
			StringBuilder result = new StringBuilder();
			
			foreach (var field in GetType().GetFields())
			{
				var attr = field.GetCustomAttribute<ServerConfigVariable>();

				if (attr == null) continue;

				if (attr.Comment.Length != 0)
				{
					foreach (string line in attr.Comment)
					{
						result.AppendLine("# " + line);
					}
				}
				
				var val = field.GetValue(this);
				var strVal = val is IFormattable formattable
					? formattable.ToString(null, CultureInfo.InvariantCulture)
					: val?.ToString();
				
				result.Append(field.Name + "=" + strVal + "\n\n");
			}
			
			return result.ToString().TrimEnd('\n');
		}

		public static ServerSettings Deserialize(string input)
		{
			try
			{
				var result = new ServerSettings();

				string[] lines = input.Split('\n');
				
				foreach (var line in lines)
				{
					string trimLine = line.TrimStart(' ').Replace("\r", "");

					if (trimLine.Length == 0) continue;
					if (trimLine.StartsWith("#")) continue;

					int eqIndex = trimLine.IndexOf('=');

					if (eqIndex == -1) throw new Exception("= character not found.");

					string key = trimLine.Substring(0, eqIndex);
					string value = trimLine.Length > eqIndex + 1 ? trimLine.Substring(eqIndex + 1) : "";

					try
					{
                        FieldInfo field = result.GetType().GetField(key);
						field.SetValue(result, Convert.ChangeType(value, field.FieldType, CultureInfo.InvariantCulture));
						Logger.Info(key + ": " + field.GetValue(result));
					}
					catch (Exception ex)
					{
						throw new Exception($"Variable deserialization error ({key})", ex);
					}
				}
				
				return result;
			}
			catch (Exception ex)
			{
				throw new Exception("Config deserialization failed", ex);
			}
		}
	}
}
