﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Butterfly.Collections;
using Butterfly.Core;
using Butterfly.HabboHotel.GameClients;
using Butterfly.HabboHotel.Items;
using Butterfly.HabboHotel.Pathfinding;
using Butterfly.HabboHotel.Pets;
using Butterfly.HabboHotel.RoomBots;
using Butterfly.HabboHotel.Rooms.Games;
using Butterfly.HabboHotel.Users.Inventory;
using Butterfly.Messages;
using Database_Manager.Database.Session_Details.Interfaces;
using Butterfly.Util;
using System.Diagnostics;
using Butterfly.HabboHotel.Groups;

namespace Butterfly.HabboHotel.Rooms
{
    class RoomUserManager
    {
        private Room room;

        internal Hashtable usersByUsername;
        internal Hashtable usersByUserID;
        internal event RoomEventDelegate OnUserEnter;

        private Hashtable pets;

        private QueuedDictionary<int, RoomUser> userlist;
        //internal int RoomUserCounter;

        private int petCount;
        private int userCount;

        private int primaryPrivateUserID;
        private int secondaryPrivateUserID;


        internal int PetCount
        {
            get
            {
                return petCount;
            }
        }

        internal QueuedDictionary<int, RoomUser> UserList
        {
            get
            {
                return userlist;
            }
        }

        internal int GetRoomUserCount()
        {
            return userlist.Inner.Count;
        }

        public RoomUserManager(Room room)
        {
            //this.RoomUserCounter = 0;
            this.room = room;
            this.userlist = new QueuedDictionary<int, RoomUser>(new EventHandler(OnUserAdd), null, new EventHandler(onRemove), null);
            this.pets = new Hashtable();

            this.usersByUsername = new Hashtable();
            this.usersByUserID = new Hashtable();
            this.primaryPrivateUserID = 0;
            this.secondaryPrivateUserID = 0;
            this.ToRemove = new List<RoomUser>(room.UsersMax);

            this.petCount = 0;
            this.userCount = 0;
        }

        internal RoomUser DeployBot(RoomBot Bot, Pet PetData)
        {
            RoomUser BotUser = new RoomUser(0, room.RoomId, primaryPrivateUserID++, room, false);
            int PersonalID = secondaryPrivateUserID++;
            BotUser.InternalRoomID = PersonalID;
            //this.UserList[PersonalID] = BotUser;
            userlist.Add(PersonalID, BotUser);
            DynamicRoomModel Model = room.GetGameMap().Model;

            if ((Bot.X > 0 && Bot.Y > 0) && Bot.X < Model.MapSizeX && Bot.Y < Model.MapSizeY)
            {
                BotUser.SetPos(Bot.X, Bot.Y, Bot.Z);
                BotUser.SetRot(Bot.Rot, false);
            }
            else
            {
                Bot.X = Model.DoorX;
                Bot.Y = Model.DoorY;

                BotUser.SetPos(Model.DoorX, Model.DoorY, Model.DoorZ);
                BotUser.SetRot(Model.DoorOrientation, false);
            }

            BotUser.BotData = Bot;
            BotUser.BotAI = Bot.GenerateBotAI(BotUser.VirtualId);

            if (BotUser.IsPet)
            {


                BotUser.BotAI.Init((int)Bot.BotId, BotUser.VirtualId, room.RoomId, BotUser, room);
                BotUser.PetData = PetData;
                BotUser.PetData.VirtualId = BotUser.VirtualId;
            }
            else
            {
                BotUser.BotAI.Init(-1, BotUser.VirtualId, room.RoomId, BotUser, room);
            }

            UpdateUserStatus(BotUser, false);
            BotUser.UpdateNeeded = true;

            ServerMessage EnterMessage = new ServerMessage(28);
            EnterMessage.AppendInt32(1);
            BotUser.Serialize(EnterMessage, room.GetGameMap().gotPublicPool);
            room.SendMessage(EnterMessage);

            BotUser.BotAI.OnSelfEnterRoom();

            if (BotUser.BotData.AiType == AIType.Guide)
                room.guideBotIsCalled = true;
            if (BotUser.IsPet)
            {
                if (pets.ContainsKey(BotUser.PetData.PetId)) //Pet allready placed
                    pets[BotUser.PetData.PetId] = BotUser;
                else
                    pets.Add(BotUser.PetData.PetId, BotUser);

                petCount++;
            }

            return BotUser;
        }

        internal void RemoveBot(int VirtualId, bool Kicked)
        {
            RoomUser User = GetRoomUserByVirtualId(VirtualId);

            if (User == null || !User.IsBot)
            {
                return;
            }

            if (User.IsPet)
            {
                pets.Remove(User.PetData.PetId);
                petCount--;
            }

            User.BotAI.OnSelfLeaveRoom(Kicked);

            ServerMessage LeaveMessage = new ServerMessage(29);
            LeaveMessage.AppendRawInt32(User.VirtualId);
            room.SendMessage(LeaveMessage);

            userlist.Remove(User.InternalRoomID);
            //freeIDs[User.InternalRoomID] = null;
        }


