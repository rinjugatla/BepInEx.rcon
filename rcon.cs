using BepInEx;
using BepInEx.Configuration;
using Rcon.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Rcon
{
    [BepInPlugin("nl.avii.plugins.rcon", "rcon", "1.0")]
    public class Rcon : BaseUnityPlugin
    {
        public delegate string UnknownCommand(string command, string[] args);
        public event UnknownCommand OnUnknownCommand;

        public delegate string ParamsAction(params object[] args);
        private AsynchronousSocketListener SocketListener;

        private ConfigEntry<bool> Enabled;
        private ConfigEntry<int> Port;
        private ConfigEntry<string> Password;

        private Dictionary<string, Type> Commands = new Dictionary<string, Type>();
        private Dictionary<string, ParamsAction> CustomCommands = new Dictionary<string, ParamsAction>();

        private Dictionary<string, BaseUnityPlugin> Owners = new Dictionary<string, BaseUnityPlugin>();

        private Rcon()
        {
            Enabled = Config.Bind("rcon", "enabled", false, "Enable RCON Communication");
            Port = Config.Bind("rcon", "port", 2458, "Port to use for RCON Communication");
            Password = Config.Bind("rcon", "password", "ChangeMe", "Password to use for RCON Communication");
        }

        private void OnEnable()
        {
            if (!Enabled.Value) return;
            SocketListener = new AsynchronousSocketListener();
            SocketListener.OnMessage += SocketListener_OnMessage;

            Logger.LogInfo("RCON Listening on port: " + Port.Value);
            SocketListener.StartListening(Port.Value);
        }

        private void SocketListener_OnMessage(Socket socket, int requestId, PacketType type, string payload)
        {
            switch (type)
            {
                case PacketType.Login:

                    string response_payload = "Login Success";
                    if (payload.Trim() != Password.Value.Trim())
                    {
                        response_payload = "Login Failed";
                        requestId = -1;
                    }

                    byte[] packet = PacketBuilder.CreatePacket(requestId, type, response_payload);

                    socket.Send(packet);
                    break;
                case PacketType.Command:
                    // strip slash if present
                    if (payload[0] == '/')
                    {
                        payload = payload.Substring(1);
                    }

                    var data = Regex.Matches(payload, @"(?<=[ ][\""]|^[\""])[^\""]+(?=[\""][ ]|[\""]$)|(?<=[ ]|^)[^\"" ]+(?=[ ]|$)")
                        .Cast<Match>()
                        .Select(m => m.Value)
                        .ToList();

                    string command = data[0].ToLower();
                    data.RemoveAt(0);

                    if (!Commands.ContainsKey(command) && !CustomCommands.ContainsKey(command))
                    {
                        var ret = OnUnknownCommand?.Invoke(command, data.ToArray());
                        PacketType t = PacketType.Command;
                        if (ret.ToLower().Contains("unknown"))
                            t = PacketType.Error;

                        socket.Send(PacketBuilder.CreatePacket(requestId, t, ret));
                        return;
                    }

                    if (Commands.ContainsKey(command))
                    {
                        var t = Commands[command];
                        var instance = (ICommand)Activator.CreateInstance(t);
                        instance.SetOwner(Owners[command]);
                        var response = instance.OnCommand(data.ToArray());
                        socket.Send(PacketBuilder.CreatePacket(requestId, type, response));
                    }
                    else if (CustomCommands.ContainsKey(command))
                    {
                        var response = CustomCommands[command](data.ToArray());
                        socket.Send(PacketBuilder.CreatePacket(requestId, type, response));
                    }
                    break;
                default:
                    Logger.LogError($"Unknown packet type: {type}");
                    break;
            }
        }

        private void Update()
        {
            if (!Enabled.Value) return;
            if (SocketListener == null) return;

            SocketListener.Update();
        }

        private void OnDisable()
        {
            if (!Enabled.Value) return;
            SocketListener.Close();
        }

        public void RegisterCommand<T>(BaseUnityPlugin owner, string command) where T : AbstractCommand, new()
        {
            command = command.ToLower();
            if (Owners.ContainsKey(command))
            {
                Logger.LogError($"{command} already registered");
                return;
            }
            Owners[command] = owner;
            Commands[command] = typeof(T);
            Logger.LogInfo($"Registering Command: {command}");
        }

        public void RegisterCommand(BaseUnityPlugin owner, string command, ParamsAction action)
        {
            command = command.ToLower();
            if (Owners.ContainsKey(command))
            {
                Logger.LogError($"{command} already registered");
                return;
            }
            Owners[command] = owner;
            CustomCommands[command] = action;
            Logger.LogInfo($"Registering Command: {command}");
        }

        public void UnRegisterCommand(BaseUnityPlugin owner, string command)
        {
            if (!Owners.ContainsKey(command))
            {
                return;
            }

            if (Owners[command] != owner)
            {
                return;
            }

            Owners.Remove(command);

            if (Commands.ContainsKey(command))
                Commands.Remove(command);

            if (CustomCommands.ContainsKey(command))
                CustomCommands.Remove(command);
        }
    }
}
