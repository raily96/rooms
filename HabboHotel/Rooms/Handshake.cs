
using Butterfly.Core;
using System;
namespace Butterfly.Messages
{
    partial class GameClientMessageHandler
    {
        internal void SendSessionParams()
        {
            Response.Init(257);
            Response.AppendInt32(9);
            Response.AppendInt32(0);
            Response.AppendInt32(0);
            Response.AppendInt32(1);
            Response.AppendInt32(1);
            Response.AppendInt32(3);
            Response.AppendInt32(0);
            Response.AppendInt32(2);
            Response.AppendInt32(1);
            Response.AppendInt32(4);
            Response.AppendInt32(1);
            Response.AppendInt32(5);
            Response.AppendStringWithBreak("dd-MM-yyyy");
            Response.AppendInt32(7);
            Response.AppendBoolean(false);
            Response.AppendInt32(8);
            Response.AppendStringWithBreak("/client");
            Response.AppendInt32(9);
            Response.AppendBoolean(false);

            SendResponse();
        }

        internal void SSOLogin()
        {
            Session.tryLogin(Request.PopFixedString());
        }

        //internal void RegisterHandshake()
        //{
        //    RequestHandlers.Add(206, new RequestHandler(SendSessionParams));
        //    RequestHandlers.Add(415, new RequestHandler(SSOLogin));
        //}

        //internal void UnRegisterHandshake()
        //{
        //    RequestHandlers.Remove(206);
        //    RequestHandlers.Remove(415);
        //}
    }
}
