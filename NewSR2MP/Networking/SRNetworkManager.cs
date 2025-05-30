﻿
using Il2CppMonomiPark.SlimeRancher.Persist;
using NewSR2MP.Networking.Component;
using NewSR2MP.Networking.Packet;
using NewSR2MP.Networking.Patches;
using NewSR2MP.Networking.SaveModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Il2CppMono.Security.Protocol.Ntlm;
using Il2CppTMPro;
using Riptide;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;


namespace NewSR2MP.Networking
{
    public struct NetGameInitialSettings
    {
        public NetGameInitialSettings(bool defaultValueForAll = true) // Would not use paramater here but this version of c# is ehh...
        {
            shareMoney = defaultValueForAll;
            shareKeys = defaultValueForAll;
            shareUpgrades = defaultValueForAll;
        }

        public bool shareMoney;
        public bool shareKeys;
        public bool shareUpgrades;
    }
    public partial class MultiplayerManager
    {
        public static Server server;
        
        public static Client client;
        
        public static NetGameInitialSettings initialWorldSettings = new NetGameInitialSettings();

        internal static void CheckForMPSavePath()
        {
            if (!Directory.Exists(Path.Combine(GameContext.Instance.AutoSaveDirector.StorageProvider.Cast<FileStorageProvider>().savePath, "MultiplayerSaves")))
            {
                Directory.CreateDirectory(Path.Combine(Path.Combine(GameContext.Instance.AutoSaveDirector.StorageProvider.Cast<FileStorageProvider>().savePath, "MultiplayerSaves")));
            }
        }

        public void StartHosting()
        {
            foreach (var a in Resources.FindObjectsOfTypeAll<IdentifiableActor>())
            {
                try
                {
                    if (!string.IsNullOrEmpty(a.gameObject.scene.name))
                    {
                        var actor = a.gameObject;
                        actor.AddComponent<NetworkActor>();
                        actor.AddComponent<NetworkActorOwnerToggle>();
                        actor.AddComponent<TransformSmoother>();
                        actor.AddComponent<NetworkResource>();
                        var ts = actor.GetComponent<TransformSmoother>();
                        ts.interpolPeriod = 0.15f;
                        ts.enabled = false;
                        actors.Add(a.GetActorId().Value, a.GetComponent<NetworkActor>());
                    }
                }
                catch { }
            }

            
            server.ClientDisconnected += OnPlayerLeft;

            sceneContext.gameObject.AddComponent<NetworkTimeDirector>();
            sceneContext.gameObject.AddComponent<NetworkWeatherDirector>();
            
            var hostNetworkPlayer = sceneContext.player.AddComponent<NetworkPlayer>();
            hostNetworkPlayer.id = ushort.MaxValue;
            currentPlayerID = hostNetworkPlayer.id;
            players.Add(ushort.MaxValue, hostNetworkPlayer);
        }

        public void OnPlayerJoined(string username, ushort clientId)
        {
            DoNetworkSave();
            
            var player = Instantiate(onlinePlayerPrefab);
            player.name = $"Player{clientId}";
            var netPlayer = player.GetComponent<NetworkPlayer>();
            
            netPlayer.usernamePanel = netPlayer.transform.GetChild(1).GetComponent<TextMesh>();
            netPlayer.usernamePanel.text = username;
            netPlayer.usernamePanel.characterSize = 0.2f;
            netPlayer.usernamePanel.anchor = TextAnchor.MiddleCenter;
            netPlayer.usernamePanel.fontSize = 24;
            
            players.Add(clientId, netPlayer);
            playerUsernames.Add(username, clientId);
            playerUsernamesReverse.Add(clientId, username);
            
            netPlayer.id = clientId;
            
            DontDestroyOnLoad(player);
            player.SetActive(true);
            
            var packet = new PlayerJoinMessage()
            {
                id = clientId,
                local = false,
                username = username,
            };
            var packet2 = new PlayerJoinMessage()
            {
                id = clientId,
                local = true,
                username = username,
            };
            NetworkSend(packet, ServerSendOptions.SendToAllExcept(clientId));
            NetworkSend(packet2, ServerSendOptions.SendToPlayer(clientId));
        }
        public void OnPlayerLeft(object? sender, ServerDisconnectedEventArgs args)
        {
            OnServerDisconnect(args.Client.Id);
            
            var packet = new PlayerLeaveMessage
            {
                id = args.Client.Id,
            };
            
            NetworkSend(packet);
        }
        
        public void StopHosting()
        {
            ammoByPlotID.Clear();

        }

