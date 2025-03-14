﻿using System;
using System.Collections;
using System.Collections.Generic;
using Butterfly.HabboHotel.ChatMessageStorage;
using Butterfly.HabboHotel.GameClients;
using Butterfly.HabboHotel.Misc;
using Butterfly.HabboHotel.Pathfinding;
using Butterfly.HabboHotel.Pets;
using Butterfly.HabboHotel.RoomBots;
using Butterfly.HabboHotel.Rooms.Games;
using Butterfly.IRC;
using Butterfly.Messages;
using Uber.HabboHotel.Rooms;
using System.Drawing;
using Butterfly.Core;

namespace Butterfly.HabboHotel.Rooms
{
    public class RoomUser : IEquatable<RoomUser>
    {
        internal UInt32 HabboId;
        internal Int32 VirtualId;
        internal UInt32 RoomId;

        internal int IdleTime;//byte
        //internal int Steps;

        internal int X;//byte
        internal int Y;//byte
        internal double Z;
        internal byte SqState;

        internal int CarryItemID;//byte
        internal int CarryTimer;//byte

        internal int RotHead;//byte
        internal int RotBody;//byte

        internal bool CanWalk;
        internal bool AllowOverride;
        internal bool TeleportEnabled;

        internal int GoalX;//byte
        internal int GoalY;//byte

        internal Boolean SetStep;
        internal int SetX;//byte
        internal int SetY;//byte
        internal double SetZ;
        internal uint userID;

        internal RoomBot BotData;
        internal BotAI BotAI;

        internal ItemEffectType CurrentItemEffect;
        internal bool Freezed;
        internal int FreezeCounter;
        internal Team team;
        internal FreezePowerUp banzaiPowerUp;
        internal int FreezeLives;

        internal bool shieldActive;
        internal int shieldCounter;

        internal bool throwBallAtGoal;
        internal bool moonwalkEnabled = false;
        internal bool isFlying = false;
        internal bool isSitting = false;
        internal int flyk = 0;

        internal bool LyingDown;
        internal bool Sitting;
        
        internal Point Coordinate
        {
            get
            {
                return new Point(X, Y);
            }
        }

        public bool Equals(RoomUser comparedUser)
        {
            return (comparedUser.HabboId == this.HabboId);
        }

        internal bool IsPet
        {
            get
            {
                return (IsBot && BotData.IsPet);
            }
        }

        internal string GetUsername()
        {
            if (IsBot)
                return string.Empty;

            if (this.GetClient() != null)
            {
                return GetClient().GetHabbo().Username;
            }

            return string.Empty;
        }

        internal Pet PetData;

        internal Boolean IsWalking;
        internal Boolean UpdateNeeded;
        internal Boolean IsAsleep;

        internal Dictionary<string, string> Statusses;

        internal int DanceId;

        //internal List<Coord> Path;
        //internal int PathStep;

        //internal bool PathRecalcNeeded;
        //internal int PathRecalcX;
        //internal int PathRecalcY;
        //private int mMessageAmount;

        //internal int TeleDelay;//byte
        private int FloodCount;//byte
        private DateTime FloodTime;

        internal Boolean IsDancing
        {
            get
            {
                if (DanceId >= 1)
                {
                    return true;
                }

                return false;
            }
        }

        internal Boolean NeedsAutokick
        {
            get
            {
                if (IsBot)
                    return false;
                if (GetClient() == null || GetClient().GetHabbo() == null)
                    return true;
                if (GetClient().GetHabbo().Rank > 1)
                    return false;
                if (IdleTime >= 1800)
                    return true;

                return false;
            }
        }

        internal bool IsTrading
        {
            get
            {
                if (IsBot)
                {
                    return false;
                }

                if (Statusses.ContainsKey("trd"))
                {
                    return true;
                }

                return false;
            }
        }

        internal bool IsOwner()
        {
            if (IsBot)
                return false;
            return (GetUsername() == GetRoom().Owner);
        }

        internal bool IsBot
        {
            get
            {
                if (this.BotData != null)
                {
                    return true;
                }

                return false;
            }
        }

        internal bool IsSpectator;

        internal int InternalRoomID;

        private Queue events;

