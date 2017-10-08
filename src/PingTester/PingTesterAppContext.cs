using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows.Forms;
using Dota2Ping.Properties;
using IniParser;
using IniParser.Model;

namespace Dota2Ping
{
    public class PingTesterAppContext : ApplicationContext
    {
        private NotifyIcon _trayIcon;
        private static readonly object IconLock = new object();

        private static readonly ManualResetEvent StopSignal = new ManualResetEvent(false);

        private const string ConfigFile = "config.ini";
        private readonly FileIniDataParser _confParser;
        private readonly IniData _confData;

        private MenuItem _selectedServerItem;
        private MenuItem _selectedIntervalItem;

        public PingTesterAppContext()
        {
            // Read config file
            _confParser = new FileIniDataParser();
            _confData = _confParser.ReadFile(ConfigFile);

            // Init the tray icon
            InitTryIcon();

            // Start pinging
            ThreadPool.QueueUserWorkItem(state => TestPing());
        }

        private void InitTryIcon()
        {
            if (_trayIcon != null)
            {
                return;
            }

            var serverMenuItems = ConstructMenuItemsFromConf("servers", "server", ref _selectedServerItem, OnServerSet);
            var intervalMenuItems = ConstructMenuItemsFromConf("intervals", "interval", ref _selectedIntervalItem,
                OnIntervalSet);

            _trayIcon = new NotifyIcon
            {
                Icon = Resources.Dota2,
                ContextMenu = new ContextMenu(new[]
                {
                    new MenuItem("Server", serverMenuItems),
                    new MenuItem("Interval", intervalMenuItems),
                    new MenuItem("Exit", Exit)
                }),
                Visible = true
            };
        }

        private MenuItem[] ConstructMenuItemsFromConf(string listKey, string defaultItemKey,
            ref MenuItem defaultMenuItem, EventHandler clickHandler)
        {
            var list = _confData[listKey];
            var menuItems = new List<MenuItem>();
            var defaultItemName = _confData["settings"][defaultItemKey];

            foreach (var keyData in list)
            {
                var menuItem = new MenuItem(keyData.KeyName, clickHandler);
                menuItems.Add(menuItem);
                if (keyData.KeyName == defaultItemName)
                {
                    defaultMenuItem = menuItem;
                    defaultMenuItem.Checked = true;
                }
            }
            return menuItems.ToArray();
        }

        private void OnServerSet(object sender, EventArgs eventArgs)
        {
            CheckMenuItem(ref _selectedServerItem, sender as MenuItem);
            _confData["settings"]["server"] = _selectedServerItem.Text;
            _confParser.WriteFile(ConfigFile, _confData);

        }

        private void OnIntervalSet(object sender, EventArgs eventArgs)
        {
            CheckMenuItem(ref _selectedIntervalItem, sender as MenuItem);
            _confData["settings"]["interval"] = _selectedIntervalItem.Text;
            _confParser.WriteFile(ConfigFile, _confData);
        }

        private void CheckMenuItem(ref MenuItem previousItem, MenuItem newItem)
        {
            if (previousItem != null)
            {
                previousItem.Checked = false;
            }

            if (newItem != null)
            {
                previousItem = newItem;
                previousItem.Checked = true;
            }
        }

        private void TestPing()
        {
            while (!StopSignal.WaitOne(0))
            {
                try
                {

                    if (_selectedServerItem == null || _selectedIntervalItem == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    var host = _confData["servers"][_selectedServerItem.Text];
                    var result = PingHost(host);

                    if (result != null && result.Status == IPStatus.Success)
                    {
                        if (result.RoundtripTime < int.Parse(_confData["settings"]["moderatePing"]))
                        {
                            SetTryIcon(Resources.Dota2Green, result.RoundtripTime);
                        }
                        else if (result.RoundtripTime < int.Parse(_confData["settings"]["highPing"]))
                        {
                            SetTryIcon(Resources.Dota2Yellow, result.RoundtripTime);
                        }
                        else
                        {
                            SetTryIcon(Resources.Dota2Red, result.RoundtripTime);
                        }
                    }
                    else
                    {
                        SetTryIcon(Resources.Dota2, 0);
                    }

                    var intervalStr = _confData["intervals"][_selectedIntervalItem.Text];
                    Thread.Sleep(int.Parse(intervalStr) * 1000);
                }
                catch (Exception)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        private void SetTryIcon(System.Drawing.Icon icon, long time)
        {
            lock (IconLock)
            {
                _trayIcon.Icon = icon;
                _trayIcon.Text = string.Format("{0}ms - {1}", time, _selectedServerItem.Text);
            }
        }

        private static PingReply PingHost(string nameOrAddress)
        {
            var pinger = new Ping();
            try
            {
                return pinger.Send(nameOrAddress);
            }
            catch (PingException)
            {
                return null;
            }
        }

        void Exit(object sender, EventArgs e)
        {
            // Hide tray icon, otherwise it will remain shown until user mouses over it
            _trayIcon.Visible = false;
            StopSignal.Set();

            Application.Exit();
        }
    }
}