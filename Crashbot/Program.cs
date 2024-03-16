﻿using Steamworks;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;

namespace Crashbot
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Crashbot");
            Environment.CurrentDirectory = Directory.GetParent(Assembly.GetExecutingAssembly()?.Location ?? AppContext.BaseDirectory)!.FullName;
            Console.WriteLine(Environment.CurrentDirectory);

            var steam = new SteamSocket(new SteamKit2.SteamUser.LogOnDetails()
            {
                Username = "tgo_inc",
                Password = CredentialManager.GetPassword(),
            });

            steam.Connect();

            var steamuser = new SteamKit2.SteamID(76561198299556567);
            CSteamID cSteamID = new(steamuser.AccountID);
            var sock = Steamworks.SteamNetworking.CreateP2PConnectionSocket(cSteamID, 1, 0, true);
            var result = Steamworks.SteamNetworking.SendP2PPacket(cSteamID, [1], 1, Steamworks.EP2PSend.k_EP2PSendReliable);

            while (SteamNetworking.IsP2PPacketAvailable(out uint size))
            {
                // allocate buffer and needed variables
                var buffer = new byte[size];

                // read the message into the buffer
                if (SteamNetworking.ReadP2PPacket(buffer, size, out uint bytesRead, out CSteamID remoteId))
                {
                    string message = System.Text.Encoding.UTF8.GetString(buffer, 0, (int)bytesRead);
                    Console.WriteLine("Received a message: " + message);
                }
            }

            Console.WriteLine(result);

            //socket.ReadIncomming();
        }
    }
}
