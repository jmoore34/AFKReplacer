using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.CustomItems.API.Features;
using Exiled.CustomRoles.API;
using Exiled.CustomRoles.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using MEC;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AFKReplacer
{
    public class Plugin : Plugin<Config>
    {
        public override string Name => "AFK Replacer";
        public override string Author => "Jon M";
        public override Version Version => new Version(1, 0, 0);

        // Singleton pattern allows easy access to the central state from other classes
        // (e.g. commands)
        public static Plugin Singleton { get; private set; }

        // A stoppable handle on the coroutine that performs AFK checks every second
        private CoroutineHandle coroutine { get; set; }

        // Detect AFK by keeping track of how long each player has kept their rotation
        private Dictionary<Player, PlayerData> playerDataMap = new Dictionary<Player, PlayerData>();

        public override void OnEnabled()
        {
            // Set up the Singleton so we can easily get the instance with all the state
            // from another class.
            Singleton = this;

            // Register event handlers
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStart;
            Exiled.Events.Handlers.Server.RoundEnded += OnRoundEnded;
            Exiled.Events.Handlers.Player.ChangingRole += OnChangingRole;
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            // Deregister event handlers
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStart;
            Exiled.Events.Handlers.Server.RoundEnded -= OnRoundEnded;
            Exiled.Events.Handlers.Player.ChangingRole -= OnChangingRole;

            // This will prevent commands and other classes from being able to access
            // any state while the plugin is disabled
            Singleton = null;

            base.OnDisabled();
        }

        public void OnRoundStart()
        {
            playerDataMap.Clear();
            coroutine = Timing.RunCoroutine(timer());
        }

        public void OnRoundEnded(RoundEndedEventArgs ev)
        {
            Timing.KillCoroutines(coroutine);
        }

        private IEnumerator<float> timer()
        {
            while (true)
            {
                try
                {
                    foreach (Player player in Player.List)
                    {
                        if (player != null
                            && player.IsAlive
                            && player.Rotation != null
                            && player.Position != null
                            // skip staff, unless staff are not immune
                            && (!player.RemoteAdminAccess || !Config.StaffAfkImmune)
                            // skip tutorials, unless custom class e.g. serpents hand
                            && (player.Role.Type != RoleTypeId.Tutorial || !player.GetCustomRoles().IsEmpty()))
                        {
                            PlayerData playerData;
                            var hasData = playerDataMap.TryGetValue(player, out playerData);
                            if (!hasData)
                            {
                                playerDataMap[player] = new PlayerData();
                                playerData = playerDataMap[player];
                            }

                            //Log.Info($"{player.Nickname} old rot: {playerData.LastPlayerRotation} new rot: {player.Rotation} old pos: {playerData.LastPlayerPosition}  new pos: {player.Position} seconds: {playerData.SecondsSinceRotationChange} despawns: {playerData.DespawnCount}");
                            var oldRotation = playerData.LastPlayerRotation;
                            var newRotation = player.Rotation;

                            var oldPosition = playerData.LastPlayerPosition;
                            var newPosition = player.Position;

                            if (player.Role.Is(out Scp079Role scp))
                            {
                                newPosition = scp.CameraPosition;
                                newRotation = scp.Camera.Rotation;
                            }

                            // if not afk, i.e. is moving
                            if (oldRotation == null || oldRotation != newRotation || oldPosition == null || oldPosition != newPosition)
                            {
                                playerData.SecondsSinceRotationChange = 0;
                            }
                            else // if not moving
                            {
                                var secondsUntilKickOrDespawn = Config.SecondsBeforeAFKKickOrDespawn - playerData.SecondsSinceRotationChange;
                                if (playerData.SecondsSinceRotationChange >= Config.SecondsBeforeAFKWarn)
                                {
                                    var s = secondsUntilKickOrDespawn == 1 ? "" : "s";

                                    var action = playerData.DespawnCount >= Config.DespawnsBeforeKick ? "kicked" : "despawned";

                                    player.Broadcast(1,
                                        $"<color=red>Warning:\n</color><color=yellow>You will be automatically AFK {action} in <color=red>{secondsUntilKickOrDespawn} second{s}</color>.\n<color=green>Please move to reset the AFK timer.</color>",
                                        Broadcast.BroadcastFlags.Normal,
                                        true
                                        );
                                }
                                if (secondsUntilKickOrDespawn <= 0)
                                {
                                    if (playerData.DespawnCount >= Config.DespawnsBeforeKick)
                                    {
                                        ReplacePlayer(player);
                                        Timing.CallDelayed(0.5f, () =>
                                        {
                                            if (player != null)
                                            {
                                                player.Kick("You were automatically AFK kicked by a plugin. Press the Re-Join button to re-connect.");
                                            }
                                        });
                                    }
                                    else
                                    {
                                        player.DespawnAndReplace();
                                        playerData.DespawnCount++;
                                    }
                                }

                                playerData.SecondsSinceRotationChange++;
                            }
                            playerData.LastPlayerRotation = newRotation;
                            playerData.LastPlayerPosition = newPosition;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
                yield return Timing.WaitForSeconds(1f);
            }
        }

        private void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (ev.Player != null && playerDataMap != null && playerDataMap.TryGetValue(ev.Player, out PlayerData data))
                data.SecondsSinceRotationChange = 0;
        }


        public void ReplacePlayer(Player playerToReplace)
        {
            if (playerToReplace == null || playerToReplace.Position == null || !playerToReplace.IsAlive)
                return;

            var position = playerToReplace.Position;
            if (playerToReplace.IsInElevator())
                return;

            // regular spectator replacement
            var spectators = Player.List.Where(p => p.Role.Type == RoleTypeId.Spectator);
            if (spectators == null || spectators.IsEmpty())
                return;

            var spectator = spectators.RandomElement();

            spectator.Role.Set(playerToReplace.Role.Type, SpawnReason.Respawn, RoleSpawnFlags.UseSpawnpoint);
            var roleName = $"<color={playerToReplace.Role.Color.ToHex()}>{playerToReplace.Role.Type.GetFullName()}</color>";
            foreach (CustomRole role in playerToReplace.GetCustomRoles())
            {
                role.AddRole(spectator);
                roleName = $"<color={playerToReplace.Role.Color.ToHex()}>{role.Name}</color>";
            }

            var playerHealth = playerToReplace.Health;
            var playerMaxHealth = playerToReplace.MaxHealth;
            var playerArtificialHealth = playerToReplace.ArtificialHealth;
            var playerMaxArtificialHealth = playerToReplace.MaxArtificialHealth;
            var playerHumeShield = playerToReplace.HumeShield;
            var playerCuffer = playerToReplace.Cuffer;
            if (playerToReplace.Role.Is(out Scp079Role scp))
            {
                var playerEnergy = scp.Energy;
                var playerXP = scp.Experience;
                var playerLevel = scp.Level;
                Timing.CallDelayed(2f, () =>
                {
                    if (spectator.Role.Is(out Scp079Role newScp))
                    {
                        newScp.Energy = playerEnergy;
                        newScp.Experience = playerXP;
                        newScp.Level = playerLevel;
                    }
                });
            }
            foreach (var item in playerToReplace.Items.ToArray())
            {
                CustomItem.TryGet(item, out CustomItem? custom);
                spectator.GiveItemDelayed(item.Clone(), custom);
                item.Destroy();
            }
            List<ItemType> ammo = playerToReplace.Ammo.Keys.ToList();
            foreach (ItemType a in ammo)
            {
                Timing.CallDelayed(3f, () =>
                {
                    spectator.AddItem(a);
                });
            }
            playerToReplace.ClearInventory();


            spectator.Broadcast(10, $"<color=yellow>You have replaced an AFK player and become <color={playerToReplace.Role.Color.ToHex()}>{roleName}</color>.</color>");
            Timing.CallDelayed(2f, () =>
            {
                spectator.ClearInventory(); // called before items are added
                spectator.Teleport(position);
                spectator.Health = playerHealth;
                spectator.MaxHealth = playerMaxHealth;
                spectator.ArtificialHealth = playerArtificialHealth;
                spectator.MaxArtificialHealth = playerMaxArtificialHealth;
                spectator.HumeShield = playerHumeShield;
                spectator.Cuffer = playerCuffer;
            });

        }
    }
}