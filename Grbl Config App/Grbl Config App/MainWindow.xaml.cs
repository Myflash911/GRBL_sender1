﻿
/*
 * MainWindow.xaml.cs - part of Grbl Code Sender
 *
 * v0.34 / 2021-08-20 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2019-2020, Io Engineering (Terje Io)
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

· Redistributions of source code must retain the above copyright notice, this
list of conditions and the following disclaimer.

· Redistributions in binary form must reproduce the above copyright notice, this
list of conditions and the following disclaimer in the documentation and/or
other materials provided with the distribution.

· Neither the name of the copyright holder nor the names of its contributors may
be used to endorse or promote products derived from this software without
specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

*/

using System;
using System.Windows;
using CNC.Core;
using CNC.Controls;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using Microsoft.Win32;

namespace Grbl_Config_App
{

    public partial class MainWindow : Window
    {
        private const string version = "2.0.34";
        public static UIViewModel UIViewModel { get; } = new UIViewModel();

        public MainWindow()
        {
            CNC.Core.Resources.Path = AppDomain.CurrentDomain.BaseDirectory;

            InitializeComponent();
            Title = string.Format(Title, version);

            int res;
            if ((res = AppConfig.Settings.SetupAndOpen(Title, (GrblViewModel)DataContext, App.Current.Dispatcher)) != 0)
                Environment.Exit(res);

            CNC.Core.Grbl.GrblViewModel = (GrblViewModel)DataContext;
        }

        #region UIEvents

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            System.Threading.Thread.Sleep(50);
            Comms.com.PurgeQueue();

            using (new UIUtils.WaitCursor())
            {
                GrblInfo.Get();
                GrblSettings.Load();
            }

            configView.Activate(true, ViewType.Startup);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            configView.Activate(false, ViewType.Shutdown);

            using (new UIUtils.WaitCursor()) // disconnecting from websocket may take some time...
            {
               Comms.com.Close();
            }
        }

        private void exitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        void aboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            About about = new About(Title) { Owner = Application.Current.MainWindow };
            about.DataContext = DataContext;
            about.ShowDialog();
        }

        #endregion

        private void getHalSettingsItem_Click(object sender, RoutedEventArgs e)
        {
            bool getExtended = GrblInfo.ExtendedProtocol && GrblInfo.Build >= 20200716;

            var g = new Settings();

            if(g.Load(DataContext as GrblViewModel, "$ESH"))
            {
                SaveFileDialog saveDialog = new SaveFileDialog()
                {
                    Filter = "Tab separated file (*.txt)|*.txt",
                    AddExtension = true,
                    DefaultExt = ".txt",
                    ValidateNames = true
                };

                if (saveDialog.ShowDialog() == true)
                    g.Backup(saveDialog.FileName);
            }
        }

        private void getGrblSettingsItem_Click(object sender, RoutedEventArgs e)
        {
            bool getExtended = GrblInfo.ExtendedProtocol && GrblInfo.Build >= 20200716;

            var g = new Settings();

            if (g.Load(DataContext as GrblViewModel, "$ESG"))
            {
                SaveFileDialog saveDialog = new SaveFileDialog()
                {
                    Filter = "Comma separated file (*.csv)|*.csv",
                    AddExtension = true,
                    DefaultExt = ".csv",
                    ValidateNames = true
                };

                if (saveDialog.ShowDialog() == true)
                    g.Backup(saveDialog.FileName);
            }
        }
    }

    public class Settings
    {
        List<string> settings = new List<string>();

        public bool Load(GrblViewModel model, string cmd)
        {
            bool? res = null;
            CancellationToken cancellationToken = new CancellationToken();

            Comms.com.PurgeQueue();

            model.Silent = true;

                new Thread(() =>
                {
                    res = WaitFor.AckResponse<string>(
                        cancellationToken,
                        response => ProcessDetail(response),
                        a => model.OnResponseReceived += a,
                        a => model.OnResponseReceived -= a,
                        400, () => Comms.com.WriteCommand(cmd));
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();

            return settings.Count > 0;
        }

        public void Backup(string filename)
        {
            if (settings.Count > 0) try
            {
                StreamWriter file = new StreamWriter(filename);
                if (file != null)
                {
                    foreach (string s in settings)
                        file.WriteLine(s);

                    file.Close();
                }
            }
            catch
            {
            }
        }

        private void ProcessDetail(string data)
        {
            if (data != "ok")
            {
                settings.Add(data);
            }
        }
    }
}
