﻿//    File:        MainForm.cs
//    Copyright:   Copyright (C) 2012 Christian Wilson. All rights reserved.
//    Website:     https://github.com/seaboy1234/Minecraft-Mod-Updater
//    Description: This is intended to help Minecraft server owners who use mods make the experience of adding new mods and updating old ones easier for everyone.
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using ModUpdater.Utility;
using ModUpdater.Net;
using System.Text;

namespace ModUpdater.Client.GUI
{
    public partial class MainForm : Form
    {
        public static MainForm Instance { get; private set; }
        public ModFile CurrentDownload;
        public IPAddress LocalAddress;
        public string ServerFolder { get { return serverName.Replace(' ', '_').Replace('.', '-').ToLower(); } }
        public delegate void Void();
        private PacketHandler ph;
        private Socket socket;
        private List<string> ModFiles = new List<string>();
        private List<Mod> Mods = new List<Mod>();
        private string[] PostDownload;
        private bool ServerShutdown = false;
        private string serverName = "";
        private float serverFontSize = 36;
        private ImageList modImages;
        private bool warnDisconnect = true;
        public MainForm()
        {
            if (Instance == null) Instance = this;
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            if (lsModsToUpdate.Items.Count == 0)
            {
                if (Properties.Settings.Default.LaunchAfterUpdate)
                {
                    SplashScreen.CloseSplashScreen();
                    Hide();
                    Program.StartMinecraft();
                }
                try
                {
                    Invoke(new Void(delegate
                    {
                        Close();
                    }));
                }
                catch { }
                return;
            }
            if (!Properties.Settings.Default.AutoUpdate)
            {
                if (MessageBox.Show("Are you sure you want to update " + lsModsToUpdate.Items.Count + " mods and delete " + lsModsToDelete.Items.Count + " more?", "Confirm Update Action", MessageBoxButtons.YesNo) != System.Windows.Forms.DialogResult.Yes)
                {
                    return;
                }
                TaskManager.AddAsyncTask(delegate
                {
                    SplashScreen.ShowSplashScreen();                    
                });
            }
            if(Properties.Settings.Default.FirstRun)
                Program.UpdateMinecraft();
            TaskManager.AddDelayedAsyncTask(delegate
            {
                SplashScreen.UpdateStatusText("Downloading Updates...");
                SplashScreen.GetScreen().Invoke(new Void(delegate
                {
                    SplashScreen.GetScreen().label2.Font.Dispose();
                    SplashScreen.GetScreen().label2.Font = new Font(FontFamily.GenericSansSerif, serverFontSize);
                    SplashScreen.GetScreen().label2.Text = serverName;
                }));
            }, 300);
            TaskManager.AddDelayedAsyncTask(delegate
            {
                foreach (object o in lsModsToDelete.Items)
                {
                    string m = (string)o;
                    string path = Properties.Settings.Default.MinecraftPath + "\\" + Path.GetDirectoryName(m) + Path.GetFileName(m).TrimEnd('\\').Replace("clientmods", "mods");
                    File.Delete(Properties.Settings.Default.MinecraftPath + @"\mods\" + Path.GetFileName(m));
                }
                foreach (Mod m in Mods)
                {
                    if (lsModsToUpdate.Items.Contains(m))
                    {
                        Packet.Send(new RequestModPacket { Type = RequestModPacket.RequestType.Download, FileName = m.File }, ph.Stream);
                        Packet.Send(new RequestModPacket { Type = RequestModPacket.RequestType.Config, FileName = m.File }, ph.Stream);
                    }
                }
            }, 1000);
            Hide();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            TaskManager.AddAsyncTask(delegate
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
                LocalAddress = IPAddress.Parse(direction);
            });
            Debug.Assert("Debug mode is enabled.  In-depth messages will be displayed.");
            if (ProgramOptions.Debug)
            {
                MinecraftModUpdater.Logger.Log(Logger.Level.Warning, "Client is running in debug-mode.");
            }
            if (ProgramOptions.CommandLine)
            {
                MinecraftModUpdater.Logger.Log(Logger.Level.Warning, "Client is running in commandline.");
            }
            TaskManager.AddAsyncTask(delegate
            {
                if (Extras.CheckForUpdate())
                    UpdateForm.Open();
            });
            if (Properties.Settings.Default.FirstRun)
            {
                TaskManager.AddAsyncTask(delegate
                {
                    SplashScreen.ShowSplashScreen();
                });
                OnFirstRun();
            }
            ConnectionForm cf = new ConnectionForm();
            if (cf.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                Dispose();
                return;
            }
            TaskManager.AddAsyncTask(delegate
            {
                SplashScreen.ShowSplashScreen();
            });
            Debug.Assert("Launching Program.");
            Thread.Sleep(500);
            SplashScreen.UpdateStatusTextWithStatus("Preparing to connect to the update server...", TypeOfMessage.Warning);
            this.Opacity = .0;
            Thread.Sleep(3000);
            SplashScreen.UpdateStatusText("Connecting...");
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket = s;
            Debug.Assert("Creating Objects.");
            try
            {
                string srv = cf.ConnectTo.Address;
                int port =  cf.ConnectTo.Port;
                if (srv == LocalAddress.ToString()) srv = "127.0.0.1";
                ConnectionHandler.ConnectTo(s, srv, port);
            }
            catch (SocketException ex)
            {
                Debug.Assert(ex);
                MessageBox.Show("There was an error while connecting to the update server.  I will now self destruct.");
                Thread.Sleep(1000);
                SplashScreen.UpdateStatusTextWithStatus("Boom!!!", TypeOfMessage.Error);
                Thread.Sleep(5000);
                SplashScreen.UpdateStatusTextWithStatus("That was a joke, by the way.", TypeOfMessage.Warning);
                Thread.Sleep(1000);
                SplashScreen.CloseSplashScreen();
                Thread.Sleep(3000);
                Close();
                return;
            }
            catch(Exception ex)
            {
                ExceptionHandler.HandleException(ex);
            }
            modImages = new ImageList();
            modImages.ImageSize = new Size(230, 180);
            modImages.ColorDepth = ColorDepth.Depth32Bit;
            TaskManager.AddAsyncTask(delegate
            {
                while (s.Connected) ;
                if (!warnDisconnect) return;
                SplashScreen.UpdateStatusTextWithStatus("Lost connection to server.", TypeOfMessage.Error);
                if (Visible) MessageBox.Show("Lost connection to server.");
                else Thread.Sleep(5000);
                Invoke(new Void(delegate
                {
                    Close();
                }));
            });
            MinecraftModUpdater.Logger.Log(Logger.Level.Info, "Logging started.");
            ph = new PacketHandler(s);
            TaskManager.AddAsyncTask(delegate
            {
                ph.Start();
            });
            TaskManager.AddAsyncTask(delegate
            {
                ph.RegisterPacketHandler(PacketId.Metadata, ph_Metadata);
                ph.RegisterPacketHandler(PacketId.ModInfo, ph_ModInfo);
                ph.RegisterPacketHandler(PacketId.ModList, ph_ModList);
                ph.RegisterPacketHandler(PacketId.AllDone, ph_AllDone);
                ph.RegisterPacketHandler(PacketId.NextDownload, ph_NextDownload);
                ph.RegisterPacketHandler(PacketId.FilePart, ph_FilePart);
                ph.RegisterPacketHandler(PacketId.Image, ph_Image);
                Debug.Assert("Packet Handlers registered.");
            });
            if ((new LoginForm()).ShowDialog() != DialogResult.OK)
            {
                MinecraftModUpdater.Logger.Log(Logger.Level.Error, "Login failed");
                SplashScreen.UpdateStatusTextWithStatus("Your login failed.", TypeOfMessage.Error);
                Thread.Sleep(2000);
                SplashScreen.CloseSplashScreen();
                Thread.Sleep(400);
                Close();
                return;
            }
            Thread.Sleep(1000);
            SplashScreen.UpdateStatusText("Connected to server.  Retreving Mod List.");
            Packet.Send(new HandshakePacket { Username = Properties.Settings.Default.Username }, ph.Stream);
            Debug.Assert("Sent Handshake Packet.");
            Thread.Sleep(100);
        }

