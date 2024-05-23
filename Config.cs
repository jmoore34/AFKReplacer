using Exiled.API.Interfaces;
using System.ComponentModel;

namespace AFKReplacer
{
    public class Config : IConfig
    {
        public bool Debug { get; set; } = false;

        [Description("How many seconds a player can be AFK before they start getting warned about an imminent AFK kick")]
        public int SecondsBeforeAFKWarn { get; set; } = 40;

        [Description("How many seconds a player can be AFK before being kicked")]
        public int SecondsBeforeAFKKickOrDespawn { get; set; } = 50;

        [Description("Whether players with RA access are immune to being automatically AFK despawned/kicked")]
        public bool StaffAfkImmune { get; set; } = true;

        [Description("How many times a player should be AFK Despawned before being AFK Kicked")]
        public int DespawnsBeforeKick { get; set; } = 1;

        [Description("Is the plugin enabled?")]
        public bool IsEnabled { get; set; } = true;
    }
}