﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using Butterfly.Collections;
using Butterfly.Core;
using Butterfly.HabboHotel.GameClients;
using Butterfly.HabboHotel.Items;
using Butterfly.HabboHotel.Pathfinding;
using Butterfly.Messages;
using Database_Manager.Database.Session_Details.Interfaces;
using Butterfly.HabboHotel.Rooms.Games;
using Butterfly.HabboHotel.Rooms.Wired;
using Butterfly.Util;

namespace Butterfly.HabboHotel.Rooms
{
    class RoomItemHandling
    {
        private Room room;

        internal QueuedDictionary<uint, RoomItem> mFloorItems;
        internal QueuedDictionary<uint, RoomItem> mWallItems;

        private Hashtable mRemovedItems;
        private Hashtable mMovedItems;
        private Hashtable mAddedItems;

        internal QueuedDictionary<uint, RoomItem> mRollers;
        private List<uint> rollerItemsMoved;
        private List<uint> rollerUsersMoved;
        private List<ServerMessage> rollerMessages;

        private bool mGotRollers;
        private int mRollerSpeed;
        private int mRoolerCycle;

        private Queue roomItemUpdateQueue;

        internal bool GotRollers
        {
            get
            {
                return mGotRollers;
            }
            set
            {
                mGotRollers = value;
            }
        }

        public RoomItemHandling(Room room)
        {
            this.room = room;

            this.mRemovedItems = new Hashtable();
            this.mMovedItems = new Hashtable();
            this.mAddedItems = new Hashtable();
            this.mRollers = new QueuedDictionary<uint,RoomItem>();

            this.mWallItems = new QueuedDictionary<uint, RoomItem>();
            this.mFloorItems = new QueuedDictionary<uint, RoomItem>();
            this.roomItemUpdateQueue = new Queue();
            this.mGotRollers = false;
            this.mRoolerCycle = 0;
            this.mRollerSpeed = 4;

            rollerItemsMoved = new List<uint>();
            rollerUsersMoved = new List<uint>();
            rollerMessages = new List<ServerMessage>();
        }

        internal void QueueRoomItemUpdate(RoomItem item)
        {
            lock (roomItemUpdateQueue.SyncRoot)
            {
                roomItemUpdateQueue.Enqueue(item);
            }
        }

        internal List<RoomItem> RemoveAllFurniture(GameClient Session)
        {
            List<RoomItem> ReturnList = new List<RoomItem>();
            foreach (RoomItem Item in mFloorItems.Values.ToArray())
            {
                Item.Interactor.OnRemove(Session, Item);
                ServerMessage Message = new ServerMessage(94);
                Message.AppendRawUInt(Item.Id);
                Message.AppendStringWithBreak("");
                Message.AppendBoolean(false);
                room.SendMessage(Message);

                //mFloorItems.Remove(Item.Id);

                ReturnList.Add(Item);
            }

            foreach (RoomItem Item in mWallItems.Values.ToArray())
            {
                Item.Interactor.OnRemove(Session, Item);
                ServerMessage Message = new ServerMessage(84);
                Message.AppendRawUInt(Item.Id);
                Message.AppendStringWithBreak("");
                Message.AppendBoolean(false);
                room.SendMessage(Message);
                //mWallItems.Remove(Item.Id);

                ReturnList.Add(Item);
            }

            mWallItems.Clear();
            mFloorItems.Clear();

            mRemovedItems.Clear();

            mMovedItems.Clear();
            mAddedItems.Clear();
            mRollers.QueueDelegate(new onCycleDoneDelegate(ClearRollers));

            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                dbClient.runFastQuery("DELETE FROM items_rooms WHERE room_id = " + room.RoomId);
            }

            room.GetGameMap().GenerateMaps();
            room.GetRoomUserManager().UpdateUserStatusses();

            if (room.GotWired())
            {
                room.GetWiredHandler().OnPickall();
            }

            return ReturnList;
        }

        private void ClearRollers()
        {
            mRollers.Clear();
        }

        internal void SetSpeed(int p)
        {
            this.mRollerSpeed = p;
        }

