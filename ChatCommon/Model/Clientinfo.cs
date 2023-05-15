using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatCommon.Model
{
    public class Clientinfo
    {
        public TcpClient TcpClient { get; set; }
        public string UserName { get; set; }

        public Clientinfo(TcpClient tcpCLient, string userName)
        {
            this.TcpClient = tcpCLient;
            this.UserName = userName;
        }
    }
}
