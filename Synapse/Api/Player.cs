﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hints;
using Mirror;
using Mirror.LiteNetLib4Mirror;
using RemoteAdmin;
using Searching;
using Synapse.Api.Enum;
using Synapse.Api.Items;
using Synapse.Api.Roles;
using Synapse.Database;
using Synapse.Patches.EventsPatches.PlayerPatches;
using Synapse.Permission;
using UnityEngine;

namespace Synapse.Api
{
    public class Player : MonoBehaviour
    {
        internal Player()
        {
            Hub = GetComponent<ReferenceHub>();
            Scp106Controller = new Scp106Controller(this);
            Scp079Controller = new Scp079Controller(this);
            Scp096Controller = new Scp096Controller(this);
            Jail = new Jail(this);
            ActiveBroadcasts = new BroadcastList(this);
            Inventory = new PlayerInventory(this);
            GrenadeManager = GetComponent<Grenades.GrenadeManager>();
            GameConsoleTransmission = this.GetComponent<GameConsoleTransmission>();
        }

        #region Methods
        public void Kick(string message) => ServerConsole.Disconnect(gameObject, message);

        public void Ban(int duration, string reason, string issuer = "Plugin") => SynapseController.Server.GetObjectOf<BanPlayer>().BanUser(gameObject, duration, reason, issuer);

        public void ChangeRoleAtPosition(RoleType role)
        {
            RoleChangeClassIdPatch.ForceLite = true;
            Hub.characterClassManager.SetClassIDAdv(role, true);
        }

        public void Kill(DamageTypes.DamageType damageType = default) => PlayerStats.HurtPlayer(new PlayerStats.HitInfo(-1f, "WORLD", damageType, 0), gameObject);

        public void GiveTextHint(string message, float duration = 5f)
        {
            Hub.hints.Show(new TextHint(message, new HintParameter[]
                {
                    new StringHintParameter("")
                }, HintEffectPresets.FadeInAndOut(duration), duration));
        }

        internal void ClearBroadcasts() => GetComponent<global::Broadcast>().TargetClearElements(Connection);

        internal void Broadcast(ushort time, string message) => GetComponent<global::Broadcast>().TargetAddElement(Connection, message, time, new global::Broadcast.BroadcastFlags());

        internal void InstantBroadcast(ushort time, string message)
        {
            ClearBroadcasts();
            GetComponent<global::Broadcast>().TargetAddElement(Connection, message, time, new global::Broadcast.BroadcastFlags());
        }

        public void SendConsoleMessage(string message, string color = "red") => ClassManager.TargetConsolePrint(Connection, message, color);

        public void SendRAConsoleMessage(string message, bool success = true, RaCategory type = RaCategory.None) => SynapseExtensions.RaMessage(CommandSender,message, success, type);


        public void GiveEffect(Effect effect, byte intensity = 1, float duration = -1f) => PlayerEffectsController.ChangeByString(effect.ToString().ToLower(), intensity, duration);

        public void RaLogin()
        {
            ServerRoles.RemoteAdmin = true;
            ServerRoles.Permissions = SynapseGroup.GetVanillaPermissionValue() | ServerRoles._globalPerms;
            ServerRoles.RemoteAdminMode = ServerRoles._globalPerms > 0UL ? ServerRoles.AccessMode.GlobalAccess : ServerRoles.AccessMode.PasswordOverride;
            if(!ServerRoles.AdminChatPerms)
                ServerRoles.AdminChatPerms = SynapseGroup.HasVanillaPermission(PlayerPermissions.AdminChat);
            ServerRoles.TargetOpenRemoteAdmin(Connection, false);
        }