        internal void LoadFurniture()
        {
            //this.Items.Clear();
            this.mFloorItems.Clear();
            this.mWallItems.Clear();
            DataTable Data;
            using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
            {
                if (dbClient.dbType == Database_Manager.Database.DatabaseType.MySQL)
                {
                    dbClient.setQuery("CALL getroomitems(@roomid)");
                    dbClient.addParameter("roomid", room.RoomId);
                }
                else
                {
                    dbClient.setQuery("EXECUTE getroomitems " + room.RoomId);
                }

                Data = dbClient.getTable();


                uint itemID;
                decimal x;
                decimal y;
                sbyte n;
                uint baseID;
                string extradata;
                WallCoordinate wallCoord;
                foreach (DataRow dRow in Data.Rows)
                {
                    itemID = Convert.ToUInt32(dRow[0]);
                    x = Convert.ToDecimal(dRow[1]);
                    y = Convert.ToDecimal(dRow[2]);
                    n = Convert.ToSByte(dRow[3]);
                    baseID = Convert.ToUInt32(dRow[4]);
                    if (DBNull.Value.Equals(dRow[5]))
                        extradata = string.Empty;
                    else
                        extradata = (string)dRow[5];

                    if (n > 6) // Is wallitem
                    {
                        wallCoord = new WallCoordinate((double)x, (double)y, n);
                        RoomItem item = new RoomItem(itemID, room.RoomId, baseID, extradata, wallCoord, room);

                        if (!mWallItems.ContainsKey(itemID))
                            mWallItems.Inner.Add(itemID, item);
                    }
                    else //Is flooritem
                    {
                        int coordX, coordY;
                        TextHandling.Split((double)x, out coordX, out coordY);

                        RoomItem item = new RoomItem(itemID, room.RoomId, baseID, extradata, coordX, coordY, (double)y, n, room);
                        if (!mFloorItems.ContainsKey(itemID))
                            mFloorItems.Inner.Add(itemID, item);
                    }
                }

                foreach (RoomItem Item in mFloorItems.Values)
                {
                    if (Item.IsRoller)
                        mGotRollers = true;
                    else if (Item.GetBaseItem().InteractionType == Butterfly.HabboHotel.Items.InteractionType.dimmer)
                    {
                        if (room.MoodlightData == null)
                            room.MoodlightData = new MoodlightData(Item.Id);
                    }
                    else if (WiredUtillity.TypeIsWired(Item.GetBaseItem().InteractionType))
                    {
                        WiredLoader.LoadWiredItem(Item, room, dbClient);
                    }

                    if (WiredHandler.TypeIsWire(Item.GetBaseItem().InteractionType))
                    {
                        room.GetWiredHandler().AddWire(Item, Item.Coordinate, Item.Rot, Item.GetBaseItem().InteractionType);
                    }
                }
            }
        }


        internal RoomItem GetItem(uint pId)
        {
            if (mFloorItems.ContainsKey(pId))
                return mFloorItems.GetValue(pId);
            else if (mWallItems.ContainsKey(pId))
                return mWallItems.GetValue(pId);
            else
                return null;
        }

        internal void RemoveFurniture(GameClient Session, uint pId)
        {
            RoomItem Item = GetItem(pId);

            if (Item == null)
                return;

            if (Item.GetBaseItem().InteractionType == InteractionType.fbgate)
            {
                room.GetSoccer().UnRegisterGate(Item);
            }

            Item.Interactor.OnRemove(Session, Item);
            RemoveRoomItem(Item);

            if (Item.wiredHandler != null)
            {
                using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
                {
                    Item.wiredHandler.DeleteFromDatabase(dbClient);
                    Item.wiredHandler.Dispose();
                    room.GetWiredHandler().RemoveFurniture(Item);
                }
                Item.wiredHandler = null;
            }

            if (Item.wiredCondition != null)
            {
                using (IQueryAdapter dbClient = ButterflyEnvironment.GetDatabaseManager().getQueryreactor())
                {
                    Item.wiredCondition.DeleteFromDatabase(dbClient);
                    Item.wiredCondition.Dispose();
                    room.GetWiredHandler().conditionHandler.ClearTile(Item.Coordinate);
                }
                Item.wiredCondition = null;
            }

        }

        private void RemoveRoomItem(RoomItem Item)
        {
            if (Item.IsWallItem)
            {
                ServerMessage Message = new ServerMessage(84);
                Message.AppendRawUInt(Item.Id);
                Message.AppendStringWithBreak("");
                Message.AppendBoolean(false);
                room.SendMessage(Message);
            }
            else if (Item.IsFloorItem)
            {
                ServerMessage Message = new ServerMessage(94);
                Message.AppendRawUInt(Item.Id);
                Message.AppendStringWithBreak("");
                Message.AppendBoolean(false);
                room.SendMessage(Message);
            }

            

            if (Item.IsWallItem)
                mWallItems.Remove(Item.Id);
            else
            {
                room.GetGameMap().RemoveFromMap(Item);
                mFloorItems.Remove(Item.Id);
            }

            RemoveItem(Item);

            room.GetRoomUserManager().UpdateUserStatusses();

            if (WiredHandler.TypeIsWire(Item.GetBaseItem().InteractionType))
            {
                room.GetWiredHandler().RemoveWiredItem(Item.Coordinate);
            }
        }

