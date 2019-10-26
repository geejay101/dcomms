﻿using Dcomms.Sandbox;
using Dcomms.Vision;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Dcomms.SandboxTester
{
    public partial class SandboxTesterMainWindow : Window
    {
        VisionChannel1 VisionChannel { get; set; } = new VisionChannel1();

        readonly SandboxTester1 _tester;

        Timer _timer;
        public SandboxTesterMainWindow()
        {
            _tester = new SandboxTester1(VisionChannel);
            InitializeComponent();
            this.DataContext = _tester;
            visionGui.DataContext = VisionChannel;

            this.Closed += CryptographyTesterMainWindow_Closed;


            _timer = new Timer((o) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    if (increaseNumberOfEnginesOnTimer.IsChecked == true)
                    {
                        _tester.DrpTester3.IncreaseNumberOfEngines.Execute(null);
                    }
                });
            }, null, 0, 3000);
            this.Closed += CryptographyTesterMainWindow_Closed1;

            VisionChannel.DisplayPeersDelegate = (text, peersList, mode) =>
            {
                var wnd = new PeersDisplayWindow(text, peersList, mode);
                wnd.Show();
            };
            VisionChannel.DisplayRoutingPathDelegate = (req) =>
            {
                var logMessages = VisionChannel.GetLogMessages(req);
                var peers = logMessages.Select(x => x.RoutedPathPeer).Distinct().ToList();

                var peersWnd = new PeersDisplayWindow($"routing for {req}", peers, VisiblePeersDisplayMode.routingPath);
                peersWnd.Show();


                var logWnd = new FilteredLogMessagesWindow(logMessages) { Title = $"routing for {req}" };
                logWnd.Show();

            };
        }

        private void CryptographyTesterMainWindow_Closed1(object sender, EventArgs e)
        {
            _timer.Dispose();
        }

        private void CryptographyTesterMainWindow_Closed(object sender, EventArgs e)
        {
            _tester.Dispose();
        }
    }
}
