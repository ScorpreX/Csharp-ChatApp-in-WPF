using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatCommon.Model
{
    public enum MessageType : byte
    {
        Login,
        Logout,
        Register,
        UnicastMessage,
        BroadcastMessage,
        Accept,
        Decline,
        Connected,
        Disconnected,
        ServerStopped
    }
}