        private List<ServerMessage> CycleRollers() // Credit goes to Thor for the roller packets
        {
            mRollers.OnCycle();

            if (mGotRollers)
            {
                if (mRoolerCycle >= mRollerSpeed || mRollerSpeed == 0)
                {
                    rollerItemsMoved.Clear();
                    rollerUsersMoved.Clear();
                    rollerMessages.Clear();

                    List<RoomItem> ItemsOnRoller;
                    List<RoomItem> ItemsOnNext;

                    foreach (RoomItem Item in mRollers.Values)
                    {
                        Point NextCoord = Item.SquareInFront;

                        ItemsOnRoller = room.GetGameMap().GetRoomItemForSquare(Item.GetX, Item.GetY, Item.GetZ);
                        RoomUser UserOnRoller = room.GetRoomUserManager().GetUserForSquare(Item.GetX, Item.GetY);

                        if (ItemsOnRoller.Count > 0 || UserOnRoller != null)
                        {
                            ItemsOnNext = room.GetGameMap().GetCoordinatedItems(NextCoord);

                            double NextZ = 0;
                            int ItemCount = 0;
                            bool NextRoller = false;

                            double NextRollerZ = 0.0;
                            bool NextRollerClear = true;

                            foreach (RoomItem tItem in ItemsOnNext)
                            {
                                if (tItem.IsRoller)
                                {
                                    NextRoller = true;
                                    if (tItem.TotalHeight > NextRollerZ)
                                        NextRollerZ = tItem.TotalHeight;
                                }
                            }

                            if (NextRoller)
                            {
                                foreach (RoomItem tItem in ItemsOnNext)
                                {
                                    if (tItem.TotalHeight > NextRollerZ)
                                        NextRollerClear = false;
                                }
                            }
                            else
                                NextRollerZ = NextRollerZ + room.GetGameMap().GetHeightForSquareFromData(NextCoord);
                            NextZ = NextRollerZ;
                            bool rItemsOnNext = (ItemCount > 0);
                            if (room.GetRoomUserManager().GetUserForSquare(NextCoord.X, NextCoord.Y) != null)
                                rItemsOnNext = true;

                            foreach (RoomItem tItem in ItemsOnRoller)
                            {
                                double AddZ = tItem.GetZ - Item.TotalHeight;
                                if (!rollerItemsMoved.Contains(tItem.Id) && room.GetGameMap().CanRollItemHere(NextCoord.X, NextCoord.Y)
                                    && NextRollerClear && Item.GetZ < tItem.GetZ && room.GetRoomUserManager().GetUserForSquare(NextCoord.X, NextCoord.Y) == null)
                                {
                                    rollerMessages.Add(UpdateItemOnRoller(tItem, NextCoord, Item.Id, NextRollerZ + AddZ));
                                    rollerItemsMoved.Add(tItem.Id);
                                }
                            }

                            if (UserOnRoller != null && !UserOnRoller.IsWalking && NextRollerClear && !rItemsOnNext && room.GetGameMap().CanRollItemHere(NextCoord.X, NextCoord.Y) && room.GetGameMap().GetFloorStatus(NextCoord) != 0)
                            {
                                if (!rollerUsersMoved.Contains(UserOnRoller.HabboId))
                                {
                                    rollerMessages.Add(UpdateUserOnRoller(UserOnRoller, NextCoord, Item.Id, NextZ));
                                    rollerUsersMoved.Add(UserOnRoller.HabboId);
                                }
                            }
                        }
                    }

                    mRoolerCycle = 0;
                    return rollerMessages;
                }
                else
                    mRoolerCycle++;
            }

            return new List<ServerMessage>();
        }

        private ServerMessage UpdateItemOnRoller(RoomItem pItem, Point NextCoord, uint pRolledID, Double NextZ)
        {
            ServerMessage mMessage = new ServerMessage();
            mMessage.Init(230); // Cf
            mMessage.AppendInt32(pItem.GetX);
            mMessage.AppendInt32(pItem.GetY);

            mMessage.AppendInt32(NextCoord.X);
            mMessage.AppendInt32(NextCoord.Y);

            mMessage.AppendInt32(1);

            mMessage.AppendUInt(pItem.Id);

            mMessage.AppendStringWithBreak(TextHandling.GetString(pItem.GetZ));
            mMessage.AppendStringWithBreak(TextHandling.GetString(NextZ));

            mMessage.AppendUInt(pRolledID);

            //room.SendMessage(mMessage);

            //SetFloorItem(null, pItem, NextCoord.X, NextCoord.Y, pItem.Rot, false, true);
            SetFloorItem(pItem, NextCoord.X, NextCoord.Y, NextZ);

            return mMessage;
        }