        internal RoomUser(uint HabboId, uint RoomId, int VirtualId, Room room, bool isSpectator)
        {
            this.Freezed = false;
            this.HabboId = HabboId;
            this.RoomId = RoomId;
            this.VirtualId = VirtualId;
            this.IdleTime = 0;
            this.X = 0;
            this.Y = 0;
            this.Z = 0;
            this.RotHead = 0;
            this.RotBody = 0;
            this.UpdateNeeded = true;
            this.Statusses = new Dictionary<string, string>();
            //this.Path = new List<Coord>();
            //this.PathStep = 0;
            //this.TeleDelay = -1;
            this.mRoom = room;

            this.AllowOverride = false;
            this.CanWalk = true;

            this.IsSpectator = isSpectator;
            this.SqState = 3;
            //this.Steps = 0;

            this.InternalRoomID = 0;
            this.CurrentItemEffect = ItemEffectType.None;
            this.events = new Queue();
            this.FreezeLives = 0;
            //this.mMessageAmount = 0;
        }

        internal RoomUser(uint HabboId, uint RoomId, int VirtualId, GameClient pClient, Room room)
        {
            this.mClient = pClient;
            this.Freezed = false;
            this.HabboId = HabboId;
            this.RoomId = RoomId;
            this.VirtualId = VirtualId;
            this.IdleTime = 0;
            this.X = 0;
            this.Y = 0;
            this.Z = 0;
            this.RotHead = 0;
            this.RotBody = 0;
            this.UpdateNeeded = true;
            this.Statusses = new Dictionary<string, string>();
            //this.Path = new List<Coord>();
            //this.PathStep = 0;
            //this.TeleDelay = -1;

            this.AllowOverride = false;
            this.CanWalk = true;

            this.IsSpectator = false;
            this.SqState = 3;
            //this.Steps = 0;

            this.InternalRoomID = 0;
            this.CurrentItemEffect = ItemEffectType.None;
            this.mRoom = room;
            this.events = new Queue();
        }

        internal void Unidle()
        {
            this.IdleTime = 0;

            if (this.IsAsleep)
            {
                this.IsAsleep = false;

                ServerMessage Message = new ServerMessage(486);
                Message.AppendInt32(VirtualId);
                Message.AppendBoolean(false);

                GetRoom().SendMessage(Message);
            }
        }

        internal void OnFly()
        {
            if (flyk == 0)
            {
                flyk++;
                return;
            }

            double lastK = 0.5 * Math.Sin(0.7 * flyk);
            flyk++;
            double nextK = 0.5 * Math.Sin(0.7 * flyk);
            double differance = nextK - lastK;

            GetRoom().SendMessage(GetRoom().GetRoomItemHandler().UpdateUserOnRoller(this, this.Coordinate, 0, this.Z + differance));
        }

        internal void Dispose()
        {
            Statusses.Clear();
            mRoom = null;
            mClient = null;
        }

        internal void Chat(GameClient Session, string Message, bool Shout)
        {
            if (Session != null)
            {
                if (Session.GetHabbo().Rank < 5)
                {
                    if (GetRoom().RoomMuted)
                        return;
                }
            }

            Unidle();

            if (!IsBot && GetClient().GetHabbo().Muted)
            {
                GetClient().SendNotif("You are muted.");
                return;
            }

            if (Message.StartsWith(":") && Session != null)
            {
                string[] parsedCommand = Message.Split(' ');
                if (ChatCommandRegister.IsChatCommand(parsedCommand[0].ToLower().Substring(1)))
                {
                    ChatCommandHandler handler = new ChatCommandHandler(Message.Split(' '), Session);

                    if (handler.WasExecuted())
                    {
                        Logging.LogMessage(string.Format("User {0} issued command {1}", GetUsername(), Message));
                        if (Session.GetHabbo().Rank > 5)
                        {
                            ButterflyEnvironment.GetGame().GetModerationTool().LogStaffEntry(Session.GetHabbo().Username, string.Empty, "Chat command", string.Format("Issued chat command {0}", Message));
                        }
                        return;
                    }
                }
            }

            uint rank = 1;
            Message = LanguageLocale.FilterSwearwords(Message);
            if (!IsBot && Session != null && Session.GetHabbo() != null)
                rank = Session.GetHabbo().Rank;

            TimeSpan SinceLastMessage = DateTime.Now - FloodTime;
            if (SinceLastMessage.Seconds > 4)
                FloodCount = 0;

            if (SinceLastMessage.Seconds < 4 && FloodCount > 5 && !IsBot && rank < 5)
            {
                ServerMessage Packet = new ServerMessage(27);
                Packet.AppendInt32(30); // Blocked for 30 sec
                GetClient().SendMessage(Packet);
                return;
            }
            FloodTime = DateTime.Now;
            FloodCount++;

            if (!IsBot)
            {
                ButterflyEnvironment.GetGame().GetQuestManager().ProgressUserQuest(Session, HabboHotel.Quests.QuestType.SOCIAL_CHAT);
            }

            // **LOG CHAT NEL DATABASE**
            if (!IsBot && Session != null && Session.GetHabbo() != null)
            {
                //Console.WriteLine("Chiamata LogChat con messaggio: " + Message);
                Session.GetHabbo().LogChat(Message, false); // Salva il messaggio nel database
            }

            InvokedChatMessage message = new InvokedChatMessage(this, Message, Shout);
            GetRoom().QueueChatMessage(message);
        }