        public void RaLogout()
        {
            if (GlobalBadge == GlobalBadge.GlobalBanning && Server.Get.PermissionHandler.ServerSection.GlobalBanTeamAccess)
            {
                RaLogin();
                return;
            }

            if (GlobalBadge == GlobalBadge.Manager && Server.Get.PermissionHandler.ServerSection.ManagerAccess)
            {
                RaLogin();
                return;
            }

            if (GlobalBadge == GlobalBadge.Staff && Server.Get.PermissionHandler.ServerSection.StaffAccess)
            {
                RaLogin();
                return;
            }

            Hub.serverRoles.RemoteAdmin = false;
            Hub.serverRoles.RemoteAdminMode = ServerRoles.AccessMode.LocalAccess;
            Hub.serverRoles.TargetCloseRemoteAdmin(Connection);
        }

        public void Heal(float hp) => PlayerStats.HealHPAmount(hp);

        public void Hurt(int amount, DamageTypes.DamageType damagetype = default, Player attacker = null) =>
            PlayerStats.HurtPlayer(new PlayerStats.HitInfo(amount, attacker == null ? "WORLD" : attacker.NickName, damagetype, attacker == null ? 0 : attacker.PlayerId), gameObject);

        public void OpenReportWindow(string text) => GameConsoleTransmission.SendToClient(Connection, "[REPORTING] " + text, "white");

        public void SendToServer(ushort port)
        {
            var component = SynapseController.Server.Host.PlayerStats;
            var writer = NetworkWriterPool.GetWriter();
            writer.WriteSingle(1f);
            writer.WriteUInt16(port);
            var msg = new RpcMessage
            {
                netId = component.netId,
                componentIndex = component.ComponentIndex,
                functionHash = typeof(PlayerStats).FullName.GetStableHashCode() * 503 + "RpcRoundrestartRedirect".GetStableHashCode(),
                payload = writer.ToArraySegment()
            };
            Connection.Send(msg);
            NetworkWriterPool.Recycle(writer);
        }

        public void DimScreen()
        {
            var component = RoundSummary.singleton;
            var writer = NetworkWriterPool.GetWriter();
            var msg = new RpcMessage
            {
                netId = component.netId,
                componentIndex = component.ComponentIndex,
                functionHash = typeof(RoundSummary).FullName.GetStableHashCode() * 503 + "RpcDimScreen".GetStableHashCode(),
                payload = writer.ToArraySegment()
            };
            Connection.Send(msg);
            NetworkWriterPool.Recycle(writer);
        }

        public void ShakeScreen(bool achieve = false)
        {
            var component = AlphaWarheadController.Host;
            var writer = NetworkWriterPool.GetWriter();
            writer.WriteBoolean(achieve);
            var msg = new RpcMessage
            {
                netId = component.netId,
                componentIndex = component.ComponentIndex,
                functionHash = typeof(AlphaWarheadController).FullName.GetStableHashCode() * 503 + "RpcShake".GetStableHashCode(),
                payload = writer.ToArraySegment()
            };
            Connection.Send(msg);
            NetworkWriterPool.Recycle(writer);
        }

        public void PlaceBlood(Vector3 pos, int type, float size)
        {
            var component = ClassManager;
            var writer = NetworkWriterPool.GetWriter();
            writer.WriteVector3(pos);
            writer.WritePackedInt32(type);
            writer.WriteSingle(size);
            var msg = new RpcMessage
            {
                netId = component.netId,
                componentIndex = component.ComponentIndex,
                functionHash = typeof(CharacterClassManager).FullName.GetStableHashCode() * 503 + "RpcPlaceBlood".GetStableHashCode(),
                payload = writer.ToArraySegment()
            };
            Connection.Send(msg);
            NetworkWriterPool.Recycle(writer);
        }

        private float delay = Time.time;
        private int pos = 0;

        private void Update()
        {
            if (this == Server.Get.Host || SynapseGroup.Color.ToUpper() != "RAINBOW") return;

            if(Time.time >= delay)
            {
                delay = Time.time + 1f;

                RankColor = Server.Get.Colors.ElementAt(pos);

                pos = pos + 1 >= Server.Get.Colors.Count() ? 0 : pos +1;
            }
        }
        #endregion

