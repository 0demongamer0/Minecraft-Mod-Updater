﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

namespace ModUpdater.Client
{
    public partial class ExceptionHandler : Form
    {
        public Exception Exception;
        private bool Locked = false;
        private string Report;
        public ExceptionHandler(Exception e)
        {
            Exception = e;
            InitializeComponent();
        }

        private void ExceptionHandler_Load(object sender, EventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Minecraft Mod Updater has crashed.");
            sb.AppendLine("Please make an error report about this including everything below the line.");
            sb.AppendLine("If you make an error report about this, I will make sure it gets fixed.");
            sb.AppendLine();
            sb.AppendLine("----------------------------------------------------------------");
            sb.AppendLine();
            sb.AppendLine(Exception.ToString());
            txtError.Text = sb.ToString();
            Locked = true;
            Report = txtError.Text;
        }
        public static void HandleException(Exception e)
        {
            new ExceptionHandler(e).ShowDialog();
        }
        public static void Init()
        {
            TaskManager.ExceptionRaised += new TaskManager.Error(HandleException);
        }

        private void btnReport_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/seaboy1234/Minecraft-Mod-Updater/issues/new");
            using (StreamWriter sw = File.CreateText("ErrorReport.log"))
            {
                sw.WriteLine(txtError.Text);
                sw.Close();
            }
            Application.Exit();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            using (StreamWriter sw = File.CreateText("ErrorReport.log"))
            {
                sw.WriteLine(txtError.Text);
                sw.Close();
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void txtError_TextChanged(object sender, EventArgs e)
        {
            if(Locked)
                txtError.Text = Report;
        }
    }
}