        internal void OnChat(InvokedChatMessage message)
        {
            string Message = message.message;

            if (GetRoom() != null && !GetRoom().AllowsShous(this, Message))
                return;

            uint ChatHeader = 24;

            if (message.shout)
            {
                ChatHeader = 26;
            }

            string Site = "";

            ServerMessage ChatMessage = new ServerMessage(ChatHeader);
            
            ChatMessage.AppendInt32(VirtualId);

            System.Text.RegularExpressions.Regex site = new System.Text.RegularExpressions.Regex(@"^http(s)?://([\w-]+.)+[\w-]+(/[\w- ./?%&=])?$");
            if (site.Matches(Message).Count != 0)
            {
                Site = site.Match(Message).ToString();
                Message = Message.Replace(Site, "{0}");
            }
            // unloads room if done wrong
            //if (Message.Contains("http://") || Message.Contains("www."))
            //{

            //    string[] Split = Message.Split(' ');

            //    foreach (string Msg in Split)
            //    {
            //        if (Msg.StartsWith("http://") || Msg.StartsWith("www."))
            //        {
            //            Site = Msg;
            //        }
            //    }
            //    if (!string.IsNullOrEmpty(Site))
            //    {
            //        Message = Message.Replace(Site, "{0}");
            //    }
            //}

            ChatMessage.AppendStringWithBreak(Message);

            if (!string.IsNullOrEmpty(Site))
            {
                ChatMessage.AppendBoolean(false);
                ChatMessage.AppendBoolean(true);
                ChatMessage.AppendStringWithBreak(Site.Replace("http://", string.Empty));
                ChatMessage.AppendStringWithBreak(Site);
            }

            ChatMessage.AppendInt32(GetSpeechEmotion(Message));
            ChatMessage.AppendBoolean(false);

            //annoying as fuck
            //GetRoom().GetRoomUserManager().TurnHeads(X, Y, HabboId);
            GetRoom().SendMessage(ChatMessage);

            if (!IsBot)
            {
                GetRoom().OnUserSay(this, Message, message.shout);
                LogMessage(Message);
            }

            message.Dispose();
        }

        private static void LogMessage(string message)
        {
        //    ChatMessage chatMessage = ChatMessageFactory.CreateMessage(message, GetClient(), GetRoom());

        //    foreach (RoomUser user in GetRoom().UserList.Values)
        //    {
        //        if (!user.IsBot && user.GetClient() != null && user.GetClient().GetHabbo() != null)
        //            user.GetClient().GetHabbo().GetChatMessageManager().AddMessage(chatMessage);
        //    }

        //    GetRoom().GetChatMessageManager().AddMessage(chatMessage);
        }

        internal static int GetSpeechEmotion(string Message)
        {
            Message = Message.ToLower();

            if (Message.Contains(":)") || Message.Contains(":d") || Message.Contains("=]") || 
                Message.Contains("=d") || Message.Contains(":>"))
            {
                return 1;
            }

            if (Message.Contains(">:(") || Message.Contains(":@"))
            {
                return 2;
            }

            if (Message.Contains(":o"))
            {
                return 3;
            }

            if (Message.Contains(":(") || Message.Contains("=[") || Message.Contains(":'(") || Message.Contains("='["))
            {
                return 4;
            }

            return 0;
        }

        internal void ClearMovement(bool Update)
        {
            IsWalking = false;
            Statusses.Remove("mv");
            GoalX = 0;
            GoalY = 0;
            SetStep = false;
            SetX = 0;
            SetY = 0;
            SetZ = 0;

            if (Update)
            {
                UpdateNeeded = true;
            }
        }

        internal void MoveTo(Point c)
        {
            MoveTo(c.X, c.Y);
        }