        #region Synapse Api Stuff
        public readonly Jail Jail;

        public readonly Scp106Controller Scp106Controller;

        public readonly Scp079Controller Scp079Controller;

        public readonly Scp096Controller Scp096Controller;

        public BroadcastList ActiveBroadcasts { get; }

        public PlayerInventory Inventory { get; }

        public Broadcast SendBroadcast(ushort time,string message,bool instant = false)
        {
            if(this == Server.Get.Host)
                Logger.Get.Send($"Broadcast: {message}", ConsoleColor.White);

            var bc = new Broadcast(message, time,this);
            ActiveBroadcasts.Add(bc, instant);
            return bc;
        }

        private IRole _role;

        public IRole CustomRole
        {
            get => _role;
            set
            {
                if (_role != null)
                    _role.DeSpawn();

                _role = value;

                if (_role == null) return;

                _role.Player = this;
                _role.Spawn();
            }
        }

        public int RoleID
        {
            get
            {
                if (CustomRole == null) return (int)RoleType;
                else return CustomRole.GetRoleID();
            }
            set
            {
                if(value >= 0 && value <= RoleManager.HighestRole)
                {
                    CustomRole = null;
                    RoleType = (RoleType)value;
                    return;
                }

                if (!Server.Get.RoleManager.IsIDRegistered(value))
                    throw new Exception("Plugin tried to set the RoleId of a Player with an not registered RoldeID");

                CustomRole = Server.Get.RoleManager.GetCustomRole(value);
            }
        }

        public string RoleName => CustomRole == null ? RoleType.ToString() : CustomRole.GetRoleName();

        //Stuff for the Permission System
        private SynapseGroup synapseGroup;

        public SynapseGroup SynapseGroup
        {
            get
            {
                if (synapseGroup == null)
                    return Server.Get.PermissionHandler.GetPlayerGroup(this);

                return synapseGroup;
            }
            set
            {
                if (value == null)
                    return;

                synapseGroup = value;

                RefreshPermission(HideRank);
            }
        }

        public bool HasPermission(string permission) => this == Server.Get.Host ? true : SynapseGroup.HasPermission(permission);

        public void RefreshPermission(bool disp)
        {
            var group = new UserGroup
            {
                BadgeText = SynapseGroup.Badge.ToUpper() == "NONE" ? null : SynapseGroup.Badge,
                BadgeColor = SynapseGroup.Color.ToLower(),
                Cover = SynapseGroup.Cover,
                HiddenByDefault = SynapseGroup.Hidden,
                KickPower = SynapseGroup.KickPower,
                Permissions = SynapseGroup.GetVanillaPermissionValue(),
                RequiredKickPower = SynapseGroup.RequiredKickPower,
                Shared = false
            };

            ServerRoles.Group = group;

            if (!ServerRoles.OverwatchPermitted && SynapseGroup.HasVanillaPermission(PlayerPermissions.Overwatch))
                ServerRoles.OverwatchPermitted = true;

            if (SynapseGroup.RemoteAdmin)
                RaLogin();
            else
                RaLogout();

            ServerRoles.SendRealIds();

            var flag = ServerRoles.Staff || SynapseGroup.HasVanillaPermission(PlayerPermissions.ViewHiddenBadges);
            var flag2 = ServerRoles.Staff || SynapseGroup.HasVanillaPermission(PlayerPermissions.ViewHiddenGlobalBadges);

            if(flag || flag2)
                foreach(var player in Server.Get.Players)
                {
                    if (!string.IsNullOrEmpty(player.ServerRoles.HiddenBadge) && (!player.ServerRoles.GlobalHidden || flag2) && (player.ServerRoles.GlobalHidden || flag))
                        player.ServerRoles.TargetSetHiddenRole(Connection, player.ServerRoles.HiddenBadge);
                }

            if (group.BadgeColor == "none")
                return;

            if (ServerRoles._hideLocalBadge || (group.HiddenByDefault && !disp && !ServerRoles._neverHideLocalBadge))
            {
                ServerRoles._badgeCover = false;
                if (!string.IsNullOrEmpty(RankName))
                    return;

                ServerRoles.NetworkMyText = null;
                ServerRoles.NetworkMyColor = "default";
                ServerRoles.HiddenBadge = group.BadgeText;
                ServerRoles.RefreshHiddenTag();
                ServerRoles.TargetSetHiddenRole(Connection, group.BadgeText);
            }
            else
            {
                ServerRoles.HiddenBadge = null;
                ServerRoles.RpcResetFixed();
                ServerRoles.NetworkMyText = group.BadgeText;
                ServerRoles.NetworkMyColor = group.BadgeColor;
            }
        }

