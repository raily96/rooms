﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Butterfly.Core;
using Butterfly.HabboHotel.Advertisements;
using Butterfly.HabboHotel.Catalogs;
using Butterfly.HabboHotel.GameClients;
using Butterfly.HabboHotel.Items;
using Butterfly.HabboHotel.Navigators;
using Butterfly.HabboHotel.Pathfinding;
using Butterfly.HabboHotel.Pets;
using Butterfly.HabboHotel.RoomBots;
using Butterfly.HabboHotel.Rooms;
using Butterfly.HabboHotel.Users.Badges;
using Butterfly.Collections;
using Database_Manager.Database.Session_Details.Interfaces;
using Butterfly.HabboHotel.Groups;
using System.Collections;
using Butterfly.HabboHotel.Rooms.Wired;
using System.Drawing;

namespace Butterfly.Messages
{
    partial class GameClientMessageHandler
    {
        internal void GetAdvertisement()
        {
            RoomAdvertisement Ad = ButterflyEnvironment.GetGame().GetAdvertisementManager().GetRandomRoomAdvertisement();

            Response.Init(258);

            if (Ad == null)
            {
                Response.AppendStringWithBreak("");
                Response.AppendStringWithBreak("");
            }
            else
            {
                Response.AppendStringWithBreak(Ad.AdImage);
                Response.AppendStringWithBreak(Ad.AdLink);

                Ad.OnView();
            }

            SendResponse();
        }

        //internal void GetTrainerPanel()
        //{
        //    uint PetID = Request.PopWiredUInt();
        //    Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);
        //    RoomUser PetUser = Room.GetPet(PetID);
        //    GetResponse().Init(605);
        //    GetResponse().AppendUInt(PetID);
        //    int level = PetUser.PetData.Level;
        //    GetResponse().AppendInt32(level);
        //    for (int i = 0; level > i; )
        //    {
        //        i++;
        //        GetResponse().AppendInt32(i - 1);
        //    }
        //    SendResponse();
        //}

        internal void GetTrainerPanel()
        {
            uint PetId = Request.PopWiredUInt();
            Pet PetData = null;

            Room Room = Session.GetHabbo().CurrentRoom;

            if (Room == null)
            {
                return;
            }

            if ((PetData = Room.GetRoomUserManager().GetPet(PetId).PetData) == null)
            {
                return;
            }
            else
            {
                int Level = PetData.Level;
                PetData = null;

                GetResponse().Init(605);
                GetResponse().AppendUInt(PetId);
                GetResponse().AppendInt32(18);

                GetResponse().AppendBoolean(false);

                for (int i = 0; i < 18; i++)
                {
                    GetResponse().AppendInt32(i);
                }

                GetResponse().AppendBoolean(false);

                for (int i = 0; i < Level; i++)
                {
                    GetResponse().AppendInt32(i);
                }

                SendResponse();
            }
        }
        internal void UpdateRoomData(UInt32 RoomId, string name, string desc, int state)
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(RoomId);


            int Id = Convert.ToInt32(RoomId);
            string Name = name;
            string Description = desc;
            int State = state;

            Room.Name = Name;
            Room.State = State;
            Room.Description = Description;


            Room.RoomData.Name = Name;
            Room.RoomData.State = State;
            Room.RoomData.Description = Description;


            string formattedState = "open";

            if (Room.State == 1)
                formattedState = "locked";
            else if (Room.State > 1)
                formattedState = "password";

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                dbClient.setQuery("UPDATE rooms SET caption = @caption, description = @description, state = '" + formattedState + "', WHERE id = " + Room.RoomId);
                dbClient.addParameter("caption", Room.Name);
                dbClient.addParameter("description", Room.Description);
                dbClient.runQuery();
            }

            GetResponse().Init(467);
            GetResponse().AppendUInt(Room.RoomId);
            SendResponse();

            GetResponse().Init(456);
            GetResponse().AppendUInt(Room.RoomId);
            SendResponse();

            GetResponse().Init(472);
            GetResponse().AppendBoolean(Room.Hidewall);
            GetResponse().AppendInt32(Room.WallThickness);
            GetResponse().AppendInt32(Room.FloorThickness);
            Room.SendMessage(GetResponse());

            RoomData Data = Room.RoomData;

            GetResponse().Init(454);
            GetResponse().AppendBoolean(false);

            Data.Serialize(GetResponse(), false);

