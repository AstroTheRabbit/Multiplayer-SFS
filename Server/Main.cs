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
			/*
			if (IsAdministrator() == false)
			{
				// Restart program and run as admin
				var exeName = Process.GetCurrentProcess().MainModule.FileName;
				ProcessStartInfo startInfo = new ProcessStartInfo(exeName);
				startInfo.Verb = "runas";
				Process.Start(startInfo);
				return;
			}

			bool IsAdministrator()
			{
				WindowsIdentity identity = WindowsIdentity.GetCurrent();
				WindowsPrincipal principal = new WindowsPrincipal(identity);
				return principal.IsInRole(WindowsBuiltInRole.Administrator);
			}*/
			
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