        internal ServerMessage UpdateUserOnRoller(RoomUser pUser, Point pNextCoord, uint pRollerID, Double NextZ)
        {
            ServerMessage mMessage = new ServerMessage();
            mMessage.Init(230); // Cf
            mMessage.AppendInt32(pUser.X);
            mMessage.AppendInt32(pUser.Y);

            mMessage.AppendInt32(pNextCoord.X);
            mMessage.AppendInt32(pNextCoord.Y);

            mMessage.AppendInt32(0);
            mMessage.AppendUInt(pRollerID);
            mMessage.AppendString("J");
            mMessage.AppendInt32(pUser.VirtualId);
            mMessage.AppendStringWithBreak(TextHandling.GetString(pUser.Z));
            mMessage.AppendStringWithBreak(TextHandling.GetString(NextZ));

            room.GetGameMap().UpdateUserMovement(new Point(pUser.X, pUser.Y), new Point(pNextCoord.X, pNextCoord.Y), pUser);
            room.GetGameMap().GameMap[pUser.X, pUser.Y] = 1;
            pUser.X = pNextCoord.X;
            pUser.Y = pNextCoord.Y;
            pUser.Z = NextZ;
            room.GetGameMap().GameMap[pUser.X, pUser.Y] = 0;

            return mMessage;
        }

        internal void SaveFurnitureToMSSQL(IQueryAdapter dbClient)
        {
            try
            {
                if (mAddedItems.Count > 0 || mRemovedItems.Count > 0 || mMovedItems.Count > 0 || room.GetRoomUserManager().PetCount > 0)
                {
                    QueryChunk standardQueries = new QueryChunk();
                    QueryChunk itemInserts = new QueryChunk(); // REPLACE INTO items_rooms (item_id,room_id,x,y,n) VALUES 
                    QueryChunk extradataInserts = new QueryChunk(); //"REPLACE INTO items_extradata (item_id,data) VALUES "

                    foreach (RoomItem Item in mRemovedItems.Values)
                    {
                        standardQueries.AddQuery("DELETE FROM items_rooms WHERE item_id = " + Item.Id + " AND room_id = " + room.RoomId); //Do join + function
                    }

                    if (mAddedItems.Count > 0)
                    {
                        foreach (RoomItem Item in mAddedItems.Values)
                        {
                            if (!string.IsNullOrEmpty(Item.ExtraData))
                            {
                                extradataInserts.AddQuery("DELETE FROM items_extradata WHERE item_id = " + Item.Id);
                                extradataInserts.AddQuery("INSERT INTO items_extradata (item_id,data) VALUES (" + Item.Id + ",@data_id" + Item.Id + ")");
                                extradataInserts.AddParameter("@data_id" + Item.Id, Item.ExtraData);
                            }

                            itemInserts.AddQuery("DELETE FROM items_rooms WHERE item_id = " + Item.Id);

                            if (Item.IsFloorItem)
                            {
                                double combinedCoords = TextHandling.Combine(Item.GetX, Item.GetY);
                                itemInserts.AddQuery("INSERT INTO items_rooms (item_id,room_id,x,y,n) VALUES (" + Item.Id + "," + Item.RoomId + "," + TextHandling.GetString(combinedCoords) + "," + TextHandling.GetString(Item.GetZ) + "," + Item.Rot + ")");
                            }
                            else
                            {
                                itemInserts.AddQuery("INSERT INTO items_rooms (item_id,room_id,x,y,n) VALUES (" + Item.Id + "," + Item.RoomId + "," + TextHandling.GetString(Item.wallCoord.GetXValue()) + "," + TextHandling.GetString(Item.wallCoord.GetYValue()) + "," + Item.wallCoord.n() + ")");
                            }
                        }
                    }


                    foreach (RoomItem Item in mMovedItems.Values)
                    {
                        if (!string.IsNullOrEmpty(Item.ExtraData))
                        {
                            standardQueries.AddQuery("UPDATE items_extradata SET data = @data" + Item.Id + " WHERE item_id = " + Item.Id);
                            standardQueries.AddParameter("data" + Item.Id, Item.ExtraData);
                        }

                        if (Item.IsWallItem)
                        {
                            standardQueries.AddQuery("UPDATE items_rooms SET x=" + TextHandling.GetString(Item.wallCoord.GetXValue()) + ", y=" + TextHandling.GetString(Item.wallCoord.GetYValue()) + ", n=" + Item.wallCoord.n() + " WHERE item_id = " + Item.Id);
                        }
                        else
                        {
                            double combinedCoords = TextHandling.Combine(Item.GetX, Item.GetY);
                            standardQueries.AddQuery("UPDATE items_rooms SET x=" + TextHandling.GetString(combinedCoords) + ", y=" + TextHandling.GetString(Item.GetZ) + ", n=" + Item.Rot + " WHERE item_id = " + Item.Id);
                        }
                    }

                    room.GetRoomUserManager().AppendPetsUpdateString(dbClient);

                    mAddedItems.Clear();
                    mRemovedItems.Clear();
                    mMovedItems.Clear();

                    standardQueries.Execute(dbClient);
                    itemInserts.Execute(dbClient);
                    extradataInserts.Execute(dbClient);

                    standardQueries.Dispose();
                    itemInserts.Dispose();
                    extradataInserts.Dispose();

                    standardQueries = null;
                    itemInserts = null;
                    extradataInserts = null;
                }
            }
            catch (Exception e)
            {
                Logging.LogCriticalException("Error during saving furniture for room " + room.RoomId + ". Stack: " + e.ToString());
            }
        }

