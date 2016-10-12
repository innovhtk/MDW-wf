using HTKLibrary.Classes.MDW;
using HTKLibrary.Comunications;
using MDW_wf.Controller;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CSLibrary;
using CSLibrary.Constants;
using CSLibrary.Structures;
using CSLibrary.Diagnostics;
using HTKLibrary.Readers;
using System.Collections;
using MDW_wf.Model;

namespace MDW_wf
{
    static class Program
    {
        public static ConfigManager configManager;
        public static List<HighLevelInterface> CS203List = new List<HighLevelInterface>();
        public static List<HTKLibrary.Readers.CS101> CS101List = new List<CS101>();
        public static List<VirtualReader> VRList = new List<VirtualReader>();
        public static List<SlaveReader> SlaveList = new List<SlaveReader>();
        public static List<ReaderModel> Readers = new List<ReaderModel>();
        public static string applicationSettings = "application.config";
        public static appSettings appSetting = new appSettings();
        public static string mdwEmail = "middleware@htk-id.com";
        public static string mdwPwd = "Middleware2016!";
        public static string mdwURL = "http://webservice.assetsapp.com/mdw";
        public static MDWClient mdw;
        public static MDWRestClient restClient;
        /// <summary>
        /// Punto de entrada principal para la aplicación.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            configManager = ConfigManager.ReadConfig();
            Readers = ConfigManager.LoadReaders();
            mdw = new MDWClient(configManager.User, configManager.Password, configManager.MacAddress);
            restClient = new MDWRestClient(mdwEmail, mdwPwd, mdwURL, mdw);
            //Application.Run(new Start());
            int count = 0;
            try
            {
                using (StreamReader file = new StreamReader(configManager.AppPath + "\\tnuoc.c"))
                {
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        count = Convert.ToInt32(line);
                        break;
                    }
                }
            }
            catch { }
            if (count < 30)
            {
                try
                {
                    using (Start start = new Start())
                    {
                        try
                        {
                            start.ShowDialog();
                        }
                        catch(Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                            start.Close();
                            start.Dispose();
                        }
                    }
                }
                catch(Exception ex) { MessageBox.Show(ex.Message);  }
            }
            else
            {
                Activate();
                while(!configManager.Activated)
                {
                    MessageBox.Show("El periodo de prueba ha caducado");
                    using(Login login = new Login())
                    {
                        login.ShowDialog();
                        Activate();
                    }
                }

                  using (Start start = new Start())
                    {
                        try
                        {
                            start.ShowDialog();
                        }
                        catch(Exception)
                        {
                            start.Close();
                            start.Dispose();
                        }
                    }
            }
            try
            {

                using (StreamWriter writer = new StreamWriter(configManager.AppPath + "\\tnuoc.c"))
                {
                    writer.WriteLine((count + 1).ToString());
                }
                ConfigManager.Save(configManager);
                ConfigManager.SaveReaders(Readers);
            }
            catch { }

        }
        static void Activate()
        {
            mdw = new MDWClient(configManager.User, configManager.Password, configManager.MacAddress);
            restClient = new MDWRestClient(mdwEmail, mdwPwd, mdwURL, mdw);
            //MDWRestClient client = new MDWRestClient("middleware@htk-id.com", "Middleware2016!", "http://localhost:81", mdw);
            string token = restClient.LoginRestClient();
            if (token != "")
            {
                configManager.Token = token;
                var id = restClient.LoginMDWClient();
                if (id != "")
                {
                    restClient.mdw_client_id = id;
                    configManager.Activated = true;
                    configManager.UserId = id;
                }
                try
                {

                    using (StreamWriter writer = new StreamWriter(configManager.AppPath + "\\tnuoc.c"))
                    {
                        writer.WriteLine("0");
                    }
                    ConfigManager.Save(configManager);
                }
                catch { }
            }
            else
                configManager.Activated = false;
        }
       
    }
}
