using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lidgren.Network;
using MultiplayerSFS.Common;

namespace MultiplayerSFS.Server
{
    public static class CommandExtensions
    {
        public static string FormatCommand(this string name, string args = null)
        {
            string formatted = $"\"{CommandManager.CommandPrefix}{name}";
            if (args != null)
            {
                formatted += $" {args}";
            }
            formatted += "\"";
            return formatted;
        }

        public static string CleanDescription(this string desc)
        {
            // ? https://stackoverflow.com/a/34935471
            // * Removes whitespace up to and including a vertical bar (e.g. "    |") at the start of each line,
            // * as well as all leading and trailing whitespace, which is useful for multiline descriptions.
            return Regex.Replace(desc, @"[ \t]+\|", string.Empty).Trim();
        }

        public static bool TryParseBool(string arg, out bool result)
        {
            if (arg == "true")
            {
                result = true;
                return true;
            }
            if (arg == "false")
            {
                result = false;
                return true;
            }
            else
            {
                result = false;
                return false;
            }
        }
    }

    public static class CommandManager
    {
        public const string CommandPrefix = "/";
        internal static readonly Dictionary<string, Command> commands = new Dictionary<string, Command>()
        {
            { "help", new HelpCommand() },
            { "list", new ListCommand() },
            { "admin", new AdminCommand() },
            { "destroy", new DestroyCommand() },
            { "stats", new StatsCommand() },
        };

        public static bool TryParse(string input, out string name, out string[] args)
        {
            if (input.Length > 1 && input.StartsWith(CommandPrefix))
            {
                name = "";
                int i = 1;
                while (i < input.Length)
                {
                    char c = input[i];
                    if (c == ' ')
                        break;
                    else
                        name += c;
                    i++;
                }
                args = input.Substring(i).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                return !string.IsNullOrWhiteSpace(name);
            }
            else
            {
                name = null;
                args = null;
                return false;
            }
        }

        public static string TryRun(string name, string[] args, NetConnection sender)
        {
            string formatted = name.FormatCommand();
            if (commands.TryGetValue(name, out Command command))
            {
                try
                {
                    return command.Run(args, sender);
                }
                catch (Exception e)
                {
                    Logger.Error(new Exception($"{formatted} command raised an exception!", e));
                    return $"{formatted} resulted in an error:\n{e.Message}";
                }
            }
            return $"Could not find command {formatted}.";
        }