        private void OnFirstRun()
        {
            Properties.Settings.Default.MinecraftPath = Environment.CurrentDirectory + "/Minecraft";
            while (SplashScreen.GetScreen() == null) ;
            while (SplashScreen.GetScreen().Opacity != 1.0) ;
            SplashScreen.UpdateStatusText("Welcome to " + MinecraftModUpdater.ShortAppName + " Version " + MinecraftModUpdater.Version + ".");
            OptionsForm of = new OptionsForm();
            Thread.Sleep(2000);
            of.ShowDialog();
            SplashScreen.CloseSplashScreen();
        }

        void ph_Image(Packet pa)
        {
            ImagePacket p = pa as ImagePacket;
            Image i = Extras.ImageFromBytes(ph.Stream.DecryptBytes(p.Image));
            if (p.Type == ImagePacket.ImageType.Background)
            {
                SplashScreen.BackgroundImage = i;
                if (SplashScreen.GetScreen() != null)
                {
                    SplashScreen.GetScreen().Image.Image = i;
                }
            }
            else
            {
                modImages.Images.Add(p.ShowOn, i);
            }
        }
        void ph_FilePart(Packet pa)
        {
            FilePartPacket p = pa as FilePartPacket;
            
            if (ExceptionHandler.ProgramCrashed)
            {
                ph.Stop();
                return;
            }
            while (SplashScreen.GetScreen().Loading) ;
            int i = p.Index;
            byte[] d = ph.Stream.DecryptBytes(p.Part);
            foreach (byte b in d)
            {
                CurrentDownload.FileContents[i] = b;
                i++;
            }
            TaskManager.AddAsyncTask(delegate
            {
                SplashScreen.GetScreen().Invoke(new Void(delegate
                {
                    SplashScreen.GetScreen().progressBar1.PerformStep();
                }));
            });
        }

