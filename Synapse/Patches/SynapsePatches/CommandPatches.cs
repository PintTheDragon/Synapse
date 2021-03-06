﻿using HarmonyLib;
using RemoteAdmin;
using Synapse.Command;
using System;
using Logger = Synapse.Api.Logger;

// ReSharper disable All
namespace Synapse.Patches.SynapsePatches
{
    [HarmonyPatch(typeof(GameCore.Console),nameof(GameCore.Console.TypeCommand))]
    internal static class ServerCommandPatch
    {
        private static bool Prefix(string cmd, CommandSender sender = null)
        {
            var args = cmd.Split(' ');

            if (cmd.StartsWith(".") || cmd.StartsWith("/") || cmd.StartsWith("@"))
                return true;

            if(SynapseController.CommandHandlers.ServerConsoleHandler.TryGetCommand(args[0],out var command))
            {
                try
                {
                    var flag = command.Execute(new Command.CommandContext { Arguments = args.Segment(1), Player = Server.Get.Host, Platform = Command.Platform.ServerConsole});

                    var color = ConsoleColor.DarkBlue;
                    switch (flag.State)
                    {
                        case Command.CommandResultState.Ok:
                            color = ConsoleColor.Gray;
                            break;

                        case Command.CommandResultState.Error:
                            color = ConsoleColor.Red;
                            break;

                        //Since the Console always has all Permissions, a check for NoPermission is not needed!
                    }
                    Logger.Get.Send(flag.Message, color);
                }
                catch(Exception e)
                {
                    Logger.Get.Error($"Synapse-Commands: Command Execution failed!!\n{e}");
                }
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(QueryProcessor),nameof(QueryProcessor.ProcessGameConsoleQuery))]
    internal static class ClientCommandPatch
    {
        private static bool Prefix(QueryProcessor __instance,string query,bool encrypted)
        {
            if (__instance._sender == null) return false;

            var player = __instance._sender.GetPlayer();
            if (player == null) return false;
            var args = query.Split(' ');
            if(SynapseController.CommandHandlers.ClientCommandHandler.TryGetCommand(args[0],out var command))
            {
                try
                {
                    var flag = command.Execute(new Command.CommandContext { Arguments = args.Segment(1), Player = player, Platform = Command.Platform.ClientConsole });

                    var color = "blue";
                    switch (flag.State)
                    {
                        case Command.CommandResultState.Ok:
                            color = "gray";
                            break;

                        case Command.CommandResultState.Error:
                            color = "red";
                            break;

                        case CommandResultState.NoPermission:
                            color = "yellow";
                            break;
                    }

                    player.SendConsoleMessage(flag.Message, color);
                }
                catch(Exception e)
                {
                    Logger.Get.Error($"Synapse-Commands: Command Execution failed!!\n{e}\nStackTrace:\n{e.StackTrace}");
                }
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(CommandProcessor),nameof(CommandProcessor.ProcessQuery))]
    internal static class RACommandPatch
    {
        private static bool Prefix(string q, CommandSender sender)
        {
            var player = sender.GetPlayer();
            var args = q.Split(' ');

            if (q.StartsWith("@"))
                return true;

            if(SynapseController.CommandHandlers.RemoteAdminHandler.TryGetCommand(args[0],out var command))
            {
                try
                {
                    var flag = command.Execute(new Command.CommandContext { Arguments = args.Segment(1),Player = player, Platform = Command.Platform.RemoteAdmin});

                    player.SendRAConsoleMessage(flag.Message, flag.State == CommandResultState.Ok);
                }
                catch(Exception e)
                {
                    Logger.Get.Error($"Synapse-Commands: Command Execution failed!!\n{e}\nStackTrace:\n{e.StackTrace}");
                }
                return false;
            }

            return true;
        }
    }
}
