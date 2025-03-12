using Butterfly.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Butterfly.HabboHotel.Rooms
{
    public static class RoomHelper
    {
        public static ClonedTable GetThreadSafeTable()
        {
            // Implementazione del metodo per ottenere una tabella thread-safe
            // Puoi personalizzare questa implementazione in base alle tue esigenze specifiche
            return new ClonedTable(
                // Assumendo che tu abbia una Hashtable da clonare
                new Hashtable()); // Sostituisci con la tua hashtable da clonare
        }
    }
}