        void ph_NextDownload(Packet pa)
        {
            NextDownloadPacket p = pa as NextDownloadPacket;
            Thread.Sleep(100);
            SplashScreen.GetScreen().Invoke(new Void(delegate
            {
                SplashScreen.GetScreen().progressBar1.Value = 0;
                SplashScreen.GetScreen().progressBar1.Maximum = p.ChunkSize * 10;
                SplashScreen.GetScreen().progressBar1.Style = ProgressBarStyle.Blocks;
                SplashScreen.GetScreen().progressBar1.PerformStep();
            }));
            CurrentDownload = new ModFile(p.ModName, p.FileName, p.Length);
            if(!ServerShutdown)
                SplashScreen.UpdateStatusText("Downloading " + p.ModName);
            else
                SplashScreen.UpdateStatusTextWithStatus("Downloading " + p.ModName + "(Server Shutdown Mode)", TypeOfMessage.Warning);
            MinecraftModUpdater.Logger.Log(Logger.Level.Info, "Starting download of " + p.ModName);
            if(modImages.Images.ContainsKey(p.FileName))
                SplashScreen.GetScreen().setDownloadPicture(modImages.Images[p.FileName]);
            PostDownload = p.PostDownloadCLI;
            string path = Properties.Settings.Default.MinecraftPath + "\\" + p.FileName.Replace(p.FileName.Split('\\').Last(), "").TrimEnd('\\').Replace("clientmods", "mods");
            bool exists = Directory.Exists(path);
            if (!exists) Directory.CreateDirectory(path);
        }

