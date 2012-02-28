﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;

namespace ModUpdater.Server
{
    class Server
    {
        public List<Mod> Mods { get; private set; }
        public List<Client> Clients { get; private set; }
        public TcpListener TcpServer { get; private set; }
        public TcpListener TcpServer2 { get; private set; }
        public IPAddress Address { get; private set; }
        bool Online { get; set; }
        public Server()
        {
            Mods = new List<Mod>();
            Clients = new List<Client>();
            TcpServer = new TcpListener(IPAddress.Any, 4713);
            if (Directory.Exists(@"clientmods\xml")) Directory.Move(@"clientmods\xml", "xml");
            if (Directory.Exists(@"clientmods\config")) Directory.Move(@"clientmods\config", "config");
            if (Directory.Exists("clientmods")) Directory.Move("clientmods", "mods");
            if (!Directory.Exists("mods")) Directory.CreateDirectory("mods");
            if (!Directory.Exists("xml")) Directory.CreateDirectory("xml");
            if (!Directory.Exists("config")) Directory.CreateDirectory("config");
            foreach (string s in Directory.GetFiles("xml"))
            {
                Mods.Add(new Mod(s));
            }
            foreach (Mod m in Mods)
            {
                //Console.WriteLine(m.ToString());
            }
            Console.WriteLine("Registered {0} mods", Mods.Count);
        }
        public void Start()
        {
            Address = IPAddress.Loopback;
            try
            {
                string direction = "";
                WebRequest request = WebRequest.Create("http://checkip.dyndns.org/");
                using (WebResponse response = request.GetResponse())
                {
                    using (StreamReader stream = new StreamReader(response.GetResponseStream()))
                    {
                        direction = stream.ReadToEnd();
                    }
                }
                int first = direction.IndexOf("Address: ") + 9;
                int last = direction.LastIndexOf("</body>");
                direction = direction.Substring(first, last - first);
                Address = IPAddress.Parse(direction);
            }
            catch (Exception e) { MinecraftModUpdater.Logger.Log(e); }
            Console.WriteLine("Server IP Address is: " + Address.ToString());
            TcpServer.Start();
            Online = true;
            TaskManager.AddDelayedAsyncTask(delegate { SimpleConsoleImputHandler(); }, 3000);
            Receve();
        }
        public void Dispose()
        {
            TcpServer.Stop();
            Online = false;
        }
        public void Receve()
        {
            while (Online)
            {
                try
                {
                    Socket s = TcpServer.AcceptSocket();
                    TaskManager.AddAsyncTask(delegate
                    {
                        AcceptClient(s);
                    });
                }
                catch { }
            }
        }
        public void AcceptClient(Socket s)
        {
            Client c = new Client(s, this);
            Clients.Add(c);
            c.StartListening();
            Thread.Sleep(1000);
            while (s.Connected) Thread.Sleep(100);
            c.Dispose();
            Clients.Remove(c);
        }
        public void SimpleConsoleImputHandler()
        {
            Console.WriteLine("Simple Console Input Handler is online and ready.  \r\nEnter \"help\" for a list of commands.");
            while (Online)
            {
                switch (Console.ReadLine())
                {
                    case "connected":
                        Console.WriteLine(Clients.ToArray().ToString());
                        break;
                    case "exit":
                    case "stop":

                        TaskManager.AddAsyncTask(
                            delegate
                            {
                                if (Clients.Count > 0) Console.WriteLine("Waiting for {0} clients to exit.", Clients.Count);
                                while (Clients.Count > 0) ;
                                Dispose();
                            });
                        break;
                    case "help":
                    case "?":
                    default:
                        Console.WriteLine("exit, stop - Safely stops the update server after all clients exit.");
                        Console.WriteLine("connected - Shows a list of connected clients.");
                        break;
                }
            }
        }
    }
}