        public void OnServerDisconnect(ushort player)
        {
            DoNetworkSave();
            
            try
            {
                players[player].enabled = true;
                Destroy(players[player].gameObject);
                players.Remove(player);
                playerUsernames.Remove(playerUsernamesReverse[player]);
                playerUsernamesReverse.Remove(player);
                players.Remove(player);
                clientToGuid.Remove(player);
            }
            catch { }

        }
        public void Leave()
        {
            ammoByPlotID.Clear();
            try
            {
                systemContext.SceneLoader.LoadMainMenuSceneGroup();
            }
            catch { }
        }
        public void ClientDisconnect()
        {
            systemContext.SceneLoader.LoadMainMenuSceneGroup();
        }
        public void InitializeClient()
        {
            var joinMsg = new ClientUserMessage()
            {
                guid = Main.data.Player,
                name = Main.data.Username,
            };
            NetworkSend(joinMsg, ServerSendOptions.SendToAllDefault());
        }

        /// <summary>
        /// The send function common to both server and client. By default uses 'SRMPSendToAll' for server and 'SRMPSend' for client.
        /// </summary>
        /// <typeparam name="M">Message struct type. Ex: 'PlayerJoinMessage'</typeparam>
        /// <param name="message">The actual message itself. Should automatically set the M type paramater.</param>
        public static void NetworkSend<M>(M msg, ServerSendOptions serverOptions) where M : ICustomMessage
        {
            Parallel.Invoke(() =>
            {
                Message message = msg.Serialize();
            
                if (client != null)
                {
                    client.Send(message);
                }
                else if (server != null)
                {
                    if (serverOptions.ignoreSpecificPlayer)
                        server.SendToAll(message, serverOptions.player);
                    else if (serverOptions.onlySendToPlayer)          
                        server.Send(message, serverOptions.player);
                    else
                        server.SendToAll(message);

                }
            });
        }

        public static void NetworkSend<M>(M msg) where M : ICustomMessage
        {
            NetworkSend(msg, ServerSendOptions.SendToAllDefault());
        }

        public struct ServerSendOptions
        {
            public ushort player;
            public bool ignoreSpecificPlayer;
            public bool onlySendToPlayer;

            public static ServerSendOptions SendToAllDefault()
            {
                return new ServerSendOptions()
                {
                    ignoreSpecificPlayer = false,
                    onlySendToPlayer = false,
                    player = UInt16.MinValue
                };
            }
            public static ServerSendOptions SendToPlayer(ushort player)
            {
                return new ServerSendOptions()
                {
                    ignoreSpecificPlayer = false,
                    onlySendToPlayer = true,
                    player = player
                };
            }
            public static ServerSendOptions SendToAllExcept(ushort player)
            {
                return new ServerSendOptions()
                {
                    ignoreSpecificPlayer = true,
                    onlySendToPlayer = false,
                    player = player
                };
            }
        }

        /// <summary>
        /// Erases sync values.
        /// </summary>
        public static void EraseValues()
        {
            foreach (var gadget in gadgets.Values)
            {
                DestroyGadget(gadget.gameObject, "SRMP.EraseValuesGadget");
            }
            foreach (var actor in actors.Values)
            {
                DestroyActor(actor.gameObject, "SRMP.EraseValuesActor");
            }
            actors.Clear();
            gadgets.Clear();

            foreach (var player in players.Values)
            {
                Destroy(player.gameObject);
            }
            players.Clear();
            playerUsernames.Clear();
            playerUsernamesReverse.Clear();

            clientToGuid.Clear();

            ammoByPlotID.Clear();

            savedGame = new NetworkV01();
            savedGamePath = String.Empty;
        }


        public static void DoNetworkSave()
        {

            foreach (var player in players)
            {
                if (player.Key == ushort.MaxValue)
                {
                    continue;
                }

                if (clientToGuid.TryGetValue(player.Key, out var playerID))
                {
                    var ammo = GetNetworkAmmo($"player_{playerID}");
                    Il2CppSystem.Collections.Generic.List<AmmoDataV01> ammoData =
                        GameContext.Instance.AutoSaveDirector.SavedGame.AmmoDataFromSlots(ammo.Slots,
                            GameContext.Instance.AutoSaveDirector._savedGame.identifiableTypeToPersistenceId);
                    savedGame.savedPlayers.playerList[playerID].ammo = ammoData;
                    if (player.Value)
                    {
                        var playerPos = new Vector3V01();
                        playerPos.Value = player.Value.transform.position;
                        var playerRot = new Vector3V01();
                        playerRot.Value = player.Value.transform.eulerAngles;
                        savedGame.savedPlayers.playerList[playerID].position = playerPos;
                        savedGame.savedPlayers.playerList[playerID].rotation = playerRot;
                    }
                }
                else
                {
                    SRMP.Error($"Error saving player {player.Key}, their GUID was not found.");
                }
            }

            GameStream fs = CppFile.Open(savedGamePath, Il2CppSystem.IO.FileMode.Create);
            savedGame.Write(fs);
            fs.Dispose();
        }
    }
}
