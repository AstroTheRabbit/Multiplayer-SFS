using System;

namespace MultiplayerSFS.Server
{
    public class Program
	{
		public static void Main()
		{
			try
			{
				// TODO: Settings loading.
				Server.Initialize(new ServerSettings());
				Server.Run();
			}
			catch (Exception e)
			{
				Logger.Error(e);
			}
		}
	}
}