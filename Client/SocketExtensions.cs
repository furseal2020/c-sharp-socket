using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Sockets;

namespace Client
{
    static class SocketExtensions
    {
        public static bool IsConnected(this Socket socket)
        {
            try
            {
                return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch (SocketException)
            {
                return false;
            }
            catch(ObjectDisposedException)
            {
                return false;
            }
        }
    }
}