            SendResponse();
        }
        internal void GetPub()
        {
            uint Id = Request.PopWiredUInt();

            RoomData Data = ButterflyEnvironment.GetGame().GetRoomManager().GenerateRoomData(Id);

            if (Data == null)
            {
                return;
            }


            GetResponse().Init(453);
            GetResponse().AppendUInt(Data.Id);
            GetResponse().AppendStringWithBreak(Data.CCTs);
            GetResponse().AppendUInt(Data.Id);
            SendResponse();
        }



        internal void OpenPub()
        {
            int Junk = Request.PopWiredInt32();
            uint Id = Request.PopWiredUInt();
            int Junk2 = Request.PopWiredInt32();

            RoomData Data = ButterflyEnvironment.GetGame().GetRoomManager().GenerateRoomData(Id);

            if (Data == null)
            {
                return;
            }

            PrepareRoomForUser(Data.Id, "");
        }

        internal void GetGroupBadges()
        {
            if (Session == null || Session.GetHabbo() == null || CurrentLoadingRoom == null)
                return;

            CurrentLoadingRoom.groups.QueueDelegate(new onCycleDoneDelegate(OnGroupSerialize));
        }

        internal void OnGroupSerialize()
        {
            if (Session == null || Session.GetHabbo() == null || CurrentLoadingRoom == null)
                return;

            Room room = CurrentLoadingRoom;

            Response.Init(309);
            Response.AppendInt32(room.groups.Inner.Count); // Count
            foreach (Group group in room.groups.Inner.Values)
            {
                Response.AppendInt32(group.groupID); // ID
                Response.AppendStringWithBreak(group.groupBadge); // Group badge
            }
            SendResponse();
        }

        internal void GetInventory()
        {
            QueuedServerMessage response = new QueuedServerMessage(Session.GetConnection());
            response.appendResponse(Session.GetHabbo().GetInventoryComponent().SerializeFloorItemInventory());
            response.appendResponse(Session.GetHabbo().GetInventoryComponent().SerializeWallItemInventory());
            response.sendResponse();
        }

        internal void GetRoomData1()
        {
            if (Session.GetHabbo().LoadingRoom <= 0)
            {
                return;
            }

            Response.Init(297);
            Response.AppendInt32(0);
            SendResponse();
        }

        internal void GetRoomData2()
        {
            try
            {
                QueuedServerMessage message = new QueuedServerMessage(Session.GetConnection());
                if (Session.GetHabbo().LoadingRoom <= 0 || CurrentLoadingRoom == null)
                    return;

                RoomData Data = CurrentLoadingRoom.RoomData;//ButterflyEnvironment.GetGame().GetRoomManager().GenerateRoomData(Session.GetHabbo().LoadingRoom);

                if (Data == null)
                {
                    return;
                }

                if (Data.Model == null)
                {
                    Session.SendNotif(LanguageLocale.GetValue("room.missingmodeldata"));
                    Session.SendMessage(new ServerMessage(18));
                    ClearRoomLoading();
                    return;
                }

                //CurrentLoadingRoom = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().LoadingRoom);

                message.appendResponse(CurrentLoadingRoom.GetGameMap().Model.GetHeightmap());
                message.appendResponse(CurrentLoadingRoom.GetGameMap().Model.SerializeRelativeHeightmap());
                message.sendResponse();
                //Session.SendMessage(CurrentLoadingRoom.Model.GetHeightmap());
                //Session.SendMessage(CurrentLoadingRoom.Model.SerializeRelativeHeightmap());

            }
            catch (Exception e)
            {
                Logging.LogException("Unable to load room ID [" + Session.GetHabbo().LoadingRoom + "] " + e.ToString());
                Session.SendNotif(LanguageLocale.GetValue("room.roomdataloaderror"));
            }

        }

        internal Room CurrentLoadingRoom;
        private int FloodCount;
        private DateTime FloodTime;
        internal void GetRoomData3()
        {
            if (Session.GetHabbo().LoadingRoom <= 0 || !Session.GetHabbo().LoadingChecksPassed || CurrentLoadingRoom == null)
            {
                return;
            }

            if (CurrentLoadingRoom.UsersNow + 1 > CurrentLoadingRoom.UsersMax && !Session.GetHabbo().HasFuse("fuse_enter_full_rooms"))
            {
                Session.SendNotif(LanguageLocale.GetValue("room.roomfull"));
                return;
            }
            
            ClearRoomLoading();

            QueuedServerMessage response = new QueuedServerMessage(Session.GetConnection());
            Response.Init(30);

            if (!string.IsNullOrEmpty(CurrentLoadingRoom.GetGameMap().StaticModel.StaticFurniMap))
            {
                Response.AppendStringWithBreak(CurrentLoadingRoom.GetGameMap().StaticModel.StaticFurniMap);
            }
            else
            {
                Response.AppendInt32(0);
            }

            response.appendResponse(GetResponse());
            //SendResponse();

            if (CurrentLoadingRoom.Type == "private")
            {
                RoomItem[] floorItems = CurrentLoadingRoom.GetRoomItemHandler().mFloorItems.Values.ToArray();
                RoomItem[] wallItems = CurrentLoadingRoom.GetRoomItemHandler().mWallItems.Values.ToArray();

                Response.Init(32);
                Response.AppendInt32(floorItems.Length);

                foreach (RoomItem Item in floorItems)
                    Item.Serialize(Response);

                response.appendResponse(GetResponse());

                Response.Init(45);
                Response.AppendInt32(wallItems.Length);

                foreach (RoomItem Item in wallItems)
                    Item.Serialize(Response);

                response.appendResponse(GetResponse());

                Array.Clear(floorItems, 0, floorItems.Length);
                Array.Clear(wallItems, 0, wallItems.Length);
                floorItems = null;
                wallItems = null;
            }

            response.sendResponse();
            CurrentLoadingRoom.GetRoomUserManager().AddUserToRoom(Session, Session.GetHabbo().SpectatorMode);
        }

        internal void RequestFloorItems()
        {

        }

        internal void RequestWallItems()
        {

        }

        internal void OnRoomUserAdd()
        {
            QueuedServerMessage response = new QueuedServerMessage(Session.GetConnection());

            List<RoomUser> UsersToDisplay = new List<RoomUser>();

            if (CurrentLoadingRoom == null)
                return;

            foreach (RoomUser User in CurrentLoadingRoom.GetRoomUserManager().UserList.Values)
            {
                if (User.IsSpectator)
                    continue;

                UsersToDisplay.Add(User);
            }

            Response.Init(28);
            Response.AppendInt32(UsersToDisplay.Count);

            foreach (RoomUser User in UsersToDisplay)
            {
                User.Serialize(Response, CurrentLoadingRoom.GetGameMap().gotPublicPool);
            }

            response.appendResponse(GetResponse());

            GetResponse().Init(472);
            GetResponse().AppendBoolean(CurrentLoadingRoom.Hidewall);
            GetResponse().AppendInt32(CurrentLoadingRoom.WallThickness);
            GetResponse().AppendInt32(CurrentLoadingRoom.FloorThickness);
            response.appendResponse(GetResponse());

            if (CurrentLoadingRoom.Type == "public")
            {
                Response.Init(471);
                Response.AppendBoolean(false);
                Response.AppendStringWithBreak(CurrentLoadingRoom.ModelName, "room modelname");
                Response.AppendBoolean(false);
                response.appendResponse(GetResponse());
            }
            else if (CurrentLoadingRoom.Type == "private")
            {
                Response.Init(471);
                Response.AppendBoolean(true);
                Response.AppendUInt(CurrentLoadingRoom.RoomId);

                if (CurrentLoadingRoom.CheckRights(Session, true))
                {
                    Response.AppendBoolean(true);
                }
                else
                {
                    Response.AppendBoolean(false);
                }

                response.appendResponse(GetResponse());

                // GQhntX]uberEmu PacketloggingDescriptionHQMSCQFJtag1tag2Ika^SMqurbIHH

                Response.Init(454);
                Response.AppendInt32(1);
                Response.AppendUInt(CurrentLoadingRoom.RoomId);
                Response.AppendInt32(0);
                Response.AppendStringWithBreak(CurrentLoadingRoom.Name);
                Response.AppendStringWithBreak(CurrentLoadingRoom.Owner);
                Response.AppendInt32(CurrentLoadingRoom.State);
                Response.AppendInt32(0);
                Response.AppendInt32(25);
                Response.AppendStringWithBreak(CurrentLoadingRoom.Description);
                Response.AppendInt32(0);
                Response.AppendInt32(1);
                Response.AppendInt32(8228);
                Response.AppendInt32(CurrentLoadingRoom.Category);
                Response.AppendStringWithBreak("");
                Response.AppendInt32(CurrentLoadingRoom.TagCount);

                foreach (string Tag in CurrentLoadingRoom.Tags.ToArray())
                {
                    Response.AppendStringWithBreak(Tag);
                }

                CurrentLoadingRoom.Icon.Serialize(Response);
                Response.AppendBoolean(false);
                response.appendResponse(GetResponse());
            }

            ServerMessage Updates = CurrentLoadingRoom.GetRoomUserManager().SerializeStatusUpdates(true);

            if (Updates != null)
            {
                //Session.SendMessage(Updates);
                response.appendResponse(Updates);
            }

            foreach (RoomUser User in CurrentLoadingRoom.GetRoomUserManager().UserList.Values)
            {
                if (User.IsSpectator)
                    continue;

                if (User.IsDancing)
                {
                    Response.Init(480);
                    Response.AppendInt32(User.VirtualId);
                    Response.AppendInt32(User.DanceId);
                    response.appendResponse(GetResponse());
                }

                if (User.IsAsleep)
                {
                    Response.Init(486);
                    Response.AppendInt32(User.VirtualId);
                    Response.AppendBoolean(true);
                    response.appendResponse(GetResponse());
                }

                if (User.CarryItemID > 0 && User.CarryTimer > 0)
                {
                    Response.Init(482);
                    Response.AppendInt32(User.VirtualId);
                    Response.AppendInt32(User.CarryTimer);
                    response.appendResponse(GetResponse());
                }

                if (!User.IsBot)
                {
                    try
                    {
                        if (User.GetClient() != null && User.GetClient().GetHabbo() != null && User.GetClient().GetHabbo().GetAvatarEffectsInventoryComponent() != null && User.GetClient().GetHabbo().GetAvatarEffectsInventoryComponent().CurrentEffect >= 1)
                        {
                            Response.Init(485);
                            Response.AppendInt32(User.VirtualId);
                            Response.AppendInt32(User.GetClient().GetHabbo().GetAvatarEffectsInventoryComponent().CurrentEffect);
                            response.appendResponse(GetResponse());
                        }
                    }
                    catch (Exception e) { Logging.HandleException(e, "Rooms.SendRoomData3"); }
                }
            }

            response.sendResponse();
            CurrentLoadingRoom = null;
        }

        internal void PrepareRoomForUser(uint Id, string Password)
        {
            ClearRoomLoading();
            QueuedServerMessage response = new QueuedServerMessage(Session.GetConnection());

            if (ButterflyEnvironment.ShutdownStarted)
            {
                Session.SendNotif(LanguageLocale.GetValue("shutdown.alert"));
                return;
            }

            if (Session.GetHabbo().InRoom)
            {
                Room OldRoom = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

                if (OldRoom != null)
                {
                    OldRoom.GetRoomUserManager().RemoveUserFromRoom(Session, false, false);
                    Session.CurrentRoomUserID = -1;
                }
            }

            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().LoadRoom(Id);

            if (Room == null || Session == null || Session.GetHabbo() == null)
                return;

            if (Session.GetHabbo().IsTeleporting && Session.GetHabbo().TeleportingRoomID != Id)
                return;

            Session.GetHabbo().LoadingRoom = Id;
            CurrentLoadingRoom = Room;


            if (!Session.GetHabbo().HasFuse("fuse_enter_any_room") && Room.UserIsBanned(Session.GetHabbo().Id))
            {
                if (Room.HasBanExpired(Session.GetHabbo().Id))
                {
                    Room.RemoveBan(Session.GetHabbo().Id);
                }
                else
                {
                    // C`PA
                    Response.Init(224);
                    Response.AppendInt32(4);
                    //SendResponse();//******
                    response.appendResponse(GetResponse());

                    Response.Init(18);
                    //SendResponse();//******
                    response.appendResponse(GetResponse());

                    response.sendResponse();
                    return;
                }
            }

            if (Room.UsersNow >= Room.UsersMax && !Session.GetHabbo().HasFuse("fuse_enter_full_rooms"))
            {
                if (!ButterflyEnvironment.GetGame().GetRoleManager().RankHasRight(Session.GetHabbo().Rank, "fuse_enter_full_rooms"))
                {
                    Response.Init(224);
                    Response.AppendInt32(1);
                    //SendResponse();//******
                    response.appendResponse(GetResponse());

                    Response.Init(18);
                    //SendResponse();//******
                    response.appendResponse(GetResponse());

                    response.sendResponse();
                    return;
                }
            }

            if (Room.Type == "public")
            {
                if (Room.State > 0 && !Session.GetHabbo().HasFuse("fuse_mod"))
                {
                    Session.SendNotif(LanguageLocale.GetValue("room.noaccess"));

                    Response.Init(18);
                    //SendResponse();//******
                    response.appendResponse(GetResponse());

                    response.sendResponse();
                    return;
                }

                Response.Init(166);
                Response.AppendStringWithBreak("/client/public/" + Room.ModelName + "/0");
                //SendResponse();//******
                response.appendResponse(GetResponse());
            }
            else if (Room.Type == "private")
            {
                Response.Init(19);
                //SendResponse();//******
                response.appendResponse(GetResponse());

                if (!Session.GetHabbo().HasFuse("fuse_enter_any_room") && !Room.CheckRights(Session, true) && !Session.GetHabbo().IsTeleporting)
                {
                    if (Room.State == 1)
                    {
                        if (Room.UserCount == 0)
                        {
                            Response.Init(131);
                            //SendResponse();//******
                            response.appendResponse(GetResponse());
                        }
                        else
                        {
                            Response.Init(91);
                            Response.AppendStringWithBreak("");
                            //SendResponse();//******
                            response.appendResponse(GetResponse());

                            ServerMessage RingMessage = new ServerMessage(91);
                            RingMessage.AppendStringWithBreak(Session.GetHabbo().Username);
                            Room.SendMessageToUsersWithRights(RingMessage);
                        }

                        response.sendResponse();

                        return; 
                    }
                    else if (Room.State == 2)
                    {
                        if (Password.ToLower() != Room.Password.ToLower())
                        {
                            Response.Init(33);
                            Response.AppendInt32(-100002);
                            //SendResponse();//******
                            response.appendResponse(GetResponse());

                            Response.Init(18);
                            //SendResponse();//******
                            response.appendResponse(GetResponse());
                            
                            response.sendResponse();
                            return;
                        }
                    }
                }

                Response.Init(166);
                Response.AppendStringWithBreak("/client/internal/" + Room.RoomId + "/id");
                //SendResponse(); //******
                response.appendResponse(GetResponse());
            }

            Session.GetHabbo().LoadingChecksPassed = true;

            response.addBytes(LoadRoomForUser().getPacket);
            //LoadRoomForUser();
            response.sendResponse();
        }

        internal void ReqLoadRoomForUser()
        {
            LoadRoomForUser().sendResponse();
        }

        internal QueuedServerMessage LoadRoomForUser()
        {
            //Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().LoadingRoom);
            Room Room = CurrentLoadingRoom;

            QueuedServerMessage response = new QueuedServerMessage(Session.GetConnection());

            if (Room == null || !Session.GetHabbo().LoadingChecksPassed)
                return response;


            // todo: Room.SerializeGroupBadges()
            Response.Init(309);
            Response.AppendInt32(1); //yes or no i guess
            Response.AppendInt32(122768); // group iD
            Response.AppendStringWithBreak("b1201Xs03097s55091s17094a7a396e8d44670744d87bf913858b1fc");

            Response.Init(69);
            Response.AppendStringWithBreak(Room.ModelName);
            Response.AppendUInt(Room.RoomId);
            response.appendResponse(GetResponse());

            if (Session.GetHabbo().SpectatorMode)
            {
                Response.Init(254);
                response.appendResponse(GetResponse());
            }

            if (Room.Type == "private")
            {
                if (Room.Wallpaper != "0.0")
                {
                    Response.Init(46);
                    Response.AppendStringWithBreak("wallpaper");
                    Response.AppendStringWithBreak(Room.Wallpaper);
                    response.appendResponse(GetResponse());
                }

                if (Room.Floor != "0.0")
                {
                    Response.Init(46);
                    Response.AppendStringWithBreak("floor");
                    Response.AppendStringWithBreak(Room.Floor);
                    response.appendResponse(GetResponse());
                }

                Response.Init(46);
                Response.AppendStringWithBreak("landscape");
                Response.AppendStringWithBreak(Room.Landscape);
                response.appendResponse(GetResponse());

                if (Room.CheckRights(Session, true))
                {
                    Response.Init(42);
                    response.appendResponse(GetResponse());

                    Response.Init(47);
                    response.appendResponse(GetResponse());
                }
                else if (Room.CheckRights(Session))
                {
                    Response.Init(42);
                    response.appendResponse(GetResponse());
                }

                Response.Init(345);

                if (Session.GetHabbo().RatedRooms.Contains(Room.RoomId) || Room.CheckRights(Session, true))
                {
                    Response.AppendInt32(Room.Score);
                }
                else
                {
                    Response.AppendInt32(-1);
                }

                response.appendResponse(GetResponse());

                if (Room.HasOngoingEvent)
                {
                    Session.SendMessage(Room.Event.Serialize(Session));
                }
                else
                {
                    Response.Init(370);
                    Response.AppendStringWithBreak("-1");
                    response.appendResponse(GetResponse());
                }
            }

            //response.sendResponse();
            return response;
        }

        internal void ClearRoomLoading()
        {
            Session.GetHabbo().LoadingRoom = 0;
            Session.GetHabbo().LoadingChecksPassed = false;
        }

        internal void Talk()
        {
            if (ButterflyEnvironment.SystemMute)
                return;
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null)
            {
                return;
            }

            RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(Session.GetHabbo().Id);

            if (User == null)
            {
                return;
            }

            User.Chat(Session, ButterflyEnvironment.FilterInjectionChars(Request.PopFixedString()), false);
        }

        internal void Shout()
        {
            if (ButterflyEnvironment.SystemMute)
                return;
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null)
            {
                return;
            }

            RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(Session.GetHabbo().Id);

            if (User == null)
            {
                return;
            }

            User.Chat(Session, ButterflyEnvironment.FilterInjectionChars(Request.PopFixedString()), true);
        }

        internal void Whisper()
        {
            if (ButterflyEnvironment.SystemMute)
                return;

            if (Session == null || Session.GetHabbo() == null)
                return;

            Room Room = Session.GetHabbo().CurrentRoom;
            
            if (Room == null)
            {
                return;
            }

            if (Session.GetHabbo().Muted)
            {
                Session.SendNotif(LanguageLocale.GetValue("user.ismuted"));
                return;
            }

            if (Room.RoomMuted)
                return;

            string Params = ButterflyEnvironment.FilterInjectionChars(Request.PopFixedString());
            string ToUser = Params.Split(' ')[0];
            string Message = Params.Substring(ToUser.Length + 1);
            Message = LanguageLocale.FilterSwearwords(Message);
            RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(Session.GetHabbo().Id);
            RoomUser User2 = Room.GetRoomUserManager().GetRoomUserByHabbo(ToUser);

            ServerMessage TellMsg = new ServerMessage();
            TellMsg.Init(25);
            TellMsg.AppendInt32(User.VirtualId);
            TellMsg.AppendStringWithBreak(Message);
            TellMsg.AppendBoolean(false);

            if (User != null && !User.IsBot)
            {
                User.GetClient().SendMessage(TellMsg);
            }

            User.Unidle();

            if (User2 != null && !User2.IsBot)
            {
                User2.GetClient().SendMessage(TellMsg);
            }
            TimeSpan SinceLastMessage = DateTime.Now - FloodTime;
            if (SinceLastMessage.Seconds > 4)
                FloodCount = 0;

            if (SinceLastMessage.Seconds < 4 && FloodCount > 5 && Session.GetHabbo().Rank < 5)
            {
                ServerMessage Packet = new ServerMessage(27);
                Packet.AppendInt32(30); //Blocked for 30sec
                User.GetClient().SendMessage(Packet);
                return;
            }
            FloodTime = DateTime.Now;
            FloodCount++;


            List<RoomUser> ToNotify = Room.GetRoomUserManager().GetRoomUserByRank(4);
            if (ToNotify.Count > 0)
            {
                ServerMessage NotifyMessage = new ServerMessage();
                NotifyMessage.Init(25);
                NotifyMessage.AppendInt32(User.VirtualId);
                NotifyMessage.AppendStringWithBreak(LanguageLocale.GetValue("moderation.whisper") + ToUser + ": " + Message);
                NotifyMessage.AppendBoolean(false);

                foreach (RoomUser user in ToNotify)
                    if (user != null)
                        if (user.HabboId != User2.HabboId && user.HabboId != User.HabboId)
                            if (user.GetClient() != null)
                                user.GetClient().SendMessage(NotifyMessage);
            }
        }

        internal void Move()
        {
            Room Room = Session.GetHabbo().CurrentRoom;

            if (Room == null)
            {
                return;
            }

            RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(Session.GetHabbo().Id);

            if (User == null || !User.CanWalk)
            {
                return;
            }

            int MoveX = Request.PopWiredInt32();
            int MoveY = Request.PopWiredInt32();

            if (MoveX == User.X && MoveY == User.Y)
            {
                return;
            }

            User.MoveTo(MoveX, MoveY);
        }

        internal void CanCreateRoom()
        {
            Response.Init(512);
            Response.AppendBoolean(false); // true = show error with number below
            Response.AppendInt32(99999);
            SendResponse();

            // todo: room limit
        }

        internal void CreateRoom()
        {
            string RoomName = ButterflyEnvironment.FilterInjectionChars(Request.PopFixedString());
            string ModelName = Request.PopFixedString();
            string RoomState = Request.PopFixedString(); // unused, room open by default on creation. may be added in later build of Habbo?

            RoomData NewRoom = ButterflyEnvironment.GetGame().GetRoomManager().CreateRoom(Session, RoomName, ModelName);

            if (NewRoom != null)
            {
                Response.Init(59);
                Response.AppendUInt(NewRoom.Id);
                Response.AppendStringWithBreak(NewRoom.Name);
                SendResponse();
            }
        }

        internal void GetRoomEditData()//ill debug it, second,
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true))
            {
                return;
            }

            GetResponse().Init(465);
            GetResponse().AppendUInt(Room.RoomId);
            GetResponse().AppendStringWithBreak(Room.Name);
            GetResponse().AppendStringWithBreak(Room.Description);
            GetResponse().AppendInt32(Room.State);
            GetResponse().AppendInt32(Room.Category);
            GetResponse().AppendInt32(Room.UsersMax);
            GetResponse().AppendInt32(25);
            GetResponse().AppendInt32(Room.TagCount);

            foreach (string Tag in Room.Tags.ToArray())
            {
                GetResponse().AppendStringWithBreak(Tag);
            }

            GetResponse().AppendInt32(Room.UsersWithRights.Count); // users /w rights count

            foreach (uint UserId in Room.UsersWithRights)
            {
                GetResponse().AppendUInt(UserId);
                GetResponse().AppendStringWithBreak(ButterflyEnvironment.GetGame().GetClientManager().GetNameById(UserId));
            }

            GetResponse().AppendInt32(Room.UsersWithRights.Count); // users /w rights count

            GetResponse().AppendBoolean(Room.AllowPets); // allows pets in room - pet system lacking, so always off
            GetResponse().AppendBoolean(Room.AllowPetsEating); // allows pets to eat your food - pet system lacking, so always off
            GetResponse().AppendBoolean(Room.AllowWalkthrough);
            GetResponse().AppendBoolean(Room.Hidewall);
            GetResponse().AppendInt32(Room.WallThickness);
            GetResponse().AppendInt32(Room.FloorThickness);

            SendResponse();
        }

        internal void SaveRoomIcon()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true))
            {
                return;
            }//that is icon =D

            int Junk = Request.PopWiredInt32(); // always 3

            Dictionary<int, int> Items = new Dictionary<int, int>();

            int Background = Request.PopWiredInt32();
            int TopLayer = Request.PopWiredInt32();
            int AmountOfItems = Request.PopWiredInt32();

            for (int i = 0; i < AmountOfItems; i++)
            {
                int Pos = Request.PopWiredInt32();
                int Item = Request.PopWiredInt32();

                if (Pos < 0 || Pos > 10)
                {
                    return;
                }

                if (Item < 1 || Item > 27)
                {
                    return;
                }

                if (Items.ContainsKey(Pos))
                {
                    return;
                }

                Items.Add(Pos, Item);
            }

            if (Background < 1 || Background > 24)
            {
                return;
            }

            if (TopLayer < 0 || TopLayer > 11)
            {
                return;
            }

            StringBuilder FormattedItems = new StringBuilder();
            int j = 0;

            foreach (KeyValuePair<int, int> Item in Items)
            {
                if (j > 0)
                {
                    FormattedItems.Append("|");
                }

                FormattedItems.Append(Item.Key + "," + Item.Value);

                j++;
            }

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                dbClient.setQuery("UPDATE rooms SET icon_bg = " + Background + ", icon_fg = " + TopLayer + ", icon_items = @item WHERE id = " + Room.RoomId + "");
                dbClient.addParameter("item", FormattedItems.ToString());
                dbClient.runQuery();
            }

            Room.Icon = new RoomIcon(Background, TopLayer, Items);

            Response.Init(457);
            Response.AppendUInt(Room.RoomId);
            Response.AppendBoolean(true);
            SendResponse();

            Response.Init(456);
            Response.AppendUInt(Room.RoomId);
            SendResponse();

            RoomData Data = Room.RoomData;

            Response.Init(454);
            Response.AppendBoolean(false);
            Data.Serialize(Response, false);
            SendResponse();
        }

        internal void SaveRoomData()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true))
            {
                return;
            }

            int Id = Request.PopWiredInt32();
            string Name = ButterflyEnvironment.FilterInjectionChars(Request.PopFixedString());
            string Description = ButterflyEnvironment.FilterInjectionChars(Request.PopFixedString());
            int State = Request.PopWiredInt32();
            string Password = ButterflyEnvironment.FilterInjectionChars(Request.PopFixedString());
            int MaxUsers = Request.PopWiredInt32();
            int CategoryId = Request.PopWiredInt32();
            int TagCount = Request.PopWiredInt32();

            List<string> Tags = new List<string>();
            StringBuilder formattedTags = new StringBuilder();

            for (int i = 0; i < TagCount; i++)
            {
                if (i > 0)
                {
                    formattedTags.Append(",");
                }

                string tag = ButterflyEnvironment.FilterInjectionChars(Request.PopFixedString().ToLower());

                Tags.Add(tag);
                formattedTags.Append(tag);
            }


            bool AllowPets = (Request.ReadBytes(1)[0] == 65);
            bool AllowPetsEat = (Request.ReadBytes(1)[0] == 65);
            bool AllowWalkthrough = (Request.ReadBytes(1)[0] == 65);
            bool Hidewall = (Request.ReadBytes(1)[0] == 65);
            int WallThickness = Request.PopWiredInt32();
            int FloorThickness = Request.PopWiredInt32();

            if (WallThickness < -2 || WallThickness > 1)
            {
                WallThickness = 0;
            }

            if (FloorThickness < -2 || FloorThickness > 1)
            {
                FloorThickness = 0;
            }

            if (Name.Length < 1)
            {
                return;
            }

            if (State < 0 || State > 2)
            {
                return;
            }

            if (MaxUsers != 10 && MaxUsers != 15 && MaxUsers != 20 && MaxUsers != 25)
            {
                return;
            }

            FlatCat FlatCat = ButterflyEnvironment.GetGame().GetNavigator().GetFlatCat(CategoryId);

            if (FlatCat == null)
            {
                return;
            }

            if (FlatCat.MinRank > Session.GetHabbo().Rank)
            {
                Session.SendNotif(LanguageLocale.GetValue("user.roomdata.rightserror"));
                CategoryId = 0;
            }

            if (TagCount > 2)
            {
                return;
            }

            Room.AllowPets = AllowPets;
            Room.AllowPetsEating = AllowPetsEat;
            Room.AllowWalkthrough = AllowWalkthrough;
            Room.Hidewall = Hidewall;

            Room.Name = Name;
            Room.State = State;
            Room.Description = Description;
            Room.Category = CategoryId;
            Room.Password = Password;

            Room.RoomData.Name = Name;
            Room.RoomData.State = State;
            Room.RoomData.Description = Description;
            Room.RoomData.Category = CategoryId;
            Room.RoomData.Password = Password;

            Room.ClearTags();
            Room.AddTagRange(Tags);
            Room.UsersMax = MaxUsers;

            Room.RoomData.Tags.Clear();
            Room.RoomData.Tags.AddRange(Tags);
            Room.RoomData.UsersMax = MaxUsers;
            Room.WallThickness = WallThickness;
            Room.FloorThickness = FloorThickness;

            string formattedState = "open";

            if (Room.State == 1)
                formattedState = "locked";
            else if (Room.State > 1)
                formattedState = "password";

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                dbClient.setQuery("UPDATE rooms SET caption = @caption, description = @description, password = @password, category = " + CategoryId + ", state = '" + formattedState + "', tags = @tags, users_max = " + MaxUsers + ", allow_pets = " + TextHandling.BooleanToInt(AllowPets) + ", allow_pets_eat = " + TextHandling.BooleanToInt(AllowPetsEat) + ", allow_walkthrough = " + TextHandling.BooleanToInt(AllowWalkthrough) + ", allow_hidewall = " + TextHandling.BooleanToInt(Hidewall) + " WHERE id = " + Room.RoomId);
                dbClient.addParameter("caption", Room.Name);
                dbClient.addParameter("description", Room.Description);
                dbClient.addParameter("password", Room.Password);
                dbClient.addParameter("tags", formattedTags.ToString());
                dbClient.runQuery();
            }

            GetResponse().Init(467);
            GetResponse().AppendUInt(Room.RoomId);
            SendResponse();

            GetResponse().Init(456);
            GetResponse().AppendUInt(Room.RoomId);
            SendResponse();

            GetResponse().Init(472);
            GetResponse().AppendBoolean(Room.Hidewall);
            GetResponse().AppendInt32(Room.WallThickness);
            GetResponse().AppendInt32(Room.FloorThickness);
            Session.GetHabbo().CurrentRoom.SendMessage(GetResponse());

            RoomData Data = Room.RoomData;

            GetResponse().Init(454);
            GetResponse().AppendBoolean(false);

            Data.Serialize(GetResponse(), false);

            SendResponse();
        }

        internal void GiveRights()
        {
            uint UserId = Request.PopWiredUInt();

            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);
            if (Room == null)
                return;

            RoomUser RoomUser = Room.GetRoomUserManager().GetRoomUserByHabbo(UserId);

            if (Room == null || !Room.CheckRights(Session, true) || RoomUser == null || RoomUser.IsBot)
            {
                return;
            }

            if (Room.UsersWithRights.Contains(UserId))
            {
                Session.SendNotif(LanguageLocale.GetValue("user.giverights.error"));
                return;
            }

            Room.UsersWithRights.Add(UserId);

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                dbClient.runFastQuery("INSERT INTO room_rights (room_id,user_id) VALUES (" + Room.RoomId + "," + UserId + ")");
            }

            Response.Init(510);
            Response.AppendUInt(Room.RoomId);
            Response.AppendUInt(UserId);
            Response.AppendStringWithBreak(RoomUser.GetClient().GetHabbo().Username);
            SendResponse();

            RoomUser.AddStatus("flatcrtl", "");
            RoomUser.UpdateNeeded = true;

            RoomUser.GetClient().SendMessage(new ServerMessage(42));
        }

        internal void TakeRights()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true))
            {
                return;
            }

            StringBuilder DeleteParams = new StringBuilder();

            int Amount = Request.PopWiredInt32();

            for (int i = 0; i < Amount; i++)
            {
                if (i > 0)
                {
                    DeleteParams.Append(" OR ");
                }

                uint UserId = Request.PopWiredUInt();
                Room.UsersWithRights.Remove(UserId);
                DeleteParams.Append("room_id = '" + Room.RoomId + "' AND user_id = '" + UserId + "'");

                RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(UserId);

                if (User != null && !User.IsBot)
                {
                    User.GetClient().SendMessage(new ServerMessage(43));
                }

                // GhntX]hqu@U
                Response.Init(511);
                Response.AppendUInt(Room.RoomId);
                Response.AppendUInt(UserId);
                SendResponse();
            }

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                dbClient.runFastQuery("DELETE FROM room_rights WHERE " + DeleteParams.ToString());
            }
        }

        internal void TakeAllRights()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true))
            {
                return;
            }

            foreach (uint UserId in Room.UsersWithRights)
            {
                RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(UserId);

                if (User != null && !User.IsBot)
                {
                    User.GetClient().SendMessage(new ServerMessage(43));
                }

                Response.Init(511);
                Response.AppendUInt(Room.RoomId);
                Response.AppendUInt(UserId);
                SendResponse();
            }

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                dbClient.runFastQuery("DELETE FROM room_rights WHERE room_id = " + Room.RoomId);
            }

            Room.UsersWithRights.Clear();
        }

        internal void KickUser()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null)
            {
                return;
            }

            if (!Room.CheckRights(Session))
            {
                return; // insufficient permissions
            }

            uint UserId = Request.PopWiredUInt();
            RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(UserId);

            if (User == null || User.IsBot)
            {
                return;
            }

            if (Room.CheckRights(User.GetClient(), true) || User.GetClient().GetHabbo().HasFuse("fuse_mod") || User.GetClient().GetHabbo().HasFuse("fuse_no_kick"))
            {
                return; // can't kick room owner or mods!
            }

            Room.GetRoomUserManager().RemoveUserFromRoom(User.GetClient(), true, true);
            User.GetClient().CurrentRoomUserID = -1;
        }

        internal void BanUser()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true))
            {
                return; // insufficient permissions
            }

            uint UserId = Request.PopWiredUInt();
            RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(UserId);

            if (User == null || User.IsBot)
            {
                return;
            }

            if (User.GetClient().GetHabbo().HasFuse("fuse_mod") || User.GetClient().GetHabbo().HasFuse("fuse_no_kick"))
            {
                return;
            }

            Room.AddBan(UserId);
            Room.GetRoomUserManager().RemoveUserFromRoom(User.GetClient(), true, true);

            Session.CurrentRoomUserID = -1;
        }

        internal void SetHomeRoom()
        {
            uint RoomId = Request.PopWiredUInt();
            RoomData Data = ButterflyEnvironment.GetGame().GetRoomManager().GenerateRoomData(RoomId);

            if (RoomId != 0)
            {
                if (Data == null || Data.Owner.ToLower() != Session.GetHabbo().Username.ToLower())
                {
                    return;
                }
            }

            Session.GetHabbo().HomeRoom = RoomId;

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                dbClient.runFastQuery("UPDATE users SET home_room = " + RoomId + " WHERE id = " + Session.GetHabbo().Id);
            }

            Response.Init(455);
            Response.AppendUInt(RoomId);
            SendResponse();
        }

        internal void DeleteRoom()
        {
            uint RoomId = Request.PopWiredUInt();
            if (Session == null || Session.GetHabbo() == null || Session.GetHabbo().UsersRooms == null)
                return;
            
            //TargetRoom = Session.GetHabbo().CurrentRoom; ;
            Room TargetRoom = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(RoomId);
            if (TargetRoom == null)
                return;
            if (TargetRoom.Owner == Session.GetHabbo().Username || Session.GetHabbo().Rank > 6)
            {

                if (this.Session.GetHabbo().GetInventoryComponent() != null)
                {
                    this.Session.GetHabbo().GetInventoryComponent().AddItemArray(TargetRoom.GetRoomItemHandler().RemoveAllFurniture(Session));
                }

                RoomData data = TargetRoom.RoomData;
                ButterflyEnvironment.GetGame().GetRoomManager().UnloadRoom(TargetRoom);
                ButterflyEnvironment.GetGame().GetRoomManager().QueueVoteRemove(data);

                using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
                {
                    dbClient.runFastQuery("DELETE FROM rooms WHERE id = " + RoomId);
                    dbClient.runFastQuery("DELETE FROM user_favorites WHERE room_id = " + RoomId);
                    dbClient.runFastQuery("DELETE items, items_extradata, items_rooms "+
                                            "FROM items_rooms "+
                                            "INNER JOIN items ON (items.item_id = items_rooms.item_id) "+
                                            "LEFT JOIN items_extradata ON (items_extradata.item_id = items.item_id) "+
                                            "WHERE items_rooms.room_id = " + RoomId);
                    dbClient.runFastQuery("DELETE FROM room_rights WHERE room_id = " + RoomId);
                    dbClient.runFastQuery("UPDATE users SET home_room = '0' WHERE home_room = " + RoomId);
                }

                if (Session.GetHabbo().Rank > 5 && Session.GetHabbo().Username != data.Owner)
                {
                    ButterflyEnvironment.GetGame().GetModerationTool().LogStaffEntry(Session.GetHabbo().Username, data.Name, "Room deletion", string.Format("Deleted room ID {0}", data.Id));
                }

                RoomData removedRoom = (from p in Session.GetHabbo().UsersRooms
                                        where p.Id == RoomId
                                        select p).SingleOrDefault();
                if (removedRoom != null)
                    Session.GetHabbo().UsersRooms.Remove(removedRoom);

                
            }
        }

        internal void LookAt()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null)
            {
                return;
            }

            RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(Session.GetHabbo().Id);

            if (User == null)
            {
                return;
            }

            User.Unidle();

            int X = Request.PopWiredInt32();
            int Y = Request.PopWiredInt32();

            if (X == User.X && Y == User.Y)
            {
                return;
            }

            int Rot = Rotation.Calculate(User.X, User.Y, X, Y);

            User.SetRot(Rot, false);
        }

        internal void StartTyping()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null)
            {
                return;
            }

            RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(Session.GetHabbo().Id);

            if (User == null)
            {
                return;
            }

            ServerMessage Message = new ServerMessage(361);
            Message.AppendInt32(User.VirtualId);
            Message.AppendBoolean(true);
            Room.SendMessage(Message);
        }

        internal void StopTyping()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null)
            {
                return;
            }

            RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(Session.GetHabbo().Id);

            if (User == null)
            {
                return;
            }

            ServerMessage Message = new ServerMessage(361);
            Message.AppendInt32(User.VirtualId);
            Message.AppendBoolean(false);
            Room.SendMessage(Message);
        }

        internal void IgnoreUser()
        {

            Room Room = Session.GetHabbo().CurrentRoom;

            if (Room == null)
                return;

            uint Id = Request.PopWiredUInt();

            if (Session.GetHabbo().MutedUsers.Contains(Id))
                return;

            Session.GetHabbo().MutedUsers.Add(Id);

            Response.Init(419);
            Response.AppendInt32(1);
            SendResponse();
        }

        internal void UnignoreUser()
        {

            Room Room = Session.GetHabbo().CurrentRoom;

            if (Room == null)
                return;

            uint Id = Request.PopWiredUInt();

            if (!Session.GetHabbo().MutedUsers.Contains(Id))
                return;

            Session.GetHabbo().MutedUsers.Remove(Id);

            Response.Init(419);
            Response.AppendInt32(3);
            SendResponse();
        }

        internal void CanCreateRoomEvent()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true))
            {
                return;
            }

            Boolean Allow = true;
            int ErrorCode = 0;

            if (Room.State != 0)
            {
                Allow = false;
                ErrorCode = 3;
            }

            Response.Init(367);
            Response.AppendBoolean(Allow);
            Response.AppendInt32(ErrorCode);
            SendResponse();
        }

        internal void StartEvent()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true) || Room.Event != null || Room.State != 0)
            {
                return;
            }

            int category = Request.PopWiredInt32();
            string name = ButterflyEnvironment.FilterInjectionChars(Request.PopFixedString());
            string descr = ButterflyEnvironment.FilterInjectionChars(Request.PopFixedString());
            int tagCount = Request.PopWiredInt32();

            Room.Event = new RoomEvent(Room.RoomId, name, descr, category, null);
            Room.Event.Tags = new List<string>();


            for (int i = 0; i < tagCount; i++)
            {
                Room.Event.Tags.Add(ButterflyEnvironment.FilterInjectionChars(Request.PopFixedString()));
            }

            Room.SendMessage(Room.Event.Serialize(Session));
        }

        internal void StopEvent()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true) || Room.Event == null)
            {
                return;
            }

            //Room.Event = null;

            ServerMessage Message = new ServerMessage(370);
            Message.AppendStringWithBreak("-1");
            Room.SendMessage(Message);

            ButterflyEnvironment.GetGame().GetRoomManager().GetEventManager().QueueRemoveEvent(Room.RoomData, Room.Event.Category);
        }

        internal void EditEvent()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true) || Room.Event == null)
            {
                return;
            }

            int category = Request.PopWiredInt32();
            string name = ButterflyEnvironment.FilterInjectionChars(Request.PopFixedString());
            string descr = ButterflyEnvironment.FilterInjectionChars(Request.PopFixedString());
            int tagCount = Request.PopWiredInt32();

            Room.Event.Category = category;
            Room.Event.Name = name;
            Room.Event.Description = descr;
            Room.Event.Tags = new List<string>();

            ButterflyEnvironment.GetGame().GetRoomManager().GetEventManager().QueueUpdateEvent(Room.RoomData, Room.Event.Category);

            for (int i = 0; i < tagCount; i++)
            {
                Room.Event.Tags.Add(ButterflyEnvironment.FilterInjectionChars(Request.PopFixedString()));
            }

            Room.SendMessage(Room.Event.Serialize(Session));
        }

        internal void Wave()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null)
            {
                return;
            }

            RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(Session.GetHabbo().Id);

            if (User == null)
            {
                return;
            }

            User.Unidle();

            User.DanceId = 0;

            ServerMessage Message = new ServerMessage(481);
            Message.AppendInt32(User.VirtualId);
            Room.SendMessage(Message);

            ButterflyEnvironment.GetGame().GetQuestManager().ProgressUserQuest(Session, HabboHotel.Quests.QuestType.SOCIAL_WAVE);
        }

        internal void GetUserTags()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null)
            {
                return;
            }

            RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(Request.PopWiredUInt());

            if (User == null || User.IsBot)
            {
                return;
            }

            Response.Init(350);
            Response.AppendUInt(User.GetClient().GetHabbo().Id);
            Response.AppendInt32(User.GetClient().GetHabbo().Tags.Count);

            foreach (string Tag in User.GetClient().GetHabbo().Tags)
            {
                Response.AppendStringWithBreak(Tag);
            }

            SendResponse();
        }

        internal void GetUserBadges()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null)
            {
                return;
            }

            RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(Request.PopWiredUInt());

            if (User == null || User.IsBot)
                return;
            if (User.GetClient() == null)
                return;

            // CdjUzYZJIACH_RespectEarned1JACH_EmailVerification1E^jUzYZH

            Response.Init(228);
            Response.AppendUInt(User.GetClient().GetHabbo().Id);
            Response.AppendInt32(User.GetClient().GetHabbo().GetBadgeComponent().EquippedCount);

            foreach (Badge Badge in User.GetClient().GetHabbo().GetBadgeComponent().BadgeList.Values)
            {
                if (Badge.Slot <= 0)
                {
                    continue;
                }

                Response.AppendInt32(Badge.Slot);
                Response.AppendStringWithBreak(Badge.Code);
            }

            SendResponse();
        }

        internal void RateRoom()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || Session.GetHabbo().RatedRooms.Contains(Room.RoomId) || Room.CheckRights(Session, true))
            {
                return;
            }

            int Rating = Request.PopWiredInt32();

            switch (Rating)
            {
                case -1:

                    Room.Score--;
                    Room.RoomData.Score--;
                    break;

                case 1:

                    Room.Score++;
                    Room.RoomData.Score++;
                    break;

                default:

                    return;
            }

            ButterflyEnvironment.GetGame().GetRoomManager().QueueVoteAdd(Room.RoomData);

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                dbClient.runFastQuery("UPDATE rooms SET score = " + Room.Score + " WHERE id = " + Room.RoomId);
            }

            Session.GetHabbo().RatedRooms.Add(Room.RoomId);

            Response.Init(345);
            Response.AppendInt32(Room.Score);
            SendResponse();
        }

        internal void Dance()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null)
            {
                return;
            }

            RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(Session.GetHabbo().Id);

            if (User == null)
            {
                return;
            }

            User.Unidle();

            int DanceId = Request.PopWiredInt32();

            if (DanceId < 0 || DanceId > 4 || (!Session.GetHabbo().HasFuse("fuse_use_club_dance") && DanceId > 1))
            {
                DanceId = 0;
            }

            if (DanceId > 0 && User.CarryItemID > 0)
            {
                User.CarryItem(0);
            }

            User.DanceId = DanceId;

            ServerMessage DanceMessage = new ServerMessage(480);
            DanceMessage.AppendInt32(User.VirtualId);
            DanceMessage.AppendInt32(DanceId);
            Room.SendMessage(DanceMessage);

            ButterflyEnvironment.GetGame().GetQuestManager().ProgressUserQuest(Session, HabboHotel.Quests.QuestType.SOCIAL_DANCE);
        }

        internal void AnswerDoorbell()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session))
            {
                return;
            }

            string Name = Request.PopFixedString();
            byte[] Result = Request.ReadBytes(1);

            GameClient Client = ButterflyEnvironment.GetGame().GetClientManager().GetClientByUsername(Name);

            if (Client == null)
            {
                return;
            }

            if (Result[0] == Convert.ToByte(65))
            {
                Client.GetHabbo().LoadingChecksPassed = true;

                Client.GetMessageHandler().Response.Init(41);
                Client.GetMessageHandler().SendResponse();
            }
            else
            {
                Client.GetMessageHandler().Response.Init(131);
                Client.GetMessageHandler().SendResponse();
            }
        }

        internal void ApplyRoomEffect()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true))
            {
                return;
            }

            UserItem Item = Session.GetHabbo().GetInventoryComponent().GetItem(Request.PopWiredUInt());

            if (Item == null)
            {
                return;
            }

            string type = "floor";

            if (Item.GetBaseItem().Name.ToLower().Contains("wallpaper"))
            {
                type = "wallpaper";
            }
            else if (Item.GetBaseItem().Name.ToLower().Contains("landscape"))
            {
                type = "landscape";
            }

            switch (type)
            {
                case "floor":

                    Room.Floor = Item.ExtraData;
                    Room.RoomData.Floor = Item.ExtraData;

                    ButterflyEnvironment.GetGame().GetQuestManager().ProgressUserQuest(Session, HabboHotel.Quests.QuestType.FURNI_DECORATION_FLOOR);
                    break;

                case "wallpaper":

                    Room.Wallpaper = Item.ExtraData;
                    Room.RoomData.Wallpaper = Item.ExtraData;

                    ButterflyEnvironment.GetGame().GetQuestManager().ProgressUserQuest(Session, HabboHotel.Quests.QuestType.FURNI_DECORATION_WALL);
                    break;

                case "landscape":

                    Room.Landscape = Item.ExtraData;
                    Room.RoomData.Landscape = Item.ExtraData;
                    break;
            }

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                dbClient.setQuery("UPDATE rooms SET " + type + " = @extradata WHERE id = " + Room.RoomId);
                dbClient.addParameter("extradata", Item.ExtraData);
                dbClient.runQuery();
            }

            Session.GetHabbo().GetInventoryComponent().RemoveItem(Item.Id, false);

            ServerMessage Message = new ServerMessage(46);
            Message.AppendStringWithBreak(type);
            Message.AppendStringWithBreak(Item.ExtraData);
            Room.SendMessage(Message);
        }

        internal void PlacePostIt()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);
            if (Room == null || !Room.CheckRights(Session))
            {
                return;
            }
            
            uint itemId = Request.PopWiredUInt();
            string locationData = Request.PopFixedString();

            UserItem item = Session.GetHabbo().GetInventoryComponent().GetItem(itemId);

            if (item == null || Room == null)
                return;

            try
            {
                WallCoordinate coordinate = new WallCoordinate(":" + locationData.Split(':')[1]);

                RoomItem RoomItem = new RoomItem(item.Id, Room.RoomId, item.BaseItem, item.ExtraData, coordinate, Room);

                if (Room.GetRoomItemHandler().SetWallItem(Session, RoomItem))
                {
                    Session.GetHabbo().GetInventoryComponent().RemoveItem(itemId, true);
                }
            }
            catch
            {
                Response.Init(516);
                Response.AppendInt32(11);
                SendResponse();
                return;
            }
        }

        internal void PlaceItem()
        {
            // AZ@J16 10 10 0

            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session))
            {
                return;
            }



            string PlacementData = Request.PopFixedString();
            string[] DataBits = PlacementData.Split(' ');
            uint ItemId = uint.Parse(DataBits[0].Replace("-",""));

            UserItem Item = Session.GetHabbo().GetInventoryComponent().GetItem(ItemId);

            if (Item == null)
            {
                return;
            }
            //bool UpdateNeeded = false;

            switch (Item.GetBaseItem().InteractionType)
            {
                case Butterfly.HabboHotel.Items.InteractionType.dimmer:
                    {
                        MoodlightData moodData = Room.MoodlightData;
                        if (moodData != null && Room.GetRoomItemHandler().GetItem(moodData.ItemId) != null)
                            Session.SendNotif(LanguageLocale.GetValue("user.maxmoodlightsreached"));
                        break;
                    }
            }

            // Wall Item
            if (DataBits[1].StartsWith(":"))
            {
                try
                {
                    WallCoordinate coordinate = new WallCoordinate(":" + PlacementData.Split(':')[1]);
                    
                    RoomItem RoomItem = new RoomItem(Item.Id, Room.RoomId, Item.BaseItem, Item.ExtraData, coordinate, Room);

                    if (Room.GetRoomItemHandler().SetWallItem(Session, RoomItem))
                    {
                        Session.GetHabbo().GetInventoryComponent().RemoveItem(ItemId, true);
                    }
                }
                catch
                {
                    Response.Init(516);
                    Response.AppendInt32(11);
                    SendResponse();
                    return;
                }
            }
            // Floor Item
            else
            {
                int X = int.Parse(DataBits[1]);
                int Y = int.Parse(DataBits[2]);
                int Rot = int.Parse(DataBits[3]);

                if (Session.GetHabbo().forceRot > -1)
                    Rot = Session.GetHabbo().forceRot;

                RoomItem RoomItem = new RoomItem(Item.Id, Room.RoomId, Item.BaseItem, Item.ExtraData, X, Y, 0, Rot, Room);

                if (Room.GetRoomItemHandler().SetFloorItem(Session, RoomItem, X, Y, Rot, true, false, true))
                {
                    Session.GetHabbo().GetInventoryComponent().RemoveItem(ItemId, true);
                }

                if (WiredUtillity.TypeIsWired(Item.GetBaseItem().InteractionType))
                {
                    WiredSaver.HandleDefaultSave(Item.Id, Room);
                }

                ButterflyEnvironment.GetGame().GetQuestManager().ProgressUserQuest(Session, HabboHotel.Quests.QuestType.FURNI_PLACE);
            }
            //if (UpdateNeeded)
            //    Room.SaveFurniture();
            
        }

        internal void TakeItem()
        {
            int junk = Request.PopWiredInt32();

            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true))
            {
                return;
            }

            RoomItem Item = Room.GetRoomItemHandler().GetItem(Request.PopWiredUInt());

            if (Item == null)
            {
                return;
            }



            if (Item.GetBaseItem().InteractionType == Butterfly.HabboHotel.Items.InteractionType.postit)
                return;

            Room.GetRoomItemHandler().RemoveFurniture(Session, Item.Id);
            Session.GetHabbo().GetInventoryComponent().AddNewItem(Item.Id, Item.BaseItem, Item.ExtraData, true, true, 0);
            Session.GetHabbo().GetInventoryComponent().UpdateItems(false);

            ButterflyEnvironment.GetGame().GetQuestManager().ProgressUserQuest(Session, HabboHotel.Quests.QuestType.FURNI_PICK);
        }

        internal void MoveItem()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session))
            {
                return;
            }

            RoomItem Item = Room.GetRoomItemHandler().GetItem(Request.PopWiredUInt());

            if (Item == null)
            {
                return;
            }

            if (Item.wiredHandler != null)
            {
                using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
                {
                    Item.wiredHandler.DeleteFromDatabase(dbClient);
                    Item.wiredHandler.Dispose();
                    Room.GetWiredHandler().RemoveFurniture(Item);
                }
                Item.wiredHandler = null;
            }

            if (Item.wiredCondition != null)
            {
                using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
                {
                    Item.wiredCondition.DeleteFromDatabase(dbClient);
                    Item.wiredCondition.Dispose();
                    Room.GetWiredHandler().conditionHandler.ClearTile(Item.Coordinate);
                }
                Item.wiredCondition = null;
            }

            int x = Request.PopWiredInt32();
            int y = Request.PopWiredInt32();
            int Rotation = Request.PopWiredInt32();
            int Junk = Request.PopWiredInt32();

            bool UpdateNeeded = false;

            if (Item.GetBaseItem().InteractionType == Butterfly.HabboHotel.Items.InteractionType.teleport)
                UpdateNeeded = true;

            if (x != Item.GetX || y != Item.GetY)
            {
                ButterflyEnvironment.GetGame().GetQuestManager().ProgressUserQuest(Session, HabboHotel.Quests.QuestType.FURNI_MOVE);
            }

            if (Rotation != Item.Rot)
            {
                ButterflyEnvironment.GetGame().GetQuestManager().ProgressUserQuest(Session, HabboHotel.Quests.QuestType.FURNI_ROTATE);
            }

            Room.GetRoomItemHandler().SetFloorItem(Session, Item, x, y, Rotation, false, false, true);

            if (Item.GetZ >= 0.1)
            {
                ButterflyEnvironment.GetGame().GetQuestManager().ProgressUserQuest(Session, HabboHotel.Quests.QuestType.FURNI_STACK);
            }

            if (UpdateNeeded)
            {
                using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
                {
                    Room.GetRoomItemHandler().SaveFurniture(dbClient);
                }
            }
        }

        internal void MoveWallItem()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session))
            {
                return;
            }

            uint itemID = Request.PopWiredUInt();
            string wallPositionData = Request.PopFixedString();

            RoomItem Item = Room.GetRoomItemHandler().GetItem(itemID);

            if (Item == null)
                return;

            try
            {
                WallCoordinate coordinate = new WallCoordinate(":" + wallPositionData.Split(':')[1]);
                Item.wallCoord = coordinate;
            }
            catch
            {
                Response.Init(516);
                Response.AppendInt32(11);
                SendResponse();

                return;
            }

            Room.GetRoomItemHandler().UpdateItem(Item);

            ServerMessage LeaveMessage = new ServerMessage(84);
            LeaveMessage.AppendRawUInt(Item.Id);
            LeaveMessage.AppendStringWithBreak(string.Empty);
            LeaveMessage.AppendBoolean(false);
            Room.SendMessage(LeaveMessage);

            ServerMessage Message = new ServerMessage(83);
            Item.Serialize(Message);
            Room.SendMessage(Message);
        }

        internal void TriggerItem()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null)
            {
                return;
            }

            uint itemID = Request.PopWiredUInt();

            

            RoomItem Item = Room.GetRoomItemHandler().GetItem(itemID);


            //if (Session.GetHabbo().Username == "martinmine")
            //{
            //    StringBuilder builder = new StringBuilder();
            //    builder.AppendLine("ItemID information for ID " + itemID);
            //    if (Item != null)
            //    {
            //        builder.Append("RoomID: " + Item.RoomId);
            //        if (Item.GetRoom() != null)
            //            builder.AppendLine("Room owner: " + Item.GetRoom().Owner);
            //    }

            //    Session.SendNotif(builder.ToString());
            //}

            if (Item == null)
            {
                return;
            }

            Boolean hasRights = false;

            if (Room.CheckRights(Session))
            {
                hasRights = true;
            }

            string oldData = Item.ExtraData;
            int request = Request.PopWiredInt32();
            Item.Interactor.OnTrigger(Session, Item, request, hasRights);
            Item.OnTrigger(Room.GetRoomUserManager().GetRoomUserByHabbo(Session.GetHabbo().Id));

            if (Room.GotWired() && !WiredUtillity.TypeIsWired(Item.GetBaseItem().InteractionType))
            {
                bool shouldBeHandled = false;
                int x = Item.GetX;
                int y = Item.GetY;
                Point up = new Point(x, y + 1);
                Point down = new Point(x + 1, y);
                Point left = new Point(x, y - 1);
                Point right = new Point(x - 1, y);

                foreach (RoomItem item in Room.GetGameMap().GetCoordinatedItems(up))
                {
                    if (WiredHandler.TypeIsWire(item.GetBaseItem().InteractionType))
                        shouldBeHandled = true;
                }

                foreach (RoomItem item in Room.GetGameMap().GetCoordinatedItems(down))
                {
                    if (WiredHandler.TypeIsWire(item.GetBaseItem().InteractionType))
                        shouldBeHandled = true;
                }

                foreach (RoomItem item in Room.GetGameMap().GetCoordinatedItems(left))
                {
                    if (WiredHandler.TypeIsWire(item.GetBaseItem().InteractionType))
                        shouldBeHandled = true;
                }

                foreach (RoomItem item in Room.GetGameMap().GetCoordinatedItems(right))
                {
                    if (WiredHandler.TypeIsWire(item.GetBaseItem().InteractionType))
                        shouldBeHandled = true;
                }

                if (shouldBeHandled)
                    Room.GetWiredHandler().TriggerOnWire(Item.Coordinate);
                else
                    Room.GetWiredHandler().RemoveWiredItem(Item.Coordinate);
            }
        }

        internal void TriggerItemDiceSpecial()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null)
            {
                return;
            }

            RoomItem Item = Room.GetRoomItemHandler().GetItem(Request.PopWiredUInt());

            if (Item == null)
            {
                return;
            }

            Boolean hasRights = false;

            if (Room.CheckRights(Session))
            {
                hasRights = true;
            }

            Item.Interactor.OnTrigger(Session, Item, -1, hasRights);
            Item.OnTrigger(Room.GetRoomUserManager().GetRoomUserByHabbo(Session.GetHabbo().Id));
        }

        internal void OpenPostit()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);
            //TODO fix post it
            if (Room == null)
            {
                return;
            }

            RoomItem Item = Room.GetRoomItemHandler().GetItem(Request.PopWiredUInt());

            if (Item == null || Item.GetBaseItem().InteractionType != Butterfly.HabboHotel.Items.InteractionType.postit)
            {
                return;
            }

            // @p181855059CFF9C stickynotemsg
            Response.Init(48);
            Response.AppendStringWithBreak(Item.Id.ToString());
            Response.AppendStringWithBreak(Item.ExtraData);
            SendResponse();
        }

        internal void SavePostit()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null)
            {
                return;
            }

            RoomItem Item = Room.GetRoomItemHandler().GetItem(Request.PopWiredUInt());

            if (Item == null || Item.GetBaseItem().InteractionType != Butterfly.HabboHotel.Items.InteractionType.postit)
            {
                return;
            }

            String Data = Request.PopFixedString();
            String Color = Data.Split(' ')[0];
            String Text = ButterflyEnvironment.FilterInjectionChars(Data.Substring(Color.Length + 1), true);

            if (!Room.CheckRights(Session))
            {
                if (!Data.StartsWith(Item.ExtraData))
                {
                    return; // we can only ADD stuff! older stuff changed, this is not allowed
                }
            }

            switch (Color)
            {
                case "FFFF33":
                case "FF9CFF":
                case "9CCEFF":
                case "9CFF9C":

                    break;

                default:

                    return; // invalid color
            }

            Item.ExtraData = Color + " " + Text;
            Item.UpdateState(true, true);
        }

        internal void DeletePostit()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true))
            {
                return;
            }

            RoomItem Item = Room.GetRoomItemHandler().GetItem(Request.PopWiredUInt());

            if (Item == null || Item.GetBaseItem().InteractionType != Butterfly.HabboHotel.Items.InteractionType.postit)
            {
                return;
            }

            Room.GetRoomItemHandler().RemoveFurniture(Session, Item.Id);
        }

        internal void OpenPresent()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true))
            {
                return;
            }

            RoomItem Present = Room.GetRoomItemHandler().GetItem(Request.PopWiredUInt());

            if (Present == null)
            {
                return;
            }

            DataRow Data = null;

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                dbClient.setQuery("SELECT base_id,amount,extra_data FROM user_presents WHERE item_id = " + Present.Id + "");
                Data = dbClient.getRow();
            }

            if (Data == null)
            {
                return;
            }

            Item BaseItem = ButterflyEnvironment.GetGame().GetItemManager().GetItem(Convert.ToUInt32(Data["base_id"]));

            if (BaseItem == null)
            {
                return;
            }

            Room.GetRoomItemHandler().RemoveFurniture(Session, Present.Id);

            Response.Init(219);
            Response.AppendUInt(Present.Id);
            SendResponse();

            Response.Init(129);
            Response.AppendStringWithBreak(BaseItem.Type.ToString());
            Response.AppendInt32(BaseItem.SpriteId);
            Response.AppendStringWithBreak(BaseItem.Name);
            SendResponse();

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                dbClient.runFastQuery("DELETE FROM user_presents WHERE item_id = " + Present.Id);
            }

            ButterflyEnvironment.GetGame().GetCatalog().DeliverItems(Session, BaseItem, (int)Data["amount"], (String)Data["extra_data"]);
        }

        internal void GetMoodlight()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true) || Room.MoodlightData == null)
            {
                return;
            }

            Response.Init(365);
            Response.AppendInt32(Room.MoodlightData.Presets.Count);
            Response.AppendInt32(Room.MoodlightData.CurrentPreset);

                int i = 0;

                foreach (MoodlightPreset Preset in Room.MoodlightData.Presets)
                {
                    i++;

                    Response.AppendInt32(i);
                    Response.AppendInt32(int.Parse(ButterflyEnvironment.BoolToEnum(Preset.BackgroundOnly)) + 1);
                    Response.AppendStringWithBreak(Preset.ColorCode);
                    Response.AppendInt32(Preset.ColorIntensity);
                }
            

            SendResponse();
        }

        internal void UpdateMoodlight()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true) || Room.MoodlightData == null)
            {
                return;
            }

            RoomItem Item = Room.GetRoomItemHandler().GetItem(Room.MoodlightData.ItemId);

            if (Item == null || Item.GetBaseItem().InteractionType != InteractionType.dimmer)
                return;

            // EVIH@G#EA4532RbI

            int Preset = Request.PopWiredInt32();
            int BackgroundMode = Request.PopWiredInt32();
            string ColorCode = Request.PopFixedString();
            int Intensity = Request.PopWiredInt32();

            bool BackgroundOnly = false;

            if (BackgroundMode >= 2)
            {
                BackgroundOnly = true;
            }

            Room.MoodlightData.Enabled = true;
            Room.MoodlightData.CurrentPreset = Preset;
            Room.MoodlightData.UpdatePreset(Preset, ColorCode, Intensity, BackgroundOnly);

            Item.ExtraData = Room.MoodlightData.GenerateExtraData();
            Item.UpdateState();
        }

        internal void SwitchMoodlightStatus()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true) || Room.MoodlightData == null)
            {
                return;
            }

            RoomItem Item = Room.GetRoomItemHandler().GetItem(Room.MoodlightData.ItemId);

            if (Item == null || Item.GetBaseItem().InteractionType != InteractionType.dimmer)
                return;

            if (Room.MoodlightData.Enabled)
            {
                Room.MoodlightData.Disable();
            }
            else
            {
                Room.MoodlightData.Enable();
            }

            Item.ExtraData = Room.MoodlightData.GenerateExtraData();
            Item.UpdateState();
        }

        internal void InitTrade()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CanTradeInRoom)
            {
                return;
            }

            RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(Session.GetHabbo().Id);
            RoomUser User2 = Room.GetRoomUserManager().GetRoomUserByVirtualId(Request.PopWiredInt32());

            if (User2 == null || User2.GetClient() == null || User2.GetClient().GetHabbo() == null)
                return;

            bool IsDisabled = false;
            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                dbClient.setQuery("SELECT block_trade FROM users WHERE id = " + User2.GetClient().GetHabbo().Id);
                IsDisabled = ButterflyEnvironment.EnumToBool(dbClient.getString());
            }

            if (IsDisabled)
            {
                Session.SendNotif(LanguageLocale.GetValue("user.tradedisabled"));
                return;
            }
            else
                Room.TryStartTrade(User, User2);
            
        }

        internal void OfferTradeItem()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CanTradeInRoom)
            {
                return;
            }

            Trade Trade = Room.GetUserTrade(Session.GetHabbo().Id);
            UserItem Item = Session.GetHabbo().GetInventoryComponent().GetItem(Request.PopWiredUInt());

            if (Trade == null || Item == null)
            {
                return;
            }

            Trade.OfferItem(Session.GetHabbo().Id, Item);
        }

        internal void TakeBackTradeItem()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CanTradeInRoom)
            {
                return;
            }

            Trade Trade = Room.GetUserTrade(Session.GetHabbo().Id);
            UserItem Item = Session.GetHabbo().GetInventoryComponent().GetItem(Request.PopWiredUInt());

            if (Trade == null || Item == null)
            {
                return;
            }

            Trade.TakeBackItem(Session.GetHabbo().Id, Item);
        }

        internal void StopTrade()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CanTradeInRoom)
            {
                return;
            }

            Room.TryStopTrade(Session.GetHabbo().Id);
        }

        internal void AcceptTrade()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CanTradeInRoom)
            {
                return;
            }

            Trade Trade = Room.GetUserTrade(Session.GetHabbo().Id);

            if (Trade == null)
            {
                return;
            }

            Trade.Accept(Session.GetHabbo().Id);
        }

        internal void UnacceptTrade()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CanTradeInRoom)
            {
                return;
            }

            Trade Trade = Room.GetUserTrade(Session.GetHabbo().Id);

            if (Trade == null)
            {
                return;
            }

            Trade.Unaccept(Session.GetHabbo().Id);
        }

        internal void CompleteTrade()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CanTradeInRoom)
            {
                return;
            }

            Trade Trade = Room.GetUserTrade(Session.GetHabbo().Id);

            if (Trade == null)
            {
                return;
            }

            Trade.CompleteTrade(Session.GetHabbo().Id);
        }

        internal void GiveRespect()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || Session.GetHabbo().DailyRespectPoints <= 0)
            {
                return;
            }

            RoomUser User = Room.GetRoomUserManager().GetRoomUserByHabbo(Request.PopWiredUInt());
            
            if (User == null || User.GetClient().GetHabbo().Id == Session.GetHabbo().Id || User.IsBot)
            {
                return;
            }

            ButterflyEnvironment.GetGame().GetQuestManager().ProgressUserQuest(Session, HabboHotel.Quests.QuestType.SOCIAL_RESPECT);

            ButterflyEnvironment.GetGame().GetAchievementManager().ProgressUserAchievement(Session, "ACH_RespectEarned", 1);
            ButterflyEnvironment.GetGame().GetAchievementManager().ProgressUserAchievement(User.GetClient(), "ACH_RespectGiven", 1);

            Session.GetHabbo().DailyRespectPoints--;
            User.GetClient().GetHabbo().Respect++;

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                dbClient.runFastQuery("UPDATE users SET respect = respect + 1 WHERE id = " + User.GetClient().GetHabbo().Id);
                dbClient.runFastQuery("UPDATE users SET daily_respect_points = daily_respect_points - 1 WHERE id = " + Session.GetHabbo().Id);
            }

            // FxkqUzYP_
            ServerMessage Message = new ServerMessage(440);
            Message.AppendUInt(User.GetClient().GetHabbo().Id);
            Message.AppendInt32(User.GetClient().GetHabbo().Respect);
            Room.SendMessage(Message);
        }

        internal void ApplyEffect()
        {
            Session.GetHabbo().GetAvatarEffectsInventoryComponent().ApplyEffect(Request.PopWiredInt32());
        }

        internal void EnableEffect()
        {
            Session.GetHabbo().GetAvatarEffectsInventoryComponent().EnableEffect(Request.PopWiredInt32());
        }

        internal void RecycleItems()
        {
            if (!Session.GetHabbo().InRoom)
            {
                return;
            }

            int itemCount = Request.PopWiredInt32();

            if (itemCount != 5)
            {
                return;
            }

            for (int i = 0; i < itemCount; i++)
            {
                UserItem Item = Session.GetHabbo().GetInventoryComponent().GetItem(Request.PopWiredUInt());

                if (Item != null && Item.GetBaseItem().AllowRecycle)
                {
                    Session.GetHabbo().GetInventoryComponent().RemoveItem(Item.Id, false);
                }
                else
                {
                    return;
                }
            }

            uint newItemId;// = ButterflyEnvironment.GetGame().GetCatalog().GenerateItemId();
            EcotronReward Reward = ButterflyEnvironment.GetGame().GetCatalog().GetRandomEcotronReward();

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                if (dbClient.dbType == Database_Manager.Database.DatabaseType.MSSQL)
                    dbClient.setQuery("INSERT INTO user_items (user_id,base_item,extra_data) OUTPUT INSERTED.* VALUES ( @userid ,1478, @timestamp)");
                else
                    dbClient.setQuery("INSERT INTO user_items (user_id,base_item,extra_data) VALUES ( @userid ,1478, @timestamp)");
                dbClient.addParameter("userid", (int)Session.GetHabbo().Id);
                dbClient.addParameter("timestamp", DateTime.Now.ToLongDateString());

                newItemId = (uint)dbClient.insertQuery();

                dbClient.runFastQuery("INSERT INTO user_presents (item_id,base_id,amount,extra_data) VALUES (" + newItemId + "," + Reward.BaseId + ",1,'')");
            }

            Session.GetHabbo().GetInventoryComponent().UpdateItems(true);

            Response.Init(508);
            Response.AppendBoolean(true);
            Response.AppendUInt(newItemId);
            SendResponse();
        }

        internal void RedeemExchangeFurni()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true))
            {
                return;
            }

            RoomItem Exchange = Room.GetRoomItemHandler().GetItem(Request.PopWiredUInt());

            if (Exchange == null)
            {
                return;
            }

            if (!Exchange.GetBaseItem().Name.StartsWith("CF_") && !Exchange.GetBaseItem().Name.StartsWith("CFC_"))
            {
                return;
            }
            
            string[] Split = Exchange.GetBaseItem().Name.Split('_');
            int Value = int.Parse(Split[1]);

            if (Value > 0)
            {
                Session.GetHabbo().Credits += Value;
                Session.GetHabbo().UpdateCreditsBalance();
            }

            Room.GetRoomItemHandler().RemoveFurniture(null, Exchange.Id);
            
            Response.Init(219);
            SendResponse();
        }

        internal void EnterInfobus()
        {
            // AQThe Infobus is currently closed.
            Response.Init(81);
            Response.AppendStringWithBreak(LanguageLocale.GetValue("user.enterinfobus"));
            SendResponse();
        }

        internal void KickBot()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session, true))
            {
                return;
            }

            RoomUser Bot = Room.GetRoomUserManager().GetRoomUserByVirtualId(Request.PopWiredInt32());

            if (Bot == null || !Bot.IsBot)
            {
                return;
            }

            Room.GetRoomUserManager().RemoveBot(Bot.VirtualId, true);
        }

        internal void PlacePet()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || (!Room.AllowPets && !Room.CheckRights(Session, true)) || !Room.CheckRights(Session, true))
            {
                return;
            }

            uint PetId = Request.PopWiredUInt();

            Pet Pet = Session.GetHabbo().GetInventoryComponent().GetPet(PetId);

            if (Pet == null || Pet.PlacedInRoom)
            {
                return;
            }

            int X = Request.PopWiredInt32();
            int Y = Request.PopWiredInt32();

            if (!Room.GetGameMap().CanWalk(X, Y, false))
            {
                return;
            }

            //if (Room.GetRoomUserManager().PetCount >= RoomManager.MAX_PETS_PER_ROOM)
            //{
            //    Session.SendNotif(LanguageLocale.GetValue("user.maxpetreached"));
            //    return;
            //}

            RoomUser oldPet = Room.GetRoomUserManager().GetPet(PetId);
            if (oldPet != null)
                Room.GetRoomUserManager().RemoveBot(oldPet.VirtualId, false);

            Pet.PlacedInRoom = true;
            Pet.RoomId = Room.RoomId;

            List<RandomSpeech> RndSpeechList = new List<RandomSpeech>();
            List<BotResponse> BotResponse = new List<Butterfly.HabboHotel.RoomBots.BotResponse>();
            RoomUser PetUser = Room.GetRoomUserManager().DeployBot(new RoomBot(Pet.PetId, Pet.RoomId, AIType.Pet, "freeroam", Pet.Name, "", Pet.Look, X, Y, 0, 0, 0, 0, 0, 0, ref RndSpeechList, ref BotResponse), Pet);

            Session.GetHabbo().GetInventoryComponent().MovePetToRoom(Pet.PetId);

            if (Pet.DBState != DatabaseUpdateState.NeedsInsert)
                Pet.DBState = DatabaseUpdateState.NeedsUpdate;

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
                Room.GetRoomUserManager().SavePets(dbClient);
        }

        internal void GetPetInfo()
        {
            if (Session.GetHabbo() == null ||Session.GetHabbo().CurrentRoom == null)
                return;

            RoomUser pet = Session.GetHabbo().CurrentRoom.GetRoomUserManager().GetPet(Request.PopWiredUInt());
            if (pet == null || pet.PetData == null)
            {
                Session.SendNotif(LanguageLocale.GetValue("user.petinfoerror"));
                return;
            }

            Session.SendMessage(pet.PetData.SerializeInfo());
        }

        internal void PickUpPet()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Session == null || Session.GetHabbo() == null || Session.GetHabbo().GetInventoryComponent() == null)
                return;

            if (Room == null || Room.IsPublic || (!Room.AllowPets && !Room.CheckRights(Session, true)))
            {
                return;
            }

            uint PetId = Request.PopWiredUInt();
            RoomUser PetUser = Room.GetRoomUserManager().GetPet(PetId);
            if (PetUser == null)
                return;

            if (PetUser.PetData.DBState != DatabaseUpdateState.NeedsInsert)
                PetUser.PetData.DBState = DatabaseUpdateState.NeedsUpdate;
            PetUser.PetData.RoomId = 0;

            Session.GetHabbo().GetInventoryComponent().AddPet(PetUser.PetData);

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
                Room.GetRoomUserManager().SavePets(dbClient);

            Room.GetRoomUserManager().RemoveBot(PetUser.VirtualId, false);
        }

        internal void RespectPet()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || Room.IsPublic || (!Room.AllowPets && !Room.CheckRights(Session, true)))
            {
                return;
            }

            uint PetId = Request.PopWiredUInt();
            RoomUser PetUser = Room.GetRoomUserManager().GetPet(PetId);

            if (PetUser == null || PetUser.PetData == null || PetUser.PetData.OwnerId != Session.GetHabbo().Id)
            {
                return;
            }

            PetUser.PetData.OnRespect();
            Session.GetHabbo().DailyPetRespectPoints--;

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                //dbClient.addParameter("userid", Session.GetHabbo().Id);
                dbClient.runFastQuery("UPDATE users SET daily_pet_respect_points = daily_pet_respect_points - 1 WHERE id = " + Session.GetHabbo().Id);
            }
        }

        internal void SetLookTransfer()
        {
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            if (Room == null || !Room.CheckRights(Session))
                return;

            uint ItemID = Request.PopWiredUInt();

            string Gender = Request.PopFixedString().ToUpper();
            string Look = ButterflyEnvironment.FilterInjectionChars(Request.PopFixedString());

            RoomItem RoomItemToSet = Room.GetRoomItemHandler().mFloorItems.GetValue(ItemID);

            if (Gender.Length > 1)
                return;

            if (Gender != "M" && Gender != "F")
                return;

            RoomItemToSet.Figure = ButterflyEnvironment.FilterFigure(Look);
            RoomItemToSet.Gender = Gender;

            RoomItemToSet.ExtraData = Gender + ":" + Look;
        }

        internal void CommandsPet()
        {
            uint PetID = Request.PopWiredUInt();
            Room Room = ButterflyEnvironment.GetGame().GetRoomManager().GetRoom(Session.GetHabbo().CurrentRoomId);

            RoomUser PetUser = Room.GetRoomUserManager().GetPet(PetID);

            if (PetUser == null || PetUser.PetData == null)
                return;

            GetResponse().Init(605);
            GetResponse().AppendUInt(PetID);

            int level = PetUser.PetData.Level;

            GetResponse().AppendInt32(18);
            GetResponse().AppendInt32(0);
            GetResponse().AppendInt32(1);
            GetResponse().AppendInt32(2);
            GetResponse().AppendInt32(3);
            GetResponse().AppendInt32(4);
            GetResponse().AppendInt32(17);
            GetResponse().AppendInt32(5);
            GetResponse().AppendInt32(6);
            GetResponse().AppendInt32(7);
            GetResponse().AppendInt32(8);
            GetResponse().AppendInt32(9);
            GetResponse().AppendInt32(10);
            GetResponse().AppendInt32(11);
            GetResponse().AppendInt32(12);
            GetResponse().AppendInt32(13);
            GetResponse().AppendInt32(14);
            GetResponse().AppendInt32(15);
            GetResponse().AppendInt32(16);

            for (int i = 0; level > i; )
            {
                i++;
                GetResponse().AppendInt32(i);
            }

            GetResponse().AppendInt32(0);
            GetResponse().AppendInt32(1);
            GetResponse().AppendInt32(2);
            SendResponse();
        }  



        //internal void PetRaces()
        //{
        //    int PetType = Request.PopWiredInt32();
        //    Response.Init(827);

        //    if (PetType == 0) // dogs 
        //        Response.AppendString("HQFSDQDRDSCPDQCRCSBPCQEPERESEPFKJRBIHSARAQAPAQBPB");
        //    else if (PetType == 1) // cats 
        //        Response.AppendString("IQFSDQDRDSCPDQCRCSBPCQEPERESEPFKJRBIHSARAQAPAQBPB");
        //    else if (PetType == 2) // croc 
        //        Response.AppendString("JPCHIJKPAQARASAPBQBRBSB");
        //    else if (PetType == 3) // terreir 
        //        Response.AppendString("KSAHIJKPAQARA");
        //    else if (PetType == 4) // bear 
        //        Response.AppendString("PAPAHIJK");
        //    else if (PetType == 5) // pigs 
        //        Response.AppendString("QASAHIJKQASAPB");
        //    else if (PetType == 6) // lion 
        //        Response.AppendString("RASAHIJKPAQASB");
        //    else if (PetType == 7) // rhino 
        //        Response.AppendString("SASAHIJPAQARASA");
        //    else if (PetType == 8) // spider 
        //        Response.AppendString("PBQCHIRBSBRCJKPAQARASAPBQB");

        //    SendResponse();
        //}  

        internal void SaveWired()
        {
            uint itemID = Request.PopWiredUInt();
            WiredSaver.HandleSave(itemID, Session.GetHabbo().CurrentRoom, Request);
        }

        internal void SaveWiredConditions()
        {
            uint itemID = Request.PopWiredUInt();
            WiredSaver.HandleConditionSave(itemID, Session.GetHabbo().CurrentRoom, Request);
        }

        //internal void SaveWiredWithFurniture()
        //{
        //    uint itemID = Request.PopWiredUInt();
        //    WiredSaver.HandleSave(itemID, Session.GetHabbo().CurrentRoom, Request);
        //}

        //internal void RegisterRooms()
        //{
        //    RequestHandlers.Add(182, new RequestHandler(GetAdvertisement));
        //    RequestHandlers.Add(388, new RequestHandler(GetPub));
        //    RequestHandlers.Add(2, new RequestHandler(OpenPub));
        //    RequestHandlers.Add(230, new RequestHandler(GetGroupBadges));
        //    RequestHandlers.Add(215, new RequestHandler(GetRoomData1));
        //    RequestHandlers.Add(390, new RequestHandler(GetRoomData2));
        //    RequestHandlers.Add(126, new RequestHandler(GetRoomData3));
        //    RequestHandlers.Add(52, new RequestHandler(Talk));
        //    RequestHandlers.Add(55, new RequestHandler(Shout));
        //    RequestHandlers.Add(56, new RequestHandler(Whisper));
        //    RequestHandlers.Add(75, new RequestHandler(Move));
        //    RequestHandlers.Add(387, new RequestHandler(CanCreateRoom));
        //    RequestHandlers.Add(29, new RequestHandler(CreateRoom));
        //    RequestHandlers.Add(400, new RequestHandler(GetRoomEditData));
        //    RequestHandlers.Add(386, new RequestHandler(SaveRoomIcon));
        //    RequestHandlers.Add(401, new RequestHandler(SaveRoomData));
        //    RequestHandlers.Add(96, new RequestHandler(GiveRights));
        //    RequestHandlers.Add(97, new RequestHandler(TakeRights));
        //    RequestHandlers.Add(155, new RequestHandler(TakeAllRights));
        //    RequestHandlers.Add(95, new RequestHandler(KickUser));
        //    RequestHandlers.Add(320, new RequestHandler(BanUser));
        //    RequestHandlers.Add(71, new RequestHandler(InitTrade));
        //    RequestHandlers.Add(384, new RequestHandler(SetHomeRoom));
        //    RequestHandlers.Add(23, new RequestHandler(DeleteRoom));
        //    RequestHandlers.Add(79, new RequestHandler(LookAt));
        //    RequestHandlers.Add(317, new RequestHandler(StartTyping));
        //    RequestHandlers.Add(318, new RequestHandler(StopTyping));
        //    RequestHandlers.Add(319, new RequestHandler(IgnoreUser));
        //    RequestHandlers.Add(322, new RequestHandler(UnignoreUser));
        //    RequestHandlers.Add(345, new RequestHandler(CanCreateRoomEvent));
        //    RequestHandlers.Add(346, new RequestHandler(StartEvent));
        //    RequestHandlers.Add(347, new RequestHandler(StopEvent));
        //    RequestHandlers.Add(348, new RequestHandler(EditEvent));
        //    RequestHandlers.Add(94, new RequestHandler(Wave));
        //    RequestHandlers.Add(263, new RequestHandler(GetUserTags));
        //    RequestHandlers.Add(159, new RequestHandler(GetUserBadges));
        //    RequestHandlers.Add(261, new RequestHandler(RateRoom));
        //    RequestHandlers.Add(93, new RequestHandler(Dance));
        //    RequestHandlers.Add(98, new RequestHandler(AnswerDoorbell));
        //    RequestHandlers.Add(59, new RequestHandler(ReqLoadRoomForUser));
        //    RequestHandlers.Add(66, new RequestHandler(ApplyRoomEffect));
        //    RequestHandlers.Add(90, new RequestHandler(PlaceItem));
        //    RequestHandlers.Add(67, new RequestHandler(TakeItem));
        //    RequestHandlers.Add(73, new RequestHandler(MoveItem));
        //    RequestHandlers.Add(91, new RequestHandler(MoveWallItem));
        //    RequestHandlers.Add(392, new RequestHandler(TriggerItem)); // Generic trigger item
        //    RequestHandlers.Add(393, new RequestHandler(TriggerItem)); // Generic trigger item
        //    RequestHandlers.Add(83, new RequestHandler(OpenPostit));
        //    RequestHandlers.Add(84, new RequestHandler(SavePostit));
        //    RequestHandlers.Add(85, new RequestHandler(DeletePostit));
        //    RequestHandlers.Add(78, new RequestHandler(OpenPresent));
        //    RequestHandlers.Add(341, new RequestHandler(GetMoodlight));
        //    RequestHandlers.Add(342, new RequestHandler(UpdateMoodlight));
        //    RequestHandlers.Add(343, new RequestHandler(SwitchMoodlightStatus));
        //    RequestHandlers.Add(72, new RequestHandler(OfferTradeItem));
        //    RequestHandlers.Add(405, new RequestHandler(TakeBackTradeItem));
        //    RequestHandlers.Add(70, new RequestHandler(StopTrade));
        //    RequestHandlers.Add(403, new RequestHandler(StopTrade));
        //    RequestHandlers.Add(69, new RequestHandler(AcceptTrade));
        //    RequestHandlers.Add(68, new RequestHandler(UnacceptTrade));
        //    RequestHandlers.Add(402, new RequestHandler(CompleteTrade));
        //    RequestHandlers.Add(371, new RequestHandler(GiveRespect));
        //    RequestHandlers.Add(372, new RequestHandler(ApplyEffect));
        //    RequestHandlers.Add(373, new RequestHandler(EnableEffect));
        //    //RequestHandlers.Add(3004, new RequestHandler(GetTrainerPanel)); DARIO! :@@@@
        //    RequestHandlers.Add(232, new RequestHandler(TriggerItem)); // One way gates
        //    RequestHandlers.Add(314, new RequestHandler(TriggerItem)); // Love Shuffler
        //    RequestHandlers.Add(247, new RequestHandler(TriggerItem)); // Habbo Wheel
        //    RequestHandlers.Add(76, new RequestHandler(TriggerItem)); // Dice
        //    RequestHandlers.Add(77, new RequestHandler(TriggerItemDiceSpecial)); // Dice (special)
        //    RequestHandlers.Add(414, new RequestHandler(RecycleItems));
        //    RequestHandlers.Add(183, new RequestHandler(RedeemExchangeFurni));
        //    RequestHandlers.Add(113, new RequestHandler(EnterInfobus));
        //    RequestHandlers.Add(441, new RequestHandler(KickBot));
        //    RequestHandlers.Add(3002, new RequestHandler(PlacePet));
        //    RequestHandlers.Add(3001, new RequestHandler(GetPetInfo));
        //    RequestHandlers.Add(3003, new RequestHandler(PickUpPet));
        //    RequestHandlers.Add(3004, new RequestHandler(CommandsPet));
        //    RequestHandlers.Add(3005, new RequestHandler(RespectPet));
        //    RequestHandlers.Add(3254, new RequestHandler(PlacePostIt));
        //    //RequestHandlers.Add(3007, new RequestHandler(PetRaces));
        //    RequestHandlers.Add(480, new RequestHandler(SetLookTransfer));
            
        //    RequestHandlers.Add(3051, new RequestHandler(SaveWired));
        //    RequestHandlers.Add(3050, new RequestHandler(SaveWiredWithFurniture));
        //}

        //internal void UnregisterRoom()
        //{
        //    RequestHandlers.Remove(182);
        //    RequestHandlers.Remove(388);
        //    RequestHandlers.Remove(2);
        //    RequestHandlers.Remove(230);
        //    RequestHandlers.Remove(215);
        //    RequestHandlers.Remove(390);
        //    RequestHandlers.Remove(126);
        //    RequestHandlers.Remove(52);
        //    RequestHandlers.Remove(55);
        //    RequestHandlers.Remove(56);
        //    RequestHandlers.Remove(75);
        //    RequestHandlers.Remove(387);
        //    RequestHandlers.Remove(29);
        //    RequestHandlers.Remove(400);
        //    RequestHandlers.Remove(386);
        //    RequestHandlers.Remove(401);
        //    RequestHandlers.Remove(96);
        //    RequestHandlers.Remove(97);
        //    RequestHandlers.Remove(155);
        //    RequestHandlers.Remove(95);
        //    RequestHandlers.Remove(320);
        //    RequestHandlers.Remove(71);
        //    RequestHandlers.Remove(384);
        //    RequestHandlers.Remove(23);
        //    RequestHandlers.Remove(79);
        //    RequestHandlers.Remove(317);
        //    RequestHandlers.Remove(318);
        //    RequestHandlers.Remove(319);
        //    RequestHandlers.Remove(322);
        //    RequestHandlers.Remove(345);
        //    RequestHandlers.Remove(346);
        //    RequestHandlers.Remove(347);
        //    RequestHandlers.Remove(348);
        //    RequestHandlers.Remove(94);
        //    RequestHandlers.Remove(263);
        //    RequestHandlers.Remove(159);
        //    RequestHandlers.Remove(261);
        //    RequestHandlers.Remove(93);
        //    RequestHandlers.Remove(98);
        //    RequestHandlers.Remove(59);
        //    RequestHandlers.Remove(66);
        //    RequestHandlers.Remove(90);
        //    RequestHandlers.Remove(67);
        //    RequestHandlers.Remove(73);
        //    RequestHandlers.Remove(392);
        //    RequestHandlers.Remove(393);
        //    RequestHandlers.Remove(83);
        //    RequestHandlers.Remove(84);
        //    RequestHandlers.Remove(85);
        //    RequestHandlers.Remove(78);
        //    RequestHandlers.Remove(341);
        //    RequestHandlers.Remove(342);
        //    RequestHandlers.Remove(343);
        //    RequestHandlers.Remove(72);
        //    RequestHandlers.Remove(405);
        //    RequestHandlers.Remove(70);
        //    RequestHandlers.Remove(403);
        //    RequestHandlers.Remove(69);
        //    RequestHandlers.Remove(68);
        //    RequestHandlers.Remove(402);
        //    RequestHandlers.Remove(371);
        //    RequestHandlers.Remove(372);
        //    RequestHandlers.Remove(373);
        //    RequestHandlers.Remove(232);
        //    RequestHandlers.Remove(314);
        //    RequestHandlers.Remove(247);
        //    RequestHandlers.Remove(76);
        //    RequestHandlers.Remove(77);
        //    RequestHandlers.Remove(414);
        //    RequestHandlers.Remove(183);
        //    RequestHandlers.Remove(113);
        //    RequestHandlers.Remove(441);
        //    RequestHandlers.Remove(3002);
        //    RequestHandlers.Remove(3001);
        //    RequestHandlers.Remove(3003);
        //    RequestHandlers.Remove(3005);
        //    RequestHandlers.Remove(480);
        //    RequestHandlers.Remove(3051);
        //}
    }
}