        private void UpdateUserEffect(RoomUser User, int x, int y)
        {
            if (User.IsBot)
                return;
            byte NewCurrentUserItemEffect = room.GetGameMap().EffectMap[x, y];
            if (NewCurrentUserItemEffect > 0)
            {
                ItemEffectType Type = ByteToItemEffectEnum.Parse(NewCurrentUserItemEffect);
                if (Type != User.CurrentItemEffect)
                {
                    switch (Type)
                    {
                        case ItemEffectType.Iceskates:
                            {
                                if (User.GetClient().GetHabbo().Gender == "M")
                                    User.GetClient().GetHabbo().GetAvatarEffectsInventoryComponent().ApplyCustomEffect(38);
                                else
                                    User.GetClient().GetHabbo().GetAvatarEffectsInventoryComponent().ApplyCustomEffect(39);
                                User.CurrentItemEffect = ItemEffectType.Iceskates;
                                break;
                            }

                        case ItemEffectType.Normalskates:
                            {
                                if (User.GetClient().GetHabbo().Gender == "M")
                                {
                                    User.GetClient().GetHabbo().GetAvatarEffectsInventoryComponent().ApplyCustomEffect(55);
                                }
                                else
                                {
                                    User.GetClient().GetHabbo().GetAvatarEffectsInventoryComponent().ApplyCustomEffect(56);
                                }
                                //56=girls
                                //55=
                                User.CurrentItemEffect = Type;
                                break;
                            }
                        case ItemEffectType.Swim:
                            {
                                User.GetClient().GetHabbo().GetAvatarEffectsInventoryComponent().ApplyCustomEffect(29);
                                User.CurrentItemEffect = Type;
                                break;
                            }
                        case ItemEffectType.SwimLow:
                            {
                                User.GetClient().GetHabbo().GetAvatarEffectsInventoryComponent().ApplyCustomEffect(30);
                                User.CurrentItemEffect = Type;
                                break;
                            }
                        case ItemEffectType.SwimHalloween:
                            {
                                User.GetClient().GetHabbo().GetAvatarEffectsInventoryComponent().ApplyCustomEffect(37);
                                User.CurrentItemEffect = Type;
                                break;
                            }
                        case ItemEffectType.None:
                            {
                                User.GetClient().GetHabbo().GetAvatarEffectsInventoryComponent().ApplyCustomEffect(-1);
                                User.CurrentItemEffect = Type;
                                break;
                            }
                        case ItemEffectType.PublicPool:
                            {
                                User.AddStatus("swim", string.Empty);
                                User.CurrentItemEffect = Type;
                                break;
                            }

                    }
                }
            }
            else if (User.CurrentItemEffect != ItemEffectType.None && NewCurrentUserItemEffect == 0)
            {
                User.GetClient().GetHabbo().GetAvatarEffectsInventoryComponent().ApplyEffect(-1);
                User.CurrentItemEffect = ItemEffectType.None;
                User.RemoveStatus("swim");
            }
        }

        internal RoomUser GetUserForSquare(int x, int y)
        {
            return room.GetGameMap().GetRoomUsers(new Point(x, y)).FirstOrDefault();
        }

        internal void AddUserToRoom(GameClient Session, bool Spectator)
        {
            RoomUser User = new RoomUser(Session.GetHabbo().Id, room.RoomId, primaryPrivateUserID++, room, Spectator);
            User.userID = Session.GetHabbo().Id;

            string username = Session.GetHabbo().Username;
            uint userID = User.userID;

            if (usersByUsername.ContainsKey(username.ToLower()))
                usersByUsername.Remove(username.ToLower());

            if (usersByUserID.ContainsKey(userID))
                usersByUserID.Remove(userID);

            usersByUsername.Add(Session.GetHabbo().Username.ToLower(), User);
            usersByUserID.Add(Session.GetHabbo().Id, User);

            int PersonalID = secondaryPrivateUserID++;
            User.InternalRoomID = PersonalID;
            Session.CurrentRoomUserID = PersonalID;

            Session.GetHabbo().CurrentRoomId = room.RoomId;
            UserList.Add(PersonalID, User);
        }

