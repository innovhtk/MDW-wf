﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MDW_Print
{
    static class Program
    {
        public static ConfigManager configManager;
        public static string mdwEmail = "middleware@htk-id.com";
        public static string mdwPwd = "Middleware2016!";
        public static string mdwURL = "http://webservice.assetsapp.com/mdw";
        /// <summary>
        /// Punto de entrada principal para la aplicación.
        /// </summary>
        [STAThread]
        static void Main()
        {
            configManager = ConfigManager.ReadConfig();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Print());
        }
    }
}