        public GlobalBadge GlobalBadge { get; internal set; }

        internal Dictionary<string, string> globalBadgeRequest;
        #endregion

        #region Default Stuff
        public string DisplayName 
        { 
            get => NicknameSync.DisplayName; 
            set => NicknameSync.DisplayName = value; 
        }

        public string DisplayInfo
        {
            get => NicknameSync._customPlayerInfoString;
            set => NicknameSync.Network_customPlayerInfoString = value;
        }

        public int PlayerId
        {
            get => QueryProcessor.PlayerId;
            set => QueryProcessor.NetworkPlayerId = value;
        }

        public string UserId
        {
            get => ClassManager.UserId;
            set => ClassManager.UserId = value;
        }

        public string SecondUserID
        {
            get => ClassManager.UserId2;
            set => ClassManager.UserId2 = value;
        }

        public bool NoClip
        {
            get => ServerRoles.NoclipReady;
            set => ServerRoles.NoclipReady = value;
        }

        public bool OverWatch
        {
            get => ServerRoles.OverwatchEnabled;
            set => ServerRoles.OverwatchEnabled = value;
        }

        public bool Bypass
        {
            get => ServerRoles.BypassMode;
            set => ServerRoles.BypassMode = value;
        }

        public bool GodMode
        {
            get => ClassManager.GodMode;
            set => ClassManager.GodMode = value;
        }

        public bool Invisible { get; set; }

        public Vector3 Position
        {
            get => PlayerMovementSync.GetRealPosition();
            set => PlayerMovementSync.OverridePosition(value, 0f);
        }

        public Vector2 Rotation
        {
            get => PlayerMovementSync.RotationSync;
            set => PlayerMovementSync.NetworkRotationSync = value;
        }

        public Vector3 DeathPosition
        {
            get => ClassManager.DeathPosition;
            set => ClassManager.DeathPosition = value;
        }

        public long DeathTime
        {
            get => ClassManager.DeathTime;
            set => ClassManager.DeathTime = value;
        }

        public Vector3 Scale
        {
            get => transform.localScale;
            set
            {
                try
                {
                    transform.localScale = value;

                    var method = typeof(NetworkServer).GetMethod("SendSpawnMessage", BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);

                    foreach(var ply in Server.Get.Players)
                        method.Invoke(null, new object[] { NetworkIdentity, ply.Connection });
                }
                catch (Exception e)
                {
                    SynapseController.Server.Logger.Error($"Synapse-Player: SetScale failed!!\n{e}");
                }
            }
        }

        public float Health
        {
            get => PlayerStats.Health;
            set => PlayerStats.Health = value;
        }

        public int MaxHealth
        {
            get => PlayerStats.maxHP;
            set => PlayerStats.maxHP = value;
        }

        public float ArtificialHealth
        {
            get => PlayerStats.unsyncedArtificialHealth;
            set => PlayerStats.unsyncedArtificialHealth = value;
        }