        /// <summary>
        /// Returns false if a command with the provided name was already registered.
        /// </summary>
        public static bool RegisterCommand(string name, Command command)
        {
            if (!commands.ContainsKey(name))
            {
                commands.Add(name, command);
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public abstract class Command
    {
        /// <summary>
        /// Description of the command (arguments, functionality, etc) for use with the "/help" command.
        /// </summary>
        public abstract string Description { get; }
        /// <summary>
        /// Run this command with the provided arguments. Returns a message which is sent back to the player who executed the command.
        /// </summary>
        public abstract string Run(string[] args, NetConnection sender);

        /// <summary>
        /// Returns true if the player associated with the provided `NetConnection` has admin privileges.
        /// </summary>
        public bool CheckAdmin(NetConnection sender)
        {
            if (Server.FindPlayer(sender) is ConnectedPlayer player)
            {
                return player.isAdmin;
            }
            else
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Sends the description of a command to the player, or a general help message if no command is provided.
    /// </summary>
    public class HelpCommand : Command
    {
        public override string Description => "Provides a description of a given command, or some general help info if no command is given.";

        public override string Run(string[] args, NetConnection sender)
        {
            if (args.Length == 0)
            {
                string formatted_list = "list".FormatCommand();
                string formatted_help = "help".FormatCommand("{command}");
                return $@"
                |Commands can be used to manage the server, its players, and the multiplayer world.
                |Use {formatted_list} to get a list of available commands, or {formatted_help} to get a description of a specific command.
                ".CleanDescription();
            }
            else if (args.Length == 1)
            {
                if (CommandManager.commands.TryGetValue(args[0], out Command command))
                {
                    return command.Description;
                }
                else
                {
                    string formatted = args[0].FormatCommand();
                    string formatted_list = "list".FormatCommand();
                    return $"The command {formatted} does not exist! Use {formatted_list} to get a list of available commands.";
                }
            }
            else
            {
                string formatted_list = "list".FormatCommand();
                string formatted_help = "help".FormatCommand("{command}");
                return $@"
                |Too many arguments provided.
                |Use {formatted_list} to get a list of available commands, or {formatted_help} to get a description of a specific command.
                ".CleanDescription();
            }
        }
    }

    public class ListCommand : Command
    {
        public override string Description => "Provides a list of available multiplayer commands.";

        public override string Run(string[] args, NetConnection sender)
        {
            // TODO: I might make this also operate as a search feature to find commands.
            if (args.Length > 0)
            {
                return "Too many arguments provided.";
            }
            else
            {
                return "List of available multiplayer commands:\n  " + string.Join("\n  ", CommandManager.commands.Keys.Select(n => n.FormatCommand()));
            }
        }
    }

    public class AdminCommand : Command
    {
        public override string Description => $@"
        |{"admin".FormatCommand("{password}")}: Grants admin privileges if the correct admin password is provided.
        |{"admin".FormatCommand()}: Disables admin privileges if they were already enabled.
        ".CleanDescription();

        public override string Run(string[] args, NetConnection sender)
        {
            if (Server.FindPlayer(sender) is ConnectedPlayer player)
            {
                if (args.Length > 0)
                {
                    if (player.isAdmin)
                    {
                        return "You already have admin privileges.";
                    }
                    else if (string.IsNullOrWhiteSpace(Server.settings.adminPassword))
                    {
                        return "Admin privileges are disabled on this server.";
                    }
                    else if (string.Join(" ", args) == Server.settings.adminPassword)
                    {
                        player.isAdmin = true;
                        return "Admin privileges granted!";
                    }
                    else
                    {
                        return "Incorrect admin password.";
                    }
                }
                else if (player.isAdmin)
                {
                    player.isAdmin = false;
                    return "Admin privileges removed.";
                }
                else
                {
                    return "You must provide the admin password to gain admin privileges.";
                }
            }
            return "Could not find player!";
        }
    }

    public class DestroyCommand : Command
    {
        // TODO: Maybe have some more options for specifying which rockets to destroy (e.g. is it on the surface?), but that'll need info about planets which I currently don't have server-side.
        public override string Description => $@"
        |Destroys all rockets that meet the criteria provided in the arguments. This command requires admin privileges.
        |{"destroy".FormatCommand("-a")}: Destroys <b>all</b> rockets in the world.
        |{"destroy".FormatCommand("-p {planet}")}: Destroys all rockets at a provided planet.
        ".CleanDescription();

        public override string Run(string[] args, NetConnection sender)
        {
            if (!CheckAdmin(sender))
            {
                return "You do not have the required admin privileges to use this command.";
            }

            if (args.Length == 0)
            {
                return "Not enough arguments provided.";
            }

            if (args[0] == "-a")
            {
                // * Destroy all rockets.
                Logger.Info($"Recieved {"destroy".FormatCommand("-a")} command, destroying all rockets...");
                int count = DestroyRockets(Server.world.rockets.Keys.ToList());
                return $"Destroyed {count} rocket(s).";
            }

            if (args[0] == "-p")
            {
                // * Destroy all rockets at specified planet.
                if (args.Length < 2)
                {
                    return "Not enough arguments provided.";
                }
                string planetName = string.Join(" ", args.Skip(1));
                Logger.Info($"Recieved {"destroy".FormatCommand($"-a {planetName}")} command, destroying all rockets at \"{planetName}\"...");
                List<int> ids = Server.world.rockets
                    .Where(kvp => kvp.Value.location.address == planetName)
                    .Select(kvp => kvp.Key)
                    .ToList();
                int count = DestroyRockets(ids);
                return $"Destroyed {count} rocket(s) at \"{planetName}\".";
            }

            return "Provided arguments were invalid.";
        }

        int DestroyRockets(IEnumerable<int> ids)
        {
            int count = 0;
            foreach (int id in ids)
            {
                if (Server.world.rockets.Remove(id))
                {
                    Server.SendPacketToAll
                    (
                        new Packet_DestroyRocket()
                        {
                            RocketId = id,
                        }
                    );
                    count++;
                }
            }
            return count;
        }
    }

    public class StatsCommand : Command
    {
        public override string Description => "Prints various stats about the multiplayer server.";

        public override string Run(string[] args, NetConnection sender)
        {
            if (args.Length > 0)
            {
                return "Too many arguments provided.";
            }
            else if (Server.FindPlayer(sender) is ConnectedPlayer player)
            {
                return $@"
                |• Number of players: {Server.connectedPlayers.Count}
                |• Number of rockets: {Server.world.rockets.Count}
                |• Number of parts: {Server.world.rockets.Sum(r => r.Value.parts.Count)}
                |• Connection latency: {1000 * player.avgTripTime}ms
                |• World time: {Server.world.WorldTime}
                ".CleanDescription();
            }
            else
            {
                return "Could not find player!";
            }
        }
    }
}