        internal void SaveFurniture(IQueryAdapter dbClient)
        {
            if (dbClient.dbType == Database_Manager.Database.DatabaseType.MSSQL)
            {
                SaveFurnitureToMSSQL(dbClient);
                return;
            }

            try
            {
                if (mAddedItems.Count > 0 || mRemovedItems.Count > 0 || mMovedItems.Count > 0 || room.GetRoomUserManager().PetCount > 0)
                {
                    QueryChunk standardQueries = new QueryChunk();
                    QueryChunk itemInserts = new QueryChunk("REPLACE INTO items_rooms (item_id,room_id,x,y,n) VALUES ");
                    QueryChunk extradataInserts = new QueryChunk("REPLACE INTO items_extradata (item_id,data) VALUES ");

                    foreach (RoomItem Item in mRemovedItems.Values)
                    {
                        standardQueries.AddQuery("DELETE FROM items_rooms WHERE item_id = " + Item.Id + " AND room_id = " + room.RoomId); //Do join + function
                    }

                    if (mAddedItems.Count > 0)
                    {
                        foreach (RoomItem Item in mAddedItems.Values)
                        {
                            if (!string.IsNullOrEmpty(Item.ExtraData))
                            {
                                extradataInserts.AddQuery("(" + Item.Id + ",@data_id" + Item.Id + ")");
                                extradataInserts.AddParameter("@data_id" + Item.Id, Item.ExtraData);
                            }

                            if (Item.IsFloorItem)
                            {
                                double combinedCoords = TextHandling.Combine(Item.GetX, Item.GetY);
                                itemInserts.AddQuery("(" + Item.Id + "," + Item.RoomId + "," + TextHandling.GetString(combinedCoords) + "," + TextHandling.GetString(Item.GetZ) + "," + Item.Rot + ")");
                            }
                            else
                            {
                                itemInserts.AddQuery("(" + Item.Id + "," + Item.RoomId + "," + TextHandling.GetString(Item.wallCoord.GetXValue()) + "," + TextHandling.GetString(Item.wallCoord.GetYValue()) + "," + Item.wallCoord.n() + ")");
                            }
                        }
                    }


                    foreach (RoomItem Item in mMovedItems.Values)
                    {
                        if (!string.IsNullOrEmpty(Item.ExtraData))
                        {
                            standardQueries.AddQuery("UPDATE items_extradata SET data = @data" + Item.Id + " WHERE item_id = " + Item.Id);
                            standardQueries.AddParameter("data" + Item.Id, Item.ExtraData);
                        }

                        if (Item.IsWallItem)
                        {
                            standardQueries.AddQuery("UPDATE items_rooms SET x=" + TextHandling.GetString(Item.wallCoord.GetXValue()) + ", y=" + TextHandling.GetString(Item.wallCoord.GetYValue()) + ", n=" + Item.wallCoord.n() + " WHERE item_id = " + Item.Id);
                        }
                        else
                        {
                            double combinedCoords = TextHandling.Combine(Item.GetX, Item.GetY);

                            standardQueries.AddQuery("UPDATE items_rooms SET x=" + TextHandling.GetString(combinedCoords) + ", y=" + TextHandling.GetString(Item.GetZ) + ", n=" + Item.Rot + " WHERE item_id = " + Item.Id);
                        }
                    }

                    room.GetRoomUserManager().AppendPetsUpdateString(dbClient);

                    mAddedItems.Clear();
                    mRemovedItems.Clear();
                    mMovedItems.Clear();

                    standardQueries.Execute(dbClient);
                    itemInserts.Execute(dbClient);
                    extradataInserts.Execute(dbClient);

                    standardQueries.Dispose();
                    itemInserts.Dispose();
                    extradataInserts.Dispose();

                    standardQueries = null;
                    itemInserts = null;
                    extradataInserts = null;
                }
            }
            catch (Exception e)
            {
                Logging.LogCriticalException("Error during saving furniture for room " + room.RoomId + ". Stack: " + e.ToString());
            }
        }