        public int MaxArtificialHealth
        {
            get => PlayerStats.maxArtificialHealth;
            set => PlayerStats.maxArtificialHealth = value;
        }

        public float Stamina
        {
            get => Hub.fpc.staminaController.RemainingStamina * 100;
            set => Hub.fpc.staminaController.RemainingStamina = (value / 100f);
        }

        public float StaminaUsage
        {
            get => Hub.fpc.staminaController.StaminaUse * 100;
            set => Hub.fpc.staminaController.StaminaUse = (value / 100f);
        }

        public RoleType RoleType
        {
            get => ClassManager.CurClass;
            set => ClassManager.SetPlayersClass(value, gameObject);
        }

        public Room Room
        {
            get
            {
                if (Vector3.Distance(Vector3.up * -1997, Position) <= 50) return Map.Get.GetRoom(RoomInformation.RoomType.POCKET);
                return Map.Get.Rooms.FirstOrDefault(x => x.GameObject == Hub.localCurrentRoomEffects.curRoom);
            }
            set => Position = value.Position;
        }

        public MapPoint MapPoint
        {
            get => new MapPoint(Room, Position);
            set => Position = value.Position;
        }

        internal Inventory.SyncListItemInfo VanillaItems
        {
            get => VanillaInventory.items;
            set => VanillaInventory.items = value;
        }

        public Player Cuffer
        {
            get => SynapseController.Server.GetPlayer(Handcuffs.CufferId);
            set
            {

                var handcuff = value.Handcuffs;

                if (handcuff == null) return;

                if (value == null)
                {
                    Handcuffs.NetworkCufferId = -1;
                    return;
                }

                Handcuffs.NetworkCufferId = value.PlayerId;
            }
        }

        public GameObject LookingAt
        {
            get
            {
                if (!Physics.Raycast(CameraReference.transform.position, CameraReference.transform.forward, out RaycastHit raycastthit, 100f))
                    return null;

                return raycastthit.transform.gameObject;
            }
        }

        public uint Ammo5 
        { 
            get
            {
                try
                {
                    return AmmoBox[0];
                }
                catch
                {
                    return 0;
                }
            }
            set
            {
                try
                {
                    AmmoBox[0] = value;
                }
                catch
                {
                }
            }
        }

        public uint Ammo7
        {
            get
            {
                try
                {
                    return AmmoBox[1];
                }
                catch
                {
                    return 0;
                }
            }
            set
            {
                try
                {
                    AmmoBox[1] = value;
                }
                catch
                {
                }
            }
        }

        public uint Ammo9
        {
            get
            {
                try
                {
                    return AmmoBox[2];
                }
                catch
                {
                    return 0;
                }
            }
            set
            {
                try
                {
                    AmmoBox[2] = value;
                }
                catch
                {
                }
            }
        }

        public UserGroup Rank 
        { 
            get => ServerRoles.Group; 
            set => ServerRoles.SetGroup(value, value != null && value.Permissions > 0UL, true); 
        }

        public string RankColor
        {
            get => Rank.BadgeColor;
            set => ServerRoles.SetColor(value);
        }

        public string RankName
        {
            get => Rank.BadgeText;
            set => ServerRoles.SetText(value);
        }

        public bool HideRank
        {
            get => string.IsNullOrEmpty(ServerRoles.HiddenBadge);
            set
            {
                if (value)
                    ClassManager.CmdRequestHideTag();
                else
                    ClassManager.CallCmdRequestShowTag(false);
            }
        }

        public ulong Permission 
        { 
            get => ServerRoles.Permissions; 
            set => ServerRoles.Permissions = value; 
        }

        public bool IsMuted 
        { 
            get => ClassManager.NetworkMuted; 
            set => ClassManager.NetworkMuted = value; 
        }

        public bool IsIntercomMuted
        { 
            get => ClassManager.NetworkIntercomMuted; 
            set => ClassManager.NetworkIntercomMuted = value; 
        }

