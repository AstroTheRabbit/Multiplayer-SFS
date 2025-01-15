namespace MultiplayerSFS.Server
{
    public class ServerSettings
	{
		/// <summary>
		/// The file path to the multiplayer world's save folder.
		/// </summary>
		public string worldSavePath = "/Users/home/Library/Application Support/Steam/steamapps/common/Spaceflight Simulator/SpaceflightSimulatorGame.app/Saving/Worlds/Multiplayer Testing"; // ! TEST VALUE
		/// <summary>
		/// Port used by the server. Generally should not be changed, as this is also the default port for the client's join menu.
		/// </summary>
		public int port = 9806;
		/// <summary>
		/// Password required by players to access the multiplayer server.
		/// WARNING: Leaving blank will allow any player who knows the server's IP address to join!
		/// </summary>
		public string password = "";
		/// <summary>
		/// The maximum number of connected players allowed at any one time.
		/// </summary>
		public int maxConnections = 16;

		/// <summary>
		/// Prevents players from joining if their username is already in use on the server.
		/// </summary>
		public bool blockDuplicatePlayerNames = false; // ! TEST VALUE

		/// <summary>
		/// A time (in seconds) after which the server will send a full `CreateRocket` packets to resync all rockets.
		/// A shorter resync period will ensure that rockets are better synchronised between connected players,
		/// but will also result in more lag (since the rockets have to be completely recreated client-side).
		/// </summary>
		public double completeResyncPeriod = 0; // ! TEST VALUE

		/// <summary>
		/// A time (in seconds) after which the server will send `UpdatePlayerAuthority` packets to each player.
		/// These 'authorities' are used to determine which player must provide update information (location, throttle, etc) for certain rockets.
		/// It's generally recommended to keep this value low, but too low can lower players' FPS.
		/// </summary>
		public double updateAuthoritiesPeriod = 10;

		/// <summary>
		/// A time (in milliseconds) after which connected clients will send `UpdateRocket` packets to the server.
		/// It's generally recommended to keep this value low (a value that is too high will make rockets 'jitter'),
		/// but too low can lower players' FPS.
		/// </summary>
		public double updateRocketsPeriod = 20;

		/// <summary>
		/// (Squared) distance used to determine if a player should be given 'update authority' of a nearby rocket.
		/// Should always be set above the game's current (un)load distance (1.2 * 5000 iirc).
		/// </summary>
		public double sqrLoadRange = 7500 * 7500;
	}
}