        internal bool SetFloorItem(GameClient Session, RoomItem Item, int newX, int newY, int newRot, bool newItem, bool OnRoller, bool sendMessage)
        {
            return SetFloorItem(Session, Item, newX, newY, newRot, newItem, OnRoller, sendMessage, true);
        }

        internal bool SetFloorItem(GameClient Session, RoomItem Item, int newX, int newY, int newRot, bool newItem, bool OnRoller, bool sendMessage, bool updateRoomUserStatuses)
        {
            bool NeedsReAdd = false;
            if (!newItem)
                NeedsReAdd = room.GetGameMap().RemoveFromMap(Item);
            Dictionary<int, ThreeDCoord> AffectedTiles = Gamemap.GetAffectedTiles(Item.GetBaseItem().Length, Item.GetBaseItem().Width, newX, newY, newRot);

            if (!room.GetGameMap().ValidTile(newX, newY) || room.GetGameMap().SquareHasUsers(newX, newY) && !Item.GetBaseItem().IsSeat)
            {
                if (NeedsReAdd)
                {
                    AddItem(Item);
                    room.GetGameMap().AddToMap(Item);
                }
                return false;
            }

            foreach (ThreeDCoord Tile in AffectedTiles.Values)
            {
                if (!room.GetGameMap().ValidTile(Tile.X, Tile.Y) || room.GetGameMap().SquareHasUsers(Tile.X, Tile.Y) && !Item.GetBaseItem().IsSeat)
                {
                    if (NeedsReAdd)
                    {
                        AddItem(Item);
                        room.GetGameMap().AddToMap(Item);
                    }
                    return false;
                }
            }

            

            // Start calculating new Z coordinate
            Double newZ = room.GetGameMap().Model.SqFloorHeight[newX, newY];
            DynamicRoomModel Model = room.GetGameMap().Model;
            foreach (RoomUser user in room.GetGameMap().GetRoomUsers(new Point(newX, newY)))
            {
                if (!user.Statusses.ContainsKey("sit"))
                {
                    user.Statusses.Add("sit", "1.0");
                }

                user.Z = Model.SqFloorHeight[user.X, user.Y];
                if (user.isFlying)
                    user.Z += 4 + (0.5 * Math.Sin(0.7 * user.flyk));
                user.RotHead = Model.SqSeatRot[user.X, user.Y];
                user.RotBody = Model.SqSeatRot[user.X, user.Y];
            }

            if (!OnRoller)
            {
                // Is the item trying to stack on itself!?
                //if (Item.Rot == newRot && Item.GetX == newX && Item.GetY == newY && Item.GetZ != newZ)
                //{
                //    if (NeedsReAdd)
                //        AddItem(Item);
                //    return false;
                //}

                // Make sure this tile is open and there are no users here
                if (room.GetGameMap().Model.SqState[newX, newY] != SquareState.OPEN)
                {
                    if (NeedsReAdd)
                    {
                        AddItem(Item);
                        room.GetGameMap().AddToMap(Item);
                    }
                    return false;
                }

                foreach (ThreeDCoord Tile in AffectedTiles.Values)
                {
                    if (room.GetGameMap().Model.SqState[Tile.X, Tile.Y] != SquareState.OPEN)
                    {
                        if (NeedsReAdd)
                        {
                            AddItem(Item);
                            room.GetGameMap().AddToMap(Item);
                        }
                        return false;
                    }
                }

                // And that we have no users
                if (!Item.GetBaseItem().IsSeat && !Item.IsRoller)
                {
                    foreach (ThreeDCoord Tile in AffectedTiles.Values)
                    {
                        if (room.GetGameMap().GetRoomUsers(new Point(Tile.X, Tile.Y)).Count > 0)
                        {
                            if (NeedsReAdd)
                            {
                                AddItem(Item);
                                room.GetGameMap().AddToMap(Item);
                            }
                            return false;
                        }
                    }
                }
            }

            // Find affected objects
            List<RoomItem> ItemsOnTile = GetFurniObjects(newX, newY);
            List<RoomItem> ItemsAffected = new List<RoomItem>();
            List<RoomItem> ItemsComplete = new List<RoomItem>();

            foreach (ThreeDCoord Tile in AffectedTiles.Values)
            {
                List<RoomItem> Temp = GetFurniObjects(Tile.X, Tile.Y);

                if (Temp != null)
                {
                    ItemsAffected.AddRange(Temp);
                }
            }


            ItemsComplete.AddRange(ItemsOnTile);
            ItemsComplete.AddRange(ItemsAffected);

            if (!OnRoller)
            {
                // Check for items in the stack that do not allow stacking on top of them
                foreach (RoomItem I in ItemsComplete)
                {
                    if (I == null)
                        continue;

                    if (I.Id == Item.Id)
                    {
                        continue;
                    }

                    if (I.GetBaseItem() == null)
                        continue;

                    if (!I.GetBaseItem().Stackable)
                    {
                        if (NeedsReAdd)
                        {
                            AddItem(Item);
                            room.GetGameMap().AddToMap(Item);
                        }
                        return false;
                    }
                }
            }

            //if (!Item.IsRoller)
            {
                // If this is a rotating action, maintain item at current height
                if (Item.Rot != newRot && Item.GetX == newX && Item.GetY == newY)
                {
                    newZ = Item.GetZ;
                }

                // Are there any higher objects in the stack!?
                foreach (RoomItem I in ItemsComplete)
                {
                    if (I.Id == Item.Id)
                    {
                        continue; // cannot stack on self
                    }

                    if (I.TotalHeight > newZ)
                    {
                        newZ = I.TotalHeight;
                    }
                }
            }

            // Verify the rotation is correct
            if (newRot != 0 && newRot != 2 && newRot != 4 && newRot != 6 && newRot != 8)
            {
                newRot = 0;
            }

            //Item.GetX = newX;
            //Item.GetY = newY;
            //Item.GetZ = newZ;


            Item.Rot = newRot;
            int oldX = Item.GetX;
            int oldY = Item.GetY;
            Item.SetState(newX, newY, newZ, AffectedTiles);

            if (!OnRoller && Session != null)
                Item.Interactor.OnPlace(Session, Item);


            if (newItem)
            {
                if (mFloorItems.ContainsKey(Item.Id))
                {
                    if (Session != null)
                        Session.SendNotif(LanguageLocale.GetValue("room.itemplaced"));

                    //Remove from map!!!
                    return true;
                }

                //using (DatabaseClient dbClient = ButterflyEnvironment.GetDatabase().GetClient())
                //{
                //    dbClient.addParameter("extra_data", Item.ExtraData);
                //    dbClient.runFastQuery("INSERT INTO room_items (id,room_id,base_item,extra_data,x,y,z,rot,wall_pos) VALUES ('" + Item.Id + "','" + RoomId + "','" + Item.BaseItem + "',@extra_data,'" + Item.GetX + "','" + Item.GetY + "','" + Item.GetZ + "','" + Item.Rot + "','')");
                //}
                //if (mRemovedItems.ContainsKey(Item.Id))
                //    mRemovedItems.Remove(Item.Id);
                //if (mAddedItems.ContainsKey(Item.Id))
                //    return false;

                //mAddedItems.Add(Item.Id, Item);

                if (Item.IsFloorItem && !mFloorItems.ContainsKey(Item.Id))
                    mFloorItems.Add(Item.Id, Item);
                else if (Item.IsWallItem && !mWallItems.ContainsKey(Item.Id))
                    mWallItems.Add(Item.Id, Item);

                AddItem(Item);

                if (sendMessage)
                {
                    ServerMessage Message = new ServerMessage(93);
                    Item.Serialize(Message);
                    room.SendMessage(Message);
                }
            }
            else
            {
                //using (DatabaseClient dbClient = ButterflyEnvironment.GetDatabase().GetClient())
                //{
                //    dbClient.runFastQuery("UPDATE room_items SET x = '" + Item.GetX + "', y = '" + Item.GetY + "', z = '" + Item.GetZ + "', rot = '" + Item.Rot + "', wall_pos = '' WHERE id = '" + Item.Id + "' LIMIT 1");
                //}
                UpdateItem(Item);

                if (!OnRoller && sendMessage)
                {
                    ServerMessage Message = new ServerMessage(95);
                    Item.Serialize(Message);
                    room.SendMessage(Message);
                }
            }

            if (!newItem)
            {
                room.GetWiredHandler().RemoveWiredItem(new System.Drawing.Point(oldX, oldY));

                if (WiredHandler.TypeIsWire(Item.GetBaseItem().InteractionType))
                {
                    room.GetWiredHandler().AddWire(Item, new System.Drawing.Point(newX, newY), newRot, Item.GetBaseItem().InteractionType);
                }
            }
            else
            {
                if (WiredHandler.TypeIsWire(Item.GetBaseItem().InteractionType))
                {
                    room.GetWiredHandler().AddWire(Item, Item.Coordinate, newRot, Item.GetBaseItem().InteractionType);
                }
            }

            //GenerateMaps(false);
            room.GetGameMap().AddToMap(Item);

            if (updateRoomUserStatuses)
                room.GetRoomUserManager().UpdateUserStatusses();

            return true;
        }