        private void OnUserAdd(object sender, EventArgs args)
        {
            try
            {
                KeyValuePair<int, RoomUser> userPair = (KeyValuePair<int, RoomUser>)sender;
                RoomUser user = userPair.Value;

                if (user == null || user.GetClient() == null || user.GetClient().GetHabbo() == null)
                    return;

                GameClient session = user.GetClient();

                if (session == null || session.GetHabbo() == null)
                    return;

                if (userCount >= room.UsersMax && user.GetClient().GetHabbo().Rank < 4)
                {
                    ServerMessage message = new ServerMessage(224);
                    message.AppendInt32(1);
                    session.SendMessage(message);

                    message = new ServerMessage(18);
                    session.SendMessage(message);

                    return;
                }

                if (session.GetHabbo() != null && session.GetHabbo().userGroup != null)
                {
                    Group group = session.GetHabbo().userGroup;
                    if (!room.groups.ContainsKey(group.groupID))
                    {
                        room.groups.Add(group.groupID, group);

                        ServerMessage message = new ServerMessage(309);
                        message.AppendInt32(1);
                        message.AppendInt32(group.groupID);
                        message.AppendStringWithBreak(group.groupBadge);

                        room.SendMessage(message);
                    }
                }

                if (!user.IsSpectator)
                {
                    DynamicRoomModel Model = room.GetGameMap().Model;
                    if (room.IsPublic && session.SetDoorPos)
                    {
                        user.SetPos(session.newDoorPos.X, session.newDoorPos.Y, room.GetGameMap().StaticModel.SqFloorHeight[session.newDoorPos.X, session.newDoorPos.Y]);
                        user.SetRot(Model.DoorOrientation, false);
                        session.SetDoorPos = false;
                    }
                    else
                    {
                        user.SetPos(Model.DoorX, Model.DoorY, Model.DoorZ);
                        user.SetRot(Model.DoorOrientation, false);
                    }

                    if (room.CheckRights(session, true))
                    {
                        user.AddStatus("flatcrtl", "useradmin");
                    }
                    else if (room.CheckRights(session))
                    {
                        user.AddStatus("flatcrtl", "");
                    }
                    user.CurrentItemEffect = ItemEffectType.None;

                    //UpdateUserEffect(User, User.X, User.Y);

                    if (!user.IsBot && user.GetClient().GetHabbo().IsTeleporting)
                    {
                        RoomItem Item = room.GetRoomItemHandler().GetItem(user.GetClient().GetHabbo().TeleporterId);

                        if (Item != null)
                        {
                            user.SetPos(Item.GetX, Item.GetY, Item.GetZ);
                            user.SetRot(Item.Rot, false);

                            Item.InteractingUser2 = session.GetHabbo().Id;
                            Item.ExtraData = "2";
                            Item.UpdateState(false, true);
                        }
                    }

                    user.GetClient().GetHabbo().IsTeleporting = false;
                    user.GetClient().GetHabbo().TeleporterId = 0;

                    ServerMessage EnterMessage = new ServerMessage(28);
                    EnterMessage.AppendInt32(1);
                    user.Serialize(EnterMessage, room.GetGameMap().gotPublicPool);
                    room.SendMessage(EnterMessage);


                    if (room.Owner != session.GetHabbo().Username && !room.IsPublic)
                    {
                        ButterflyEnvironment.GetGame().GetQuestManager().ProgressUserQuest(user.GetClient(), HabboHotel.Quests.QuestType.SOCIAL_VISIT);
                    }
                }

                if (session.GetHabbo().GetMessenger() != null)
                    session.GetHabbo().GetMessenger().OnStatusChanged(true);


                if (!user.IsSpectator)
                {
                    foreach (RoomUser roomUser in UserList.Values)
                    {
                        if (!user.IsBot)
                            continue;

                        roomUser.BotAI.OnUserEnterRoom(user);
                    }
                }

                user.GetClient().GetMessageHandler().OnRoomUserAdd();

                if (OnUserEnter != null)
                    OnUserEnter(user, null);

                if (room.GotMusicController())
                    room.GetRoomMusicController().OnNewUserEnter(user);
            }
            catch (Exception e)
            {
                Logging.LogCriticalException(e.ToString());
            }

        }

        internal void RequestRoomReload()
        {
            userlist.QueueDelegate(new onCycleDoneDelegate(room.onReload));
        }

        internal void UpdateUserStats(List<RoomUser> users, Hashtable userID, Hashtable userName, int primaryID, int secondaryID)
        {
            foreach (RoomUser user in users)
            {
                userlist.Inner.Add(user.InternalRoomID, user);
            }

            foreach (RoomUser user in userName.Values)
            {
                usersByUsername.Add(user.GetClient().GetHabbo().Username.ToLower(), user);
            }

            foreach (RoomUser user in userID.Values)
            {
                usersByUserID.Add(user.userID, user);
            }

            this.primaryPrivateUserID = primaryID;
            this.secondaryPrivateUserID = secondaryID;
            room.InitPets();
        }