        void ph_AllDone(Packet pa)
        {
            AllDonePacket p = pa as AllDonePacket;
            TaskManager.AddAsyncTask(delegate
            {
                string path = Properties.Settings.Default.MinecraftPath + "\\" + p.File.Replace(p.File.Split('\\').Last(), "").TrimEnd('\\');
                File.WriteAllBytes(path + "\\" + Path.GetFileName(p.File), CurrentDownload.FileContents);
                MinecraftModUpdater.Logger.Log(Logger.Level.Info, "Downloaded " + path + "\\" + Path.GetFileName(p.File));
            });
            foreach (string s in PostDownload)
            {
                try
                {
                    ProcessStartInfo pr = new ProcessStartInfo("cmd", "/c " + s);
                    pr.CreateNoWindow = true;
                    pr.UseShellExecute = false;
                    pr.RedirectStandardOutput = true;
                    Process proc = new Process();
                    proc.StartInfo = pr;
                    proc.Start();
                    MinecraftModUpdater.Logger.Log(Logger.Level.Info, "[Post Download] " + proc.StandardOutput.ReadToEnd());
                }
                catch (Exception e) { ExceptionHandler.HandleException(e); }
            }
            if (GetLastModToUpdate().File == p.File)
            {
                SplashScreen.UpdateStatusText("All files downloaded!");
                Thread.Sleep(1000);
                if (Properties.Settings.Default.LaunchAfterUpdate)
                {
                    Program.StartMinecraft();
                }
                else
                {
                    SplashScreen.CloseSplashScreen();
                }
                Packet.Send(new LogPacket { LogMessages = MinecraftModUpdater.Logger.GetMessages() }, ph.Stream);
                Packet.Send(new DisconnectPacket(), ph.Stream);
                ph.Stop();
                ph.RemovePacketHandler(PacketId.Metadata);
                ph.RemovePacketHandler(PacketId.ModInfo);
                ph.RemovePacketHandler(PacketId.ModList);
                ph.RemovePacketHandler(PacketId.NextDownload);
                ph.RemovePacketHandler(PacketId.FilePart);
                ph.RemovePacketHandler(PacketId.AllDone);
                if (socket.Connected) socket.Disconnect(false);
                Invoke(new Void(delegate
                {
                    Close();
                }));
            }
        }
        Mod GetLastModToUpdate()
        {
            Mod m = (Mod)lsModsToUpdate.Items[0];
            foreach(object o in lsModsToUpdate.Items)
            {
                m = (Mod)o;
            }
            return m;
        }
        string GetLastModFile()
        {
            string s = "";
            foreach (string str in ModFiles)
            {
                s = str;
            }
            return s;
        }
        void ph_ModList(Packet pa)
        {
            ModListPacket p = pa as ModListPacket;
            ModFiles.AddRange(p.Mods);
            foreach (string s in p.Mods)
            {
                Packet.Send(new RequestModPacket { FileName = s, Type = RequestModPacket.RequestType.Info }, ph.Stream);
            }
            bool exists = Directory.Exists(Properties.Settings.Default.MinecraftPath + @"\mods");
            if (!exists) Directory.CreateDirectory(Properties.Settings.Default.MinecraftPath + @"\mods");
            foreach (string s in Directory.GetFiles(Properties.Settings.Default.MinecraftPath + @"\mods"))
            {
                string str = @"mods\" + Path.GetFileName(s);
                bool file = ModFiles.Contains(str);
                if (!file)
                    Invoke(new Void(delegate
                    {
                        lsModsToDelete.Items.Add(Path.GetFileName(s));
                    }));
            }
            if (!Properties.Settings.Default.AutoUpdate)
                Invoke(new Void(delegate
                {
                    Opacity = 1;
                    SplashScreen.CloseSplashScreen();
                    Show();
                }));
        }

