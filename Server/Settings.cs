namespace MultiplayerSFS.Server
{
    public class ServerSettings
	{
		/// <summary>
		/// The file path to the multiplayer world's save folder.
		/// </summary>
		public string worldSavePath = "/Users/home/Library/Application Support/Steam/steamapps/common/Spaceflight Simulator/SpaceflightSimulatorGame.app/Saving/Worlds/Multiplayer Testing";
		/// <summary>
		/// Port used by the multiplayer server.
		/// </summary>
		public int port = 9806;
		/// <summary>
		/// Password required by players to access the multiplayer server.
		/// WARNING: Leaving blank will allow any player who knows the server's IP address & port to join!
		/// </summary>
		public string password = "";
		/// <summary>
		/// The maximum number of connected players allowed at any one time.
		/// </summary>
		public int maxConnections = 16;

		/// <summary>
		/// Prevents players from joining if their username is already in use on the server.
		/// </summary>
		public bool blockDuplicatePlayerNames = true;
	}
}