        internal void RemoveUserFromRoom(GameClient Session, Boolean NotifyClient, Boolean NotifyKick)
        {
            try
            {
                if (Session == null)
                    return;

                if (Session.GetHabbo() == null)
                    return;


                Session.GetHabbo().GetAvatarEffectsInventoryComponent().OnRoomExit();

                if (NotifyClient)
                {
                    if (NotifyKick)
                    {
                        Session.GetMessageHandler().GetResponse().Init(33);
                        Session.GetMessageHandler().GetResponse().AppendInt32(4008);
                        Session.GetMessageHandler().SendResponse();
                    }

                    Session.GetMessageHandler().GetResponse().Init(18);
                    Session.GetMessageHandler().SendResponse();
                }

                RoomUser User = GetRoomUserByHabbo(Session.GetHabbo().Id);


                if (User != null)
                {
                    RemoveRoomUser(User);


                    if (Session.GetHabbo() != null)
                    {
                        if (!User.IsSpectator)
                        {
                            if (User.CurrentItemEffect != ItemEffectType.None)
                            {
                                User.GetClient().GetHabbo().GetAvatarEffectsInventoryComponent().CurrentEffect = -1;
                            }
                            //UserMatrix[User.X, User.Y] = false;

                            if (Session.GetHabbo() != null)
                            {
                                if (room.HasActiveTrade(Session.GetHabbo().Id))
                                    room.TryStopTrade(Session.GetHabbo().Id);

                                if (Session.GetHabbo().Username == room.Owner)
                                {
                                    if (room.HasOngoingEvent)
                                    {
                                        room.Event = new RoomEvent(0, null, null, 0, new List<string>());

                                        ServerMessage Message = new ServerMessage(370);
                                        Message.AppendStringWithBreak("-1");
                                        room.SendMessage(Message);

                                        ButterflyEnvironment.GetGame().GetRoomManager().GetEventManager().QueueRemoveEvent(room.RoomData, room.Event.Category);
                                    }
                                }
                                Session.GetHabbo().CurrentRoomId = 0;

                                if (Session.GetHabbo().GetMessenger() != null)
                                    Session.GetHabbo().GetMessenger().OnStatusChanged(true);
                            }

                            //DateTime Start = DateTime.Now;
                            //using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
                            //{
                            //    //TimeSpan TimeUsed1 = DateTime.Now - Start;
                            //    //Logging.LogThreadException("Time used on sys part 2: " + TimeUsed1.Seconds + "s, " + TimeUsed1.Milliseconds + "ms", "");

                            //    //if (Session.GetHabbo() != null)
                            //    //    dbClient.runFastQuery("UPDATE user_roomvisits SET exit_timestamp = '" + ButterflyEnvironment.GetUnixTimestamp() + "' WHERE room_id = '" + this.Id + "' AND user_id = '" + Id + "' ORDER BY exit_timestamp DESC LIMIT 1");
                            //    //dbClient.runFastQuery("UPDATE rooms SET users_now = " + UsersNow + " WHERE id = " + Id);
                            //    //dbClient.runFastQuery("REPLACE INTO room_active VALUES (" + RoomId + ", " + UsersNow + ")");
                            //    dbClient.runFastQuery("UPDATE room_active SET active_users = " + UsersNow);
                            //}
                        }
                    }

                    usersByUserID.Remove(User.userID);
                    if (Session.GetHabbo() != null)
                        usersByUsername.Remove(Session.GetHabbo().Username.ToLower());

                    User.Dispose();
                }
                
            }
            catch (Exception e)
            {
                Logging.LogCriticalException("Error during removing user from room:" + e.ToString());
            }
        }

        private void onRemove(object sender, EventArgs args)
        {
            try
            {
                KeyValuePair<int, RoomUser> removedPair = (KeyValuePair<int, RoomUser>)sender;

                RoomUser user = removedPair.Value;
                GameClient session = user.GetClient();

                int key = removedPair.Key;
                //freeIDs[key] = null;

                List<RoomUser> Bots = new List<RoomUser>();

                foreach (RoomUser roomUser in UserList.Values)
                {
                    if (roomUser.IsBot)
                        Bots.Add(roomUser);
                }

                List<RoomUser> PetsToRemove = new List<RoomUser>();
                foreach (RoomUser Bot in Bots)
                {
                    Bot.BotAI.OnUserLeaveRoom(session);

                    if (Bot.IsPet && Bot.PetData.OwnerId == user.userID && !room.CheckRights(session, true))
                    {
                        PetsToRemove.Add(Bot);
                    }
                }

                foreach (RoomUser toRemove in PetsToRemove)
                {
                    if (user.GetClient() == null || user.GetClient().GetHabbo() == null || user.GetClient().GetHabbo().GetInventoryComponent() == null)
                        continue;

                    user.GetClient().GetHabbo().GetInventoryComponent().AddPet(toRemove.PetData);
                    RemoveBot(toRemove.VirtualId, false);
                }

                room.GetGameMap().RemoveUserFromMap(user, new Point(user.X, user.Y));

            }
            catch (Exception e)
            {
                Logging.LogCriticalException(e.ToString());
            }
        }

        private void RemoveRoomUser(RoomUser user)
        {
            UserList.Remove(user.InternalRoomID);
            user.InternalRoomID = -1;

            room.GetGameMap().GameMap[user.X, user.Y] = user.SqState;
            room.GetGameMap().RemoveUserFromMap(user, new Point(user.X, user.Y));
            ServerMessage LeaveMessage = new ServerMessage(29);
            LeaveMessage.AppendRawInt32(user.VirtualId);
            room.SendMessage(LeaveMessage);
        }

        internal RoomUser GetPet(uint PetId)
        {
            if (pets.ContainsKey(PetId))
                return (RoomUser)pets[PetId];

            return null;
        }

        internal void UpdateUserCount(int count)
        {
            this.userCount = count;
            room.RoomData.UsersNow = count;

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                dbClient.runFastQuery("UPDATE room_active SET active_users = " + count + " WHERE roomid = " + room.RoomId);
            }

