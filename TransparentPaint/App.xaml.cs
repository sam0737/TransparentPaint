using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Hellosam.Net.TransparentPrint
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(App));

        protected override void OnStartup(StartupEventArgs e)
        {
            Logger.Info("--- Starting ---");
            Logger.InfoFormat("Version:{0}", System.Windows.Forms.Application.ProductVersion);

            AppDomain.CurrentDomain.UnhandledException += new
                UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            base.OnStartup(e);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Fatal("Unhandled Exception", e.ExceptionObject as Exception);
        }
    }
}
