using CommandSystem;
using Exiled.API.Features;
using System;
using System.Linq;

namespace AFKReplacer
{
    public class Commands
    {
        [CommandHandler(typeof(ClientCommandHandler))]
        [CommandHandler(typeof(RemoteAdminCommandHandler))]
        public class AfkReplace : ICommand
        {
            public string Command => "afk";

            public string[] Aliases => new string[] { };

            public string Description => "AFK-replace yourself or another player";

            public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
            {
                if (!sender.CheckPermission(PlayerPermissions.ForceclassToSpectator))
                {
                    response = "Insufficient permissions";
                    return false;
                }
                Player? target = null;
                if (arguments.IsEmpty())
                {
                    Player.TryGet(sender, out target);
                }
                else
                {
                    var arg = string.Join(" ", arguments);
                    target = Player.List.FirstOrDefault(p =>
                        arg == p.Id.ToString()
                        || arg.Equals(p.Nickname, StringComparison.OrdinalIgnoreCase)
                    );
                }
                if (target == null)
                {
                    response = "Usage: afk <player id or name>";
                    return false;
                }
                if (!target.IsAlive)
                {
                    response = "Target must be alive";
                    return false;
                }
                Util.DespawnAndReplace(target);
                response = $"Replaced player {target.Nickname}";
                return true;
            }
        }
    }
}
