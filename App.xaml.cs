﻿using System.Windows;

namespace Kalendarz1
{
    public partial class App : Application
    {
        public static string UserID { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Menu1 loginWindow = new Menu1();
            loginWindow.Show();
        }
    }
}