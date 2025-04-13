using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace MultiplayerSFS.Server
{
    public class Program
    {
	    private const string CONFIG_FILENAME = "Multiplayer.cfg";
		public static void Main()
		{
			try
			{
				if (!File.Exists(CONFIG_FILENAME))
				{
					File.WriteAllText(CONFIG_FILENAME, new ServerSettings().Serialize());
				}
				
				Server.Initialize(ServerSettings.Deserialize(File.ReadAllText(CONFIG_FILENAME)));
				Server.Run();
			}
			catch (Exception e)
			{
				Logger.Error(e);
			}
		}
	}
}