            if (room.HasOngoingEvent)
                ButterflyEnvironment.GetGame().GetRoomManager().GetEventManager().QueueUpdateEvent(room.RoomData, room.Event.Category);
            ButterflyEnvironment.GetGame().GetRoomManager().QueueActiveRoomUpdate(room.RoomData);
        }

        internal RoomUser GetRoomUserByVirtualId(int VirtualId)
        {
            return UserList.GetValue(VirtualId);
        }

        internal RoomUser GetRoomUserByHabbo(uint pId)
        {
            if (usersByUserID.ContainsKey(pId))
                return (RoomUser)usersByUserID[pId];

            return null;
        }

        internal List<RoomUser> GetRoomUsers()
        {
            List<KeyValuePair<int, RoomUser>> users = UserList.ToList();

            List<RoomUser> returnList = new List<RoomUser>();
            foreach (KeyValuePair<int, RoomUser> pair in users)
            {
                if (!pair.Value.IsBot)
                    returnList.Add(pair.Value);
            }

            return returnList;
        }

        internal List<RoomUser> GetRoomUserByRank(int minRank)
        {
            List<RoomUser> returnList = new List<RoomUser>();
            foreach (RoomUser user in UserList.Values)
            {
                if (!user.IsBot && user.GetClient() != null && user.GetClient().GetHabbo() != null && user.GetClient().GetHabbo().Rank > minRank)
                    returnList.Add(user);
            }

            return returnList;
        }

        internal RoomUser GetRoomUserByHabbo(string pName)
        {
            if (usersByUsername.ContainsKey(pName.ToLower()))
                return (RoomUser)usersByUsername[pName.ToLower()];

            return null;
        }


        internal void SavePets(IQueryAdapter dbClient)
        {
            try
            {
                if (GetPets().Count > 0)
                {
                    AppendPetsUpdateString(dbClient);
                }
            }
            catch (Exception e)
            {
                Logging.LogCriticalException("Error during saving furniture for room " + room.RoomId + ". Stack: " + e.ToString());
            }
        }

        internal void AppendPetsUpdateString(IQueryAdapter dbClient)
        {
            QueryChunk inserts = new QueryChunk("INSERT INTO user_pets (id,user_id,room_id,name,type,race,color,expirience,energy,createstamp,nutrition,respect,z,y,z) VALUES ");
            QueryChunk updates = new QueryChunk();

            List<uint> petsSaved = new List<uint>();
            foreach (Pet pet in GetPets())
            {
                if (petsSaved.Contains(pet.PetId))
                    continue;

                petsSaved.Add(pet.PetId);
                if (pet.DBState == DatabaseUpdateState.NeedsInsert)
                {
                    inserts.AddParameter(pet.PetId + "name", pet.Name);
                    inserts.AddParameter(pet.PetId + "race", pet.Race);
                    inserts.AddParameter(pet.PetId + "color", pet.Color);
                    inserts.AddQuery("(" + pet.PetId + "," + pet.OwnerId + "," + pet.RoomId + ",@" + pet.PetId + "name," + pet.Type + ",@" + pet.PetId + "race,@" + pet.PetId + "color,0,100,'" + pet.CreationStamp + "',0,0,0,0,0)");
                }
                else if (pet.DBState == DatabaseUpdateState.NeedsUpdate)
                {
                    updates.AddParameter(pet.PetId + "name", pet.Name);
                    updates.AddParameter(pet.PetId + "race", pet.Race);
                    updates.AddParameter(pet.PetId + "color", pet.Color);
                    updates.AddQuery("UPDATE user_pets SET room_id = " + pet.RoomId + ", name = @" + pet.PetId + "name, race = @" + pet.PetId + "race, color = @" + pet.PetId + "color, type = " + pet.Type + ", expirience = " + pet.Expirience + ", " +
                        "energy = " + pet.Energy + ", nutrition = " + pet.Nutrition + ", respect = " + pet.Respect + ", createstamp = '" + pet.CreationStamp + "', x = " + pet.X + ", Y = " + pet.Y + ", Z = " + pet.Z + " WHERE id = " + pet.PetId);
                }

                pet.DBState = DatabaseUpdateState.Updated;
            }

            inserts.Execute(dbClient);
            updates.Execute(dbClient);

            inserts.Dispose();
            updates.Dispose();

            inserts = null;
            updates = null;
        }

        internal List<Pet> GetPets()
        {
            List<KeyValuePair<int, RoomUser>> users = UserList.ToList();

            List<Pet> results = new List<Pet>();
            foreach (KeyValuePair<int, RoomUser> pair in users)
            {
                RoomUser user = pair.Value;
                if (user.IsPet)
                    results.Add(user.PetData);
            }

            return results;
        }

