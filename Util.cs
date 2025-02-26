using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.CustomItems.API.Features;
using MEC;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AFKReplacer
{
    public static class Util
    {
        /// <summary>
        /// Given an IEnumerable (like a list), get a random element
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable">a list or other enumerable object</param>
        /// <returns>a random item from that enumerable</returns>
        public static T RandomElement<T>(this IEnumerable<T> enumerable)
        {
            int index = UnityEngine.Random.Range(0, enumerable.Count());
            return enumerable.ElementAt(index);
        }

        public static void DespawnAndReplace(this Player player)
        {
            player.Broadcast(10, "<color=yellow>You were replaced with a spectator.</color>");
            Plugin.Singleton.ReplacePlayer(player);
            Timing.CallDelayed(0.5f, () =>
            {
                player.Role.Set(RoleTypeId.Spectator);
            });
        }

        public static void GiveItemDelayed(this Player player, Item item, CustomItem? customItemType)
        {
            Timing.CallDelayed(3f, () =>
            {
                if (customItemType is not null)
                {
                    customItemType.Give(player);
                }
                else
                {
                    player.AddItem(item);
                }
            });
        }

        public static bool IsInElevator(this Player player)
        {
            foreach (Lift lift in Lift.List)
            {
                var elevatorRadius = 2.1f;

                var elevatorPosition = lift.Position;
                var playerPosition = player.Position;

                // normalize y position to ignore it (so we can check 2d radius, not spherical radius)
                playerPosition.y = elevatorPosition.y;

                // but we still want to make sure y isn't too far off
                if ((playerPosition - elevatorPosition).sqrMagnitude < elevatorRadius * elevatorRadius
                    && Math.Abs(player.Position.y - lift.Position.y) < 10)
                    return true;
            }
            return false;
        }


    }
}