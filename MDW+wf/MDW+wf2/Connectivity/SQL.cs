using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using MDW_wf.Model;
using System.Data;
using MDW_wf.Controller;

namespace MDW_wf.Connectivity
{
    public static class SQL
    {
        public static string Server = "";
        public static string DataBase = "";
        public static string User = "";
        public static string Password = "";
        public static void Write(HTKLibrary.Classes.MDW.Tag tag)
        {
            Server = Program.configManager.SQLServer;
            DataBase = Program.configManager.SQLDatabase;
            User = Program.configManager.SQLUser;
            Password = Program.configManager.SQLPassword;
            if (Server == "" || DataBase == "" || User == "" || Password == "") return;
            try
            {
                using (SqlConnection openCon = new SqlConnection("Data Source=" + Server + ";Initial Catalog=" + DataBase + ";User ID=" + User + ";Password=" + Password))
                {
                    string saveStaff = "INSERT into TagList (TIMESTAMP,IP,EPC,RSSI,DIRECCION) VALUES (@timestamp,@ip,@epc,@rssi,@direccion)";

                    using (SqlCommand querySaveStaff = new SqlCommand(saveStaff))
                    {
                        querySaveStaff.Connection = openCon;
                        querySaveStaff.Parameters.Add("@timestamp", SqlDbType.VarChar, 50).Value = tag.timestamp;
                        querySaveStaff.Parameters.Add("@ip", SqlDbType.VarChar, 15).Value = tag.ip;
                        querySaveStaff.Parameters.Add("@epc", SqlDbType.VarChar, 24).Value = tag.epc;
                        querySaveStaff.Parameters.Add("@rssi", SqlDbType.VarChar, 3).Value = tag.rssi;
                        querySaveStaff.Parameters.Add("@direccion", SqlDbType.VarChar, 1).Value = tag.direction;
                        openCon.Open();
                        querySaveStaff.ExecuteNonQuery();
                        openCon.Close();
                    }
                }
            }
            catch { }
          
        }

    }
}