        internal ServerMessage SerializeStatusUpdates(Boolean All)
        {
            List<RoomUser> Users = new List<RoomUser>();

            foreach (RoomUser User in UserList.Values)
            {
                if (!All)
                {
                    if (!User.UpdateNeeded)
                        continue;
                    User.UpdateNeeded = false;
                }

                Users.Add(User);
            }

            if (Users.Count == 0)
                return null;

            ServerMessage Message = new ServerMessage(34);
            Message.AppendInt32(Users.Count);

            foreach (RoomUser User in Users)
                User.SerializeStatus(Message);

            return Message;
        }

        internal void UpdateUserStatusses()
        {
            onCycleDoneDelegate userUpdate = new onCycleDoneDelegate(onUserUpdateStatus);
            UserList.QueueDelegate(userUpdate);
        }

        private void onUserUpdateStatus()
        {
            foreach (RoomUser user in UserList.Values)
                UpdateUserStatus(user, false);
        }

        internal void backupCounters(ref int primaryCounter, ref int secondaryCounter)
        {
            primaryCounter = primaryPrivateUserID;
            secondaryCounter = secondaryPrivateUserID;
        }

        private bool isValid(RoomUser user)
        {
            if (user.IsBot)
                return true;

            if (user.GetClient() == null)
                return false;
            if (user.GetClient().GetHabbo() == null)
            return false;
            if (user.GetClient().GetHabbo().CurrentRoomId != room.RoomId)
                return false;

            return true;
        }

        internal void UpdateUserStatus(RoomUser User, bool cyclegameitems)
        {
            if (User == null)
                return;
            bool isBot = User.IsBot;
            if (isBot)
                cyclegameitems = false;

            if (User.Statusses.ContainsKey("lay") || User.Statusses.ContainsKey("sit"))
            {
                User.Statusses.Remove("lay");
                User.Statusses.Remove("sit");
                User.UpdateNeeded = true;
            }

            //List<RoomItem> ItemsOnSquare = GetFurniObjects(User.X, User.Y);
            CoordItemSearch ItemSearch = new CoordItemSearch(room.GetGameMap().CoordinatedItems);
            List<RoomItem> ItemsOnSquare = ItemSearch.GetAllRoomItemForSquare(User.X, User.Y);
            double newZ = room.GetGameMap().SqAbsoluteHeight(User.X, User.Y, ItemsOnSquare);

            if (newZ != User.Z)
            {
                User.Z = newZ;
                if (User.isFlying)
                    User.Z += 4 + (0.5 * Math.Sin(0.7 * User.flyk));
                User.UpdateNeeded = true;
            }

            DynamicRoomModel Model = room.GetGameMap().Model;
            if (Model.SqState[User.X, User.Y] == SquareState.SEAT)
            {
                if (!User.Statusses.ContainsKey("sit"))
                {
                    User.Statusses.Add("sit", "1.0");
                }

                User.Z = Model.SqFloorHeight[User.X, User.Y];
                if (User.isFlying)
                    User.Z += 4 + (0.5 * Math.Sin(0.7 * User.flyk));
                User.RotHead = Model.SqSeatRot[User.X, User.Y];
                User.RotBody = Model.SqSeatRot[User.X, User.Y];

                User.UpdateNeeded = true;
            }

            foreach (RoomItem Item in ItemsOnSquare)
            {
                if (cyclegameitems)
                {
                    Item.UserWalksOnFurni(User);
                }

                if (Item.GetBaseItem().IsSeat)
                {
                    if (!User.Statusses.ContainsKey("sit"))
                    {
                        User.Statusses.Add("sit", TextHandling.GetString(Item.GetBaseItem().Height));
                    }

                    User.Z = Item.GetZ;
                    if (User.isFlying)
                        User.Z += 4 + (0.5 * Math.Sin(0.7 * User.flyk));
                    User.RotHead = Item.Rot;
                    User.RotBody = Item.Rot;

                    User.UpdateNeeded = true;
                }


                switch (Item.GetBaseItem().InteractionType)
                {
                    case InteractionType.bed:
                        {
                            if (!User.Statusses.ContainsKey("lay"))
                            {
                                User.Statusses.Add("lay", TextHandling.GetString(Item.GetBaseItem().Height) + " null");
                            }

                            User.Z = Item.GetZ;
                            if (User.isFlying)
                                User.Z += 4 + (0.2 * 0.5 * Math.Sin(0.7 * User.flyk));
                            User.RotHead = Item.Rot;
                            User.RotBody = Item.Rot;

                            User.UpdateNeeded = true;
                            break;
                        }

                    case InteractionType.fbgate:
                        {
                            if (cyclegameitems)
                            {
                                if (User.team != Item.team)
                                    User.team = Item.team;

                                else if (User.team == Item.team)
                                    User.team = Team.none;

                                if (!string.IsNullOrEmpty(Item.Figure))
                                {
                                    //User = GetUserForSquare(Item.Coordinate.X, Item.Coordinate.Y);
                                    if (User != null && !User.IsBot)
                                    {
                                        if (User.Coordinate == Item.Coordinate)
                                        {
                                            if (User.GetClient().GetHabbo().Gender != Item.Gender && User.GetClient().GetHabbo().Look != Item.Figure)
                                            {

                                                User.GetClient().GetHabbo().tempGender = User.GetClient().GetHabbo().Gender;
                                                User.GetClient().GetHabbo().tempLook = User.GetClient().GetHabbo().Look;

                                                User.GetClient().GetHabbo().Gender = Item.Gender;
                                                User.GetClient().GetHabbo().Look = Item.Figure;
                                            }
                                            else
                                            {
                                                User.GetClient().GetHabbo().Gender = User.GetClient().GetHabbo().tempGender;
                                                User.GetClient().GetHabbo().Look = User.GetClient().GetHabbo().tempLook;
                                            }

                                            ServerMessage RoomUpdate = new ServerMessage(266);
                                            RoomUpdate.AppendInt32(User.VirtualId);
                                            RoomUpdate.AppendStringWithBreak(User.GetClient().GetHabbo().Look);
                                            RoomUpdate.AppendStringWithBreak(User.GetClient().GetHabbo().Gender.ToLower());
                                            RoomUpdate.AppendStringWithBreak(User.GetClient().GetHabbo().Motto);
                                            room.SendMessage(RoomUpdate);
                                        }
                                    }
                                }
                            }

                            break;
                        }

                    //33: Red
                    //34: Green
                    //35: Blue
                    //36: Yellow

                    case InteractionType.banzaigategreen:
                    case InteractionType.banzaigateblue:
                    case InteractionType.banzaigatered:
                    case InteractionType.banzaigateyellow:
                        {
                            if (cyclegameitems)
                            {
                                int effectID = (int)Item.team + 32;
                                AvatarEffectsInventoryComponent efectmanager = User.GetClient().GetHabbo().GetAvatarEffectsInventoryComponent();

                                if (User.team != Item.team)
                                {
                                    User.team = Item.team;

                                    if (efectmanager.CurrentEffect != effectID)
                                        efectmanager.ApplyCustomEffect(effectID);
                                }
                                else
                                {
                                    if (efectmanager.CurrentEffect == effectID)
                                        efectmanager.ApplyCustomEffect(0);
                                    User.team = Team.none;
                                }
                            }
                            break;
                        }

                    case InteractionType.freezeyellowgate:
                    case InteractionType.freezeredgate:
                    case InteractionType.freezegreengate:
                    case InteractionType.freezebluegate:
                        {
                            if (cyclegameitems)
                            {
                                int effectID = (int)Item.team + 39;
                                AvatarEffectsInventoryComponent efectmanager = User.GetClient().GetHabbo().GetAvatarEffectsInventoryComponent();

                                if (User.team != Item.team)
                                {
                                    User.team = Item.team;

                                    if (efectmanager.CurrentEffect != effectID)
                                        efectmanager.ApplyCustomEffect(effectID);
                                }
                                else
                                {
                                    if (efectmanager.CurrentEffect == effectID)
                                        efectmanager.ApplyCustomEffect(0);
                                    User.team = Team.none;
                                }

                                ServerMessage message = new ServerMessage(700);
                                message.AppendBoolean((User.team != Team.none));

                                User.GetClient().SendMessage(message);
                            }
                            break;
                        }
                    
                    case InteractionType.banzaitele:
                        {
                            room.GetGameItemHandler().onTeleportRoomUserEnter(User, Item);
                            break;
                        }
                }
            }

            if (cyclegameitems)
            {
                if (room.GotSoccer())
                    room.GetSoccer().OnUserWalk(User);

                if (room.GotBanzai())
                    room.GetBanzai().OnUserWalk(User);

                //if (room.GotFreeze())
                    room.GetFreeze().OnUserWalk(User);
            }
        }