        internal List<RoomItem> GetFurniObjects(int X, int Y) 
        {

            return room.GetGameMap().GetCoordinatedItems(new Point(X, Y));
            //List<RoomItem> Results = new List<RoomItem>();

            //foreach (RoomItem Item in mFloorItems.Values)
            //{
            //    if (Item.GetX == X && Item.GetY == Y)
            //    {
            //        Results.Add(Item);
            //    }

            //    Dictionary<int, ThreeDCoord> PointList = Item.GetAffectedTiles; //GetAffectedTiles(Item.GetBaseItem().Length, Item.GetBaseItem().Width, Item.GetX, Item.GetY, Item.Rot);

            //    foreach (ThreeDCoord Tile in PointList.Values)
            //    {
            //        if (Tile.X == X && Tile.Y == Y)
            //        {
            //            Results.Add(Item);
            //        }
            //    }
            //}
            //return Results;
        }

        internal bool SetFloorItem(RoomItem Item, int newX, int newY, Double newZ)
        {
            room.GetGameMap().RemoveFromMap(Item);
            Item.SetState(newX, newY, newZ, Gamemap.GetAffectedTiles(Item.GetBaseItem().Length, Item.GetBaseItem().Width, newX, newY, Item.Rot));

            UpdateItem(Item);
            room.GetGameMap().AddItemToMap(Item);

            return true;
        }