        public string UnitName 
        { 
            get => ClassManager.NetworkCurUnitName; 
            set => ClassManager.NetworkCurUnitName = value; 
        }




        public CommandSender CommandSender
        {
            get
            {
                if (this == SynapseController.Server.Host) return ServerConsole._scs;
                return QueryProcessor._sender;
            }
        }

        public ZoneType Zone => Room.Zone;

        public bool DoNotTrack => ServerRoles.DoNotTrack;

        public bool IsDead => Team == Team.RIP;

        public bool IsZooming => WeaponManager.ZoomInProgress();

        public bool IsReloading => WeaponManager.IsReloading();

        public bool IsCuffed => Cuffer != null;

        public float AliveTime => ClassManager.AliveTime;

        public string AuthToken => ClassManager.AuthToken;

        public int Ping => LiteNetLib4MirrorServer.Peers[Connection.connectionId].Ping;

        public string NickName => NicknameSync.Network_myNickSync;

        public Team Team => ClassManager.CurRole.team;

        public Team RealTeam => (CustomRole == null) ? Team : CustomRole.GetTeam();

        public Fraction Fraction => ClassManager.Fraction;

        public Fraction RealFraction => Misc.GetFraction(RealTeam);

        public Items.SynapseItem ItemInHand => VanillaInventory.GetItemInHand().GetSynapseItem();

        public NetworkConnection Connection => ClassManager.Connection;

        public string IpAddress => QueryProcessor._ipAddress;
        #endregion

        #region ReferenceHub
        public Transform CameraReference => Hub.PlayerCameraReference;

        public NetworkIdentity NetworkIdentity => Hub.networkIdentity;

        public GameConsoleTransmission GameConsoleTransmission { get; }

        public Grenades.GrenadeManager GrenadeManager { get; }

        public LocalCurrentRoomEffects LocalCurrentRoomEffects => Hub.localCurrentRoomEffects;

        public WeaponManager WeaponManager => Hub.weaponManager;

        public AmmoBox AmmoBox => Hub.ammoBox;

        public HintDisplay HintDisplay => Hub.hints;

        public SearchCoordinator SearchCoordinator => Hub.searchCoordinator;

        public FootstepSync FootstepSync => Hub.footstepSync;

        public PlayerEffectsController PlayerEffectsController => Hub.playerEffectsController;

        public PlayerInteract PlayerInteract => Hub.playerInteract;

        public Handcuffs Handcuffs => Hub.handcuffs;

        public FallDamage FallDamage => Hub.falldamage;

        public AnimationController AnimationController => Hub.animationController;

        public SpectatorManager SpectatorManager => Hub.spectatorManager;

        public NicknameSync NicknameSync => Hub.nicknameSync;

        public PlayerMovementSync PlayerMovementSync => Hub.playerMovementSync;

        public QueryProcessor QueryProcessor => Hub.queryProcessor;

        public ServerRoles ServerRoles => Hub.serverRoles;

        public PlayerStats PlayerStats => Hub.playerStats;

        public Inventory VanillaInventory => Hub.inventory;

        public CharacterClassManager ClassManager => Hub.characterClassManager;

        public readonly ReferenceHub Hub;
        #endregion

        #region Persistence

        public string GetData(string key)
        {
            DatabaseManager.CheckEnabledOrThrow();
            var dbo = DatabaseManager.PlayerRepository.FindByGameId(UserId);
            return dbo.Data.ContainsKey(key) ? dbo.Data[key] : null;
        }

        public void SetData(string key, string value)
        {
            DatabaseManager.CheckEnabledOrThrow();
            var dbo = DatabaseManager.PlayerRepository.FindByGameId(UserId);
            dbo.Data[key] = value;
            if (value == null) dbo.Data.Remove(key);
            DatabaseManager.PlayerRepository.Save(dbo);
        }
        
        #endregion

        public override string ToString() => NickName;
    }
}