        internal void TurnHeads(int X, int Y, uint SenderId)
        {
            foreach (RoomUser user in UserList.Values)
            {
                if (user.HabboId == SenderId)
                    continue;

                user.SetRot(Rotation.Calculate(user.X, user.Y, X, Y), true); 
            }
        }

        private List<RoomUser> ToRemove;

        internal void OnCycle(ref int idleCount)
        {
            ToRemove.Clear();
            int userCounter = 0;

            foreach (RoomUser User in UserList.Values)
            {
                if (!isValid(User))
                {
                    if (User.GetClient() != null)
                        RemoveUserFromRoom(User.GetClient(), false, false);
                    else
                        RemoveRoomUser(User);
                }

                bool updated = false;
                User.IdleTime++;

                if (!User.IsAsleep && User.IdleTime >= 600)
                {
                    User.IsAsleep = true;

                    ServerMessage FallAsleep = new ServerMessage(486);
                    FallAsleep.AppendInt32(User.VirtualId);
                    FallAsleep.AppendBoolean(true);
                    room.SendMessage(FallAsleep);
                }

                if (User.NeedsAutokick && !ToRemove.Contains(User))
                {
                    ToRemove.Add(User);
                    continue;
                }

                if (User.CarryItemID > 0)
                {
                    User.CarryTimer--;
                    if (User.CarryTimer <= 0)
                        User.CarryItem(0);
                }

                if (room.GotFreeze())
                {
                    room.GetFreeze().CycleUser(User);
                }

                if (User.isFlying)
                    User.OnFly();

                if (User.SetStep)
                {
                    if (room.GetGameMap().CanWalk(User.SetX, User.SetY, User.AllowOverride))
                    {
                        room.GetGameMap().UpdateUserMovement(new Point(User.Coordinate.X, User.Coordinate.Y), new Point(User.SetX, User.SetY), User);
                        List<RoomItem> items = room.GetGameMap().GetCoordinatedItems(new Point(User.X, User.Y));

                        User.X = User.SetX;
                        User.Y = User.SetY;
                        User.Z = User.SetZ;
                        if (User.isFlying)
                            User.Z += 4 + 0.5 * Math.Sin(0.7 * User.flyk);
                        
                        foreach (RoomItem item in items)
                        {
                            item.UserWalksOffFurni(User);
                        }

                        if (User.X == room.GetGameMap().Model.DoorX && User.Y == room.GetGameMap().Model.DoorY && !ToRemove.Contains(User) && !User.IsBot)
                        {
                            ToRemove.Add(User);
                            continue;
                        }

                        if (room.IsPublic)
                        {
                            room.GetGameMap().HandleRoomLinks(User);
                        }

                        UpdateUserStatus(User, true);
                    }
                    User.SetStep = false;
                }

                if (User.IsWalking && !User.Freezed)
                {
                    Gamemap map = room.GetGameMap();
                    SquarePoint Point = DreamPathfinder.GetNextStep(User.X, User.Y, User.GoalX, User.GoalY, map.GameMap, map.ItemHeightMap, 
                        map.Model.MapSizeX, map.Model.MapSizeY, User.AllowOverride, map.DiagonalEnabled);
                    User.isSitting = false;
                    if (Point.X == User.X && Point.Y == User.Y) //No path found, or reached goal (:
                    {
                        User.IsWalking = false;
                        User.RemoveStatus("mv");

                        UpdateUserStatus(User, false);
                    }
                    else
                    {
                        int nextX = Point.X;
                        int nextY = Point.Y;

                        User.RemoveStatus("mv");

                        double nextZ = room.GetGameMap().SqAbsoluteHeight(nextX, nextY);

                        if (User.Sitting == true)
                        {

                            User.Statusses.Remove("sit");
                            User.Z += 0.35;
                            User.Sitting = false;
                            User.UpdateNeeded = true;
                        }

                        if (User.LyingDown == true)
                        {

                            User.Statusses.Remove("lay");
                            User.Z += 0.35;
                            User.LyingDown = false;
                            User.UpdateNeeded = true;
                        }
          

                        User.Statusses.Remove("lay");
                        User.Statusses.Remove("sit");
                        if (!User.isFlying)
                            User.AddStatus("mv", nextX + "," + nextY + "," + TextHandling.GetString(nextZ));
                        else
                            User.AddStatus("mv", nextX + "," + nextY + "," + TextHandling.GetString(nextZ + 4 + (0.5 * Math.Sin(0.7 * User.flyk))));

                        int newRot = Rotation.Calculate(User.X, User.Y, nextX, nextY, User.moonwalkEnabled);

                        User.RotBody = newRot;
                        User.RotHead = newRot;

                        User.SetStep = true;
                        User.SetX = nextX;
                        User.SetY = nextY;
                        User.SetZ = nextZ;

                        UpdateUserEffect(User, User.SetX, User.SetY);
                        updated = true;


                        
                        room.GetGameMap().GameMap[User.X, User.Y] = User.SqState; // REstore the old one
                        User.SqState = room.GetGameMap().GameMap[User.SetX, User.SetY];//Backup the new one

                        if (!room.AllowWalkthrough)
                            room.GetGameMap().GameMap[nextX, nextY] = 0;

                    }

                    User.UpdateNeeded = true;
                }
                else
                {
                    if (User.Statusses.ContainsKey("mv"))
                    {
                        User.RemoveStatus("mv");
                        User.UpdateNeeded = true;
                    }
                }


                if (User.IsBot)
                    User.BotAI.OnTimerTick();
                else
                {
                    userCounter++;
                }

                if (!updated)
                    UpdateUserEffect(User, User.X, User.Y);
            }

            if (userCounter == 0)
                idleCount++;


            foreach (RoomUser toRemove in ToRemove)
            {
                GameClient client = ButterflyEnvironment.GetGame().GetClientManager().GetClientByUserID(toRemove.HabboId);
                if (client != null)
                {
                    RemoveUserFromRoom(client, true, false);
                    client.CurrentRoomUserID = -1;
                }
                else
                    RemoveRoomUser(toRemove);
            }

            if (userCount != userCounter)
            {
                UpdateUserCount(userCounter);
            }
        }

        internal void Destroy()
        {
            room = null;
            usersByUsername.Clear();
            usersByUsername = null;
            usersByUserID.Clear();
            usersByUserID = null;
            OnUserEnter = null;
            pets.Clear();
            pets = null;
            userlist.Destroy();
            userlist = null;
        }
    }
}