        internal void MoveTo(int pX, int pY, bool pOverride)
        {
            if (GetRoom().GetGameMap().SquareHasUsers(pX, pY) && !pOverride)
                return;
            foreach (Items.RoomItem item in GetRoom().GetGameMap().GetAllRoomItemForSquare(pX, pY))
            {
                if (!item.GetBaseItem().Walkable)
                {
                    return;
                }
            }
            int mX = X;
            int mY = Y;
            //Gamemap map = GetRoom().GetGameMap();
            //while (mX != pX && mY != pY)
            //{
            //    SquarePoint point = DreamPathfinder.GetNextStep(X, Y, pX, pY, map.GameMap, map.ItemHeightMap, map.Model.MapSizeX, map.Model.MapSizeY, pOverride, map.DiagonalEnabled);
            //    foreach (Items.RoomItem item in GetRoom().GetGameMap().GetAllRoomItemForSquare(mX, mY))
            //    {
            //        if (!item.GetBaseItem().Walkable)
            //        {
            //            return;
            //        }
            //    }
            //    mX = point.X;
            //    mY = point.Y;
            //}
            Unidle();

            if (TeleportEnabled)
            {
                GetRoom().SendMessage(GetRoom().GetRoomItemHandler().UpdateUserOnRoller(this, new Point(pX, pY), 0, GetRoom().GetGameMap().SqAbsoluteHeight(GoalX, GoalY)));
                GetRoom().GetRoomUserManager().UpdateUserStatus(this, false);
                return;
            }

            IsWalking = true;
            GoalX = pX;
            GoalY = pY;
            throwBallAtGoal = false;
        }

        internal void MoveTo(int pX, int pY)
        {
            MoveTo(pX, pY, false);
        }

        internal void UnlockWalking()
        {
            this.AllowOverride = false;
            this.CanWalk = true;
        }

        internal void SetPos(int pX, int pY, double pZ)
        {
            this.X = pX;
            this.Y = pY;
            this.Z = pZ;
            if (isFlying)
                Z += 4 + 0.5 * Math.Sin(0.7 * flyk);
        }

        internal void CarryItem(int Item)
        {
            this.CarryItemID = Item;

            if (Item > 0)
            {
                this.CarryTimer = 240;
            }
            else
            {
                this.CarryTimer = 0;
            }

            ServerMessage Message = new ServerMessage(482);
            Message.AppendInt32(VirtualId);
            Message.AppendInt32(Item);
            GetRoom().SendMessage(Message);
        }


        internal void SetRot(int Rotation)
        {
            SetRot(Rotation, false); //**************
        }

        internal void SetRot(int Rotation, bool HeadOnly)
        {
            if (Statusses.ContainsKey("lay") || IsWalking)
            {
                return;
            }

            int diff = this.RotBody - Rotation;

            this.RotHead = this.RotBody;

            if (Statusses.ContainsKey("sit") || HeadOnly)
            {
                if (RotBody == 2 || RotBody == 4)
                {
                    if (diff > 0)
                    {
                        RotHead = RotBody - 1;
                    }
                    else if (diff < 0)
                    {
                        RotHead = RotBody + 1;
                    }
                }
                else if (RotBody == 0 || RotBody == 6)
                {
                    if (diff > 0)
                    {
                        RotHead = RotBody - 1;
                    }
                    else if (diff < 0)
                    {
                        RotHead = RotBody + 1;
                    }
                }
            }
            else if (diff <= -2 || diff >= 2)
            {
                this.RotHead = Rotation;
                this.RotBody = Rotation;
            }
            else
            {
                this.RotHead = Rotation;
            }

            this.UpdateNeeded = true;
        }

        internal void AddStatus(string Key, string Value)
        {
            Statusses[Key] = Value;
        }

        internal void RemoveStatus(string Key)
        {
            if (Statusses.ContainsKey(Key))
            {
                Statusses.Remove(Key);
            }
        }

        internal void ApplyEffect(int effectID)
        {
            if (IsBot || GetClient() == null || GetClient().GetHabbo() == null || GetClient().GetHabbo().GetAvatarEffectsInventoryComponent() == null)
                return;

            GetClient().GetHabbo().GetAvatarEffectsInventoryComponent().ApplyCustomEffect(effectID);
        }

        //internal void ResetStatus()
        //{
        //    Statusses = new Dictionary<string, string>();
        //}

