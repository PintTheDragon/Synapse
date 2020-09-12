﻿using Harmony;

namespace Synapse.Patches.SynapsePatches
{
    [HarmonyPatch(typeof(Door), nameof(Door.Start))]
    internal class DoorStartPatch
    {
        private static void Postfix(global::Door __instance)
        {
            while (SynapseController.Server.Map.Doors.Contains(null))
                SynapseController.Server.Map.Doors.Remove(null);

            SynapseController.Server.Map.Doors.Add(new Api.Components.Door(__instance));
        }
    }
}