using System;
using System.IO;

namespace MultiplayerSFS.Server
{
    public class Program
    {
	    private const string CONFIG_FILENAME = "Multiplayer.cfg";
		/// <summary>
		/// Stops the server's config file from being saved or loaded.
		/// </summary>
		private static readonly bool DEV_MODE = true;
		public static void Main()
		{
			try
			{
				ServerSettings settings;
				if (DEV_MODE)
				{
					Logger.Info("Dev mode enabled, running with default settings...", true);
					settings = new ServerSettings();
					File.Delete(CONFIG_FILENAME);
				}
				else if (!File.Exists(CONFIG_FILENAME))
				{
					Logger.Info($"'{CONFIG_FILENAME}' not found, running with default settings...", true);
					settings = new ServerSettings();
					File.WriteAllText(CONFIG_FILENAME, settings.Serialize());
				}
				else
				{
					Logger.Info($"Loading server settings from '{CONFIG_FILENAME}'...", true);
					settings = ServerSettings.Deserialize(File.ReadAllText(CONFIG_FILENAME));
				}
				Server.Initialize(settings);
				Server.Run();
			}
			catch (Exception e)
			{
				Logger.Error(e);
			}
		}
	}
}