        void ph_ModInfo(Packet pa)
        {
            ModInfoPacket p = pa as ModInfoPacket;
            Mod m = new Mod { Author = p.Author, File = p.File, ModName = p.ModName, Hash = p.Hash };
            Mods.Add(m);
            string path = Properties.Settings.Default.MinecraftPath + "\\" + p.File.Replace(p.File.Split('\\').Last(), "").TrimEnd('\\').Replace("clientmods", "mods");
            string s = "";
            bool exists = File.Exists(path + "\\" + Path.GetFileName(m.File));
            if (exists)
            {
                try
                {
                    s = Extras.GenerateHash(path + "\\" + Path.GetFileName(m.File));
                }
                catch (Exception e) { MinecraftModUpdater.Logger.Log(e); }
            }
            if (!exists || s != m.Hash)
            {
                if (m.File.Contains("bin") && s == m.Hash)
                    Invoke(new Void(delegate
                    {
                        lsMods.Items.Add(m);
                    }));
                else
                    Invoke(new Void(delegate
                    {
                        lsModsToUpdate.Items.Add(m);
                    }));
            }
            else
            {
                Invoke(new Void(delegate
                {
                    lsMods.Items.Add(m);
                }));
            }
            MinecraftModUpdater.Logger.Log(Logger.Level.Info, "Info: " + m.ModName);
            string str = GetLastModFile();
            if (str == m.File && Properties.Settings.Default.AutoUpdate)
            {
                Invoke(new Void(delegate
                {
                    btnConfirm_Click(null, null);
                }));
            }
        }

        void ph_Metadata(Packet pa)
        {
            MetadataPacket p = pa as MetadataPacket;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < p.SData.Length; i++)
            {
                sb.AppendFormat("StringData {0}: {1}\r\n", i, p.SData[i]);
            }
            for (int i = 0; i < p.IData.Length; i++)
            {
                sb.AppendFormat("IntData {0}: {1}\r\n", i, p.IData[i]);
            }
            for (int i = 0; i < p.FData.Length; i++)
            {
                sb.AppendFormat("FloatData {0}: {1}\r\n", i, p.FData[i]);
            }
            Debug.Assert(sb.ToString());
            if (p.SData[0] == "shutdown")
            {
                ServerShutdown = true;
                if (SplashScreen.GetScreen() != null)
                    SplashScreen.UpdateStatusTextWithStatus(p.SData[1], TypeOfMessage.Error);
                else
                    MessageBox.Show(p.SData[1], "Server Shutdown");
                MinecraftModUpdater.Logger.Log(Logger.Level.Error, "Server Shutdown.  Reason: " + p.SData[1]);
            }
            else if (p.SData[0] == "server_name")
            {
                serverName = p.SData[1];
                serverFontSize = p.FData[0];
                Properties.Settings.Default.MinecraftPath = Environment.CurrentDirectory + "/Minecraft/" + ServerFolder;
                MinecraftModUpdater.Logger.Log(Logger.Level.Info, Properties.Settings.Default.MinecraftPath);
            }
            else if (p.SData[0] == "splash_display")
            {
                SplashScreen.UpdateStatusText(p.SData[1]);
            }
            else if (p.SData[0] == "require_version")
            {
                warnDisconnect = false;
                SplashScreen.UpdateStatusTextWithStatus("This server requires " + p.SData[1] + " for you to connect.", TypeOfMessage.Error);
                Thread.Sleep(3000);
                SplashScreen.CloseSplashScreen();
                Thread.Sleep(1000);
                Invoke(new Void(delegate
                {
                    Close();
                }));
            }
        }
        struct Mod
        {
            public string ModName;
            public string Author;
            public string File;
            public string Hash;
            public override string ToString()
            {
                return ModName;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            warnDisconnect = false;
            MinecraftModUpdater.Logger.Log(Logger.Level.Info, "Stopping logging.");
            try
            {
                if (socket.Connected)
                {
                    Packet.Send(new LogPacket { LogMessages = MinecraftModUpdater.Logger.GetMessages() }, ph.Stream);
                    ph.Stop();
                }
                else
                {
                    string[] file = MinecraftModUpdater.Logger.GetMessages();

                    File.WriteAllLines("ModUpdater.log", file);
                }
            }
            catch { }
        }
        
    }
}