        internal bool SetWallItem(GameClient Session, RoomItem Item)
        {
            if (!Item.IsWallItem || mWallItems.ContainsKey(Item.Id))
                return false;
            if (mFloorItems.ContainsKey(Item.Id))
            {
                Session.SendNotif(LanguageLocale.GetValue("room.itemplaced"));
                return true;
            }
            Item.Interactor.OnPlace(Session, Item);

            if (Item.GetBaseItem().InteractionType == InteractionType.dimmer)
            {
                if (room.MoodlightData == null)
                {
                    room.MoodlightData = new MoodlightData(Item.Id);
                    Item.ExtraData = room.MoodlightData.GenerateExtraData();
                }
            }

            mWallItems.Add(Item.Id, Item);
            AddItem(Item);

            ServerMessage Message = new ServerMessage(83);
            Item.Serialize(Message);
            room.SendMessage(Message);

            return true;
        }
        internal void UpdateItem(RoomItem item)
        {
            if (mAddedItems.ContainsKey(item.Id))
                return;
            if (mRemovedItems.ContainsKey(item.Id))
                mRemovedItems.Remove(item.Id);
            if (!mMovedItems.ContainsKey(item.Id))
                mMovedItems.Add(item.Id, item);
        }

        internal void AddItem(RoomItem item)
        {
            if (mRemovedItems.ContainsKey(item.Id))
                mRemovedItems.Remove(item.Id);
            if (!mMovedItems.ContainsKey(item.Id) && !mAddedItems.ContainsKey(item.Id))
                mAddedItems.Add(item.Id, item);
        }

        internal void RemoveItem(RoomItem item)
        {
            if (mAddedItems.ContainsKey(item.Id))
                mAddedItems.Remove(item.Id);

            if (mMovedItems.ContainsKey(item.Id))
                mMovedItems.Remove(item.Id);
            if (!mRemovedItems.ContainsKey(item.Id))
                mRemovedItems.Add(item.Id, item);
            mRollers.Remove(item.Id);
        }

        internal void OnCycle()
        {
            if (mGotRollers)
            {
                try
                {
                    room.SendMessage(CycleRollers());
                }
                catch (Exception e)
                {
                    Logging.LogThreadException(e.ToString(), "rollers for room with ID " + room.RoomId);
                    mGotRollers = false;
                }
            }

            if (roomItemUpdateQueue.Count > 0)
            {
                List<RoomItem> addItems = new List<RoomItem>();
                lock (roomItemUpdateQueue.SyncRoot)
                {
                    while (roomItemUpdateQueue.Count > 0)
                    {
                        RoomItem item = (RoomItem)roomItemUpdateQueue.Dequeue();
                        item.ProcessUpdates();

                        if (item.IsTrans || item.UpdateCounter > 0)
                            addItems.Add(item);
                    }

                    foreach (RoomItem item in addItems)
                    {
                        roomItemUpdateQueue.Enqueue(item);
                    }
                }
            }

            mFloorItems.OnCycle();
            mWallItems.OnCycle();
        }

        internal void Destroy()
        {
            mFloorItems.Clear();
            mWallItems.Clear();
            mRemovedItems.Clear();
            mMovedItems.Clear();
            mAddedItems.Clear();
            roomItemUpdateQueue.Clear();

            room = null;
            mFloorItems = null;
            mWallItems = null;
            mRemovedItems = null;
            mMovedItems = null;
            mAddedItems = null;
            mWallItems = null;
            roomItemUpdateQueue = null;
        }
    }
}