        internal void Serialize(ServerMessage Message, bool gotPublicRoom)
        {
            // @\Ihqu@UMeth0d13haiihr-893-45.hd-180-8.ch-875-62.lg-280-62.sh-290-62.ca-1813-.he-1601-[IMRAPD4.0JImMcIrDK
            // MSadiePull up a pew and have a brew!hr-500-45.hd-600-1.ch-823-75.lg-716-76.sh-730-62.he-1602-75IRBPA2.0PAK

            if (Message == null)
                return;

            if (IsSpectator)
                return;

            if (IsBot)
            {
                Message.AppendInt32(BotAI.BaseId);
                Message.AppendStringWithBreak(BotData.Name);
                Message.AppendStringWithBreak(BotData.Motto);
                Message.AppendStringWithBreak(BotData.Look);
                Message.AppendInt32(VirtualId);
                Message.AppendInt32(X);
                Message.AppendInt32(Y);
                Message.AppendStringWithBreak(TextHandling.GetString(Z));
                Message.AppendInt32(4);
                Message.AppendInt32((BotData.AiType == AIType.Pet) ? 2 : 3);

                if (BotData.AiType == AIType.Pet)
                {
                    Message.AppendInt32(0);
                }
            }
            else if (!IsBot && GetClient() != null && GetClient().GetHabbo() != null)
            {
                Users.Habbo User = GetClient().GetHabbo();
                Message.AppendUInt(User.Id);
                Message.AppendStringWithBreak(User.Username);
                Message.AppendStringWithBreak(User.Motto);
                Message.AppendStringWithBreak(User.Look);
                Message.AppendInt32(VirtualId);
                Message.AppendInt32(X);
                Message.AppendInt32(Y);
                Message.AppendStringWithBreak(TextHandling.GetString(Z));
                Message.AppendInt32(0);
                Message.AppendInt32(1);
                Message.AppendStringWithBreak(User.Gender.ToLower());
                Message.AppendInt32(-1);
                if (User.userGroup != null)
                     Message.AppendInt32(User.userGroup.groupID);
                else
                    Message.AppendInt32(-1);
                Message.AppendInt32(-1);

                if (gotPublicRoom)
                    Message.AppendStringWithBreak("ch=s01/250,56,49");
                else
                    Message.AppendStringWithBreak(string.Empty);

                Message.AppendInt32(User.AchievementPoints);
            }
        }

        internal void SerializeStatus(ServerMessage Message)
        {
            if (IsSpectator)
            {
                return;
            }

            Message.AppendInt32(VirtualId);
            Message.AppendInt32(X);
            Message.AppendInt32(Y);
            Message.AppendStringWithBreak(TextHandling.GetString(Z));
            Message.AppendInt32(RotHead);
            Message.AppendInt32(RotBody);
            Message.AppendString("/");

            foreach (KeyValuePair<string, string> Status in Statusses)
            {
                Message.AppendString(Status.Key);

                if (Status.Value != string.Empty)
                {
                    Message.AppendString(" ");
                    Message.AppendString(Status.Value);
                }

                Message.AppendString("/");
            }

            Message.AppendStringWithBreak("/");
        }

        private GameClient mClient;
        internal GameClient GetClient()
        {
            if (IsBot)
            {
                return null;
            }
            if (mClient == null)
                mClient = ButterflyEnvironment.GetGame().GetClientManager().GetClientByUserID(HabboId);
            return mClient;
        }

        private Room mRoom;
        private Room GetRoom()
        {
            if (mRoom == null)
                mRoom = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(RoomId);
            return mRoom;
        }
    }

    internal enum ItemEffectType
    {
        None,
        Swim,
        SwimLow,
        SwimHalloween,
        Iceskates,
        Normalskates,
        PublicPool
        //Skateboard?
    }

    internal static class ByteToItemEffectEnum
    {
        internal static ItemEffectType Parse(byte pByte)
        {
            switch (pByte)
            {
                case 0:
                    return ItemEffectType.None;
                case 1:
                    return ItemEffectType.Swim;
                case 2:
                    return ItemEffectType.Normalskates;
                case 3:
                    return ItemEffectType.Iceskates;
                case 4:
                    return ItemEffectType.SwimLow;
                case 5:
                    return ItemEffectType.SwimHalloween;
                case 6:
                    return ItemEffectType.PublicPool;
                default:
                    return ItemEffectType.None;
            }
        }
    }
    //0 = none
    //1 = pool
    //2 = normal skates
    //3 = ice skates
}
