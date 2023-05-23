using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using MultiplayerSFS.GUI;
using MultiplayerSFS.Networking.Packets;

namespace MultiplayerSFS.Networking
{
    public class JoinException : Exception
    {
        public JoinResponsePacket.JoinResponse response;
        public JoinException(JoinResponsePacket.JoinResponse response)
        {
            this.response = response;
        }

        public override string ToString()
        {
            switch (response)
            {
                case JoinResponsePacket.JoinResponse.Denied_UsernameAlreadyInUse:
                    return "Username already in use.";
                case JoinResponsePacket.JoinResponse.Denied_IncorrectPassword:
                    return "Incorrect password";
                case JoinResponsePacket.JoinResponse.Accepted:
                    return "Success";
                default:
                    return "";

            }
        }
    }

    public class Client
    {
        public Socket connection = null;
        public Exception exception = null;
        public bool Successful => connection != null;

        public Client(Socket client)
        {
            this.connection = client;
        }
        public Client(Exception exception)
        {
            this.exception = exception;
        }

        public static async Task<Client> RequestConnection(JoinInfo joinInfo)
        {
            try
            {
                JoinPacket joinPacket = joinInfo.ToPacket();
                TcpClient client = new TcpClient();
                await client.ConnectAsync(joinInfo.ipAddress, joinInfo.port);
                client.Client.SendPacket(PacketType.Client_Join, joinPacket);
                    
                Packet packet = await client.Client.RecievePacket();

                switch (joinResponse.joinResponse)
                {
                    case JoinResponsePacket.JoinResponse.Accepted:
                        return new Client(client.Client);
                    case JoinResponsePacket.JoinResponse.Denied_UsernameAlreadyInUse:
                        throw new Exception("Username already in use.");
                    case JoinResponsePacket.JoinResponse.Denied_IncorrectPassword:
                        throw new Exception("Incorrect password.");
                }
            }
            catch (Exception e)
            {
                return new Client(e);
            }
            return new Client(new Exception("Huh?"));
        }
    }
}