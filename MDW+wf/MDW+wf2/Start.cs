using System;
using System.ComponentModel;
using System.Data;
using MDW_wf.Controller;
using HTKLibrary.Readers;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading;
using System.Drawing;
using MDW_wf.Model;
using System.Linq;
using System.Timers;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using CSLibrary.Constants;
using CSLibrary.Structures;
using CSLibrary;
using MDW_wf.Connectivity;
using System.Diagnostics;
//using NiceLabel5WR;
using System.IO;
using HTKLibrary.Comunications;
using Newtonsoft.Json;

namespace MDW_wf
{
    public partial class Start : Form, IDisposable
    {
        #region Global Constants
        public const int Tab_Dashboard = 0;
        public const int Tab_Readers = 1;
        public const int Tab_Print = 2;
        public const int Tab_Config = 3;
        public const int Tab_AddHardware = 4;
        #endregion

        #region Global Variables
        public ServerGPIO serverGpio = new ServerGPIO(500);
        public ServerTags serverTags = new ServerTags(501);
        public Color ThemeColor = Color.FromArgb(32, 50, 72);
        private string filter = "";
        public string Filter { get { return filter.ToLower(); } set { filter = value; } }
        #region Notify
        protected void NotifyPropertyChanged(String propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public Publish publish;
        private static HTKLibrary.Classes.MDW.MDWClient mdw;
        private static MDWRestClient restClient;
        private static RestClient legacyRestClient;
        private static SocketClient socketClient;
        #endregion
        #endregion

        #region Form
        public Start()
        {
            InitializeComponent();
            InitListView();

            lvReaders.LargeImageList = connectionImages;
            lvReaders.SmallImageList = connectionImages;
            lvReaders.StateImageList = connectionImages;
            lvReaders.View = View.Tile;

            publish = new Publish();

            serverGpio.Start();
            serverGpio.GPIOSignal += ServerGpio_GPIOSignal;
            serverTags.Start();
        }

        private void ServerGpio_GPIOSignal(GPIO gpio)
        {
            cbGPO0.Invoke((MethodInvoker)(() => cbGPO0.Checked = gpio.gpio0));
            cbGPO1.Invoke((MethodInvoker)(() => cbGPO1.Checked = gpio.gpio1));
        }

        private void Start_Load(object sender, EventArgs e)
        {
            FillConfiguration();
            HideTabHeaders();
            LoadAntennas();
            btDashboardTab_Click(null, null);
        }

        private void LoadAntennas()
        {
            int totalreaders = Program.Readers.Count();
            if (totalreaders < 1) return;
            for(int i = 0; i<totalreaders; i++)
            {
                Program.Readers[i].connected = false;
                Program.Readers[i].started = false;
                if (Program.Readers[i].model.ToLower() == "cs203")
                {
                    HighLevelInterface CS203 = new HighLevelInterface();
                    CS203.Name = Program.Readers[i].ip;
                    Program.CS203List.Add(CS203);
                }
                if (Program.Readers[i].model.ToLower() == "cs469")
                {
                    HighLevelInterface CS203 = new HighLevelInterface();
                    CS203.Name = Program.Readers[i].ip;
                    Program.CS203List.Add(CS203);
                }
                if (Program.Readers[i].model.ToLower() == "cs101")
                {
                    CS101 cs101 = new CS101(Program.Readers[i].ip);
                    cs101.Alias = Program.Readers[i].alias;
                    Program.CS101List.Add(cs101);
                }
                if(Program.Readers[i].model.ToLower() == "virtual")
                {
                    VirtualReader vr = new VirtualReader(Program.Readers[i].ip);
                    vr.alias = Program.Readers[i].alias;
                    Program.VRList.Add(vr);
                }
                if (Program.Readers[i].model.ToLower() == "remoto")
                {
                    SlaveReader vr = new SlaveReader(Program.Readers[i].ip, serverTags);
                    vr.alias = Program.Readers[i].alias;
                    Program.SlaveList.Add(vr);
                }
                //---aumetar cuenta de hardware
                ListViewItem item = new ListViewItem(Program.Readers[i].ip);
                item.SubItems.Add(Program.Readers[i].model + ", " + Program.Readers[i].alias);
                item.ImageIndex = 0;
                lvReaders.Items.Add(item);
            }
        }

        private void Start_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Fourth Step (Dettach from Form and Stop)
            foreach (HighLevelInterface ReaderXP in Program.CS203List)
            {
                if (ReaderXP.State != RFState.IDLE)
                {
                    mStop = e.Cancel = true;
                    ReaderXP.StopOperation(true);
                }
                else
                {
                    AttachCallback(false, ReaderXP.IPAddress);
                }
            }
            Environment.Exit(0);
        }
        private void HideTabHeaders()
        {
            Tab.Appearance = TabAppearance.FlatButtons;
            Tab.ItemSize = new Size(0, 1);
            Tab.SizeMode = TabSizeMode.Fixed;
        }
        public void FillConfiguration()
        {
            var address = NetworkInterface
               .GetAllNetworkInterfaces()
               .Where(i => i.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
               .SelectMany(i => i.GetIPProperties().UnicastAddresses)
               .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
               .Select(a => a.Address.ToString())
               .ToList();
            if (address.Count > 0)
                lbToolbarIP.Invoke(new MethodInvoker(delegate { lbToolbarIP.Text = address[0]; }));

            var addresswifi = NetworkInterface
               .GetAllNetworkInterfaces()
               .Where(i => i.Name == "Wi-Fi")
               .SelectMany(i => i.GetIPProperties().UnicastAddresses)
               .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
               .Select(a => a.Address.ToString())
               .ToList();
            if (address.Count > 0)
                lbToolbarIP.Invoke(new MethodInvoker(delegate { lbToolbarIPWifi.Text = addresswifi[0]; }));

            lbToolbarReaders.Invoke(new MethodInvoker(delegate { lbToolbarReaders.Text = (Program.CS203List.Count + Program.CS101List.Count).ToString(); }));

            tbConfCSVDir.Invoke(new MethodInvoker(delegate { tbConfCSVDir.Text = Program.configManager.CSVPath; }));
            tbConfSQLDB.Invoke(new MethodInvoker(delegate { tbConfSQLDB.Text = Program.configManager.SQLDatabase; }));
            tbConfSQLPass.Invoke(new MethodInvoker(delegate { tbConfSQLPass.Text = Program.configManager.SQLPassword; }));
            tbConfSQLServer.Invoke(new MethodInvoker(delegate { tbConfSQLServer.Text = Program.configManager.SQLServer; }));
            tbConfSQLUser.Invoke(new MethodInvoker(delegate { tbConfSQLUser.Text = Program.configManager.SQLUser; }));
            tbConfXMLDir.Invoke(new MethodInvoker(delegate { tbConfXMLDir.Text = Program.configManager.XMLPath; }));
            tbConfWServiceURL.Invoke(new MethodInvoker(delegate { tbConfWServiceURL.Text = Program.configManager.WebServiceURL; }));
            tbConfWsocketIP.Invoke(new MethodInvoker(delegate { tbConfWsocketIP.Text = Program.configManager.SocketIP; }));
            tbConfWSocketPort.Invoke(new MethodInvoker(delegate { tbConfWSocketPort.Text = Program.configManager.SocketPort.ToString(); }));
        }
        public void Message(string v1, bool v2)
        {
            MessageBox.Show(v1);
        }
        #endregion

        #region Panel
        private void btDashboardTab_Click(object sender, EventArgs e)
        {
            /// cambiar por valores reales
            int hh = Program.CS101List.Count;
            int readers = Program.CS203List.Count;
            int printers = 0;

            lbNumberHandhleds.Text = hh.ToString();
            lbNumberPrinters.Text = printers.ToString();
            lbNumerAntennas.Text = readers.ToString();
            lbNumerHardware.Text = (readers + hh).ToString();
            lbToolbalPrinters.Text = printers.ToString();
            lbToolbarReaders.Text = (readers + hh).ToString();

            Tab.Invoke(new MethodInvoker(delegate { Tab.SelectedIndex = Tab_Dashboard; }));
            lbStatus.Text = "Dashboard";
        }
        public void btPrintTab_Click(object sender, EventArgs e)
        {
            //Tab.Invoke(new MethodInvoker(delegate { Tab.SelectedIndex = Tab_Print; }));
            lbStatus.Text = "Módulo de impresión";
            string path = Program.configManager.AppPath + "\\MDW+Print.exe";
            Process.Start(path);
        }
        private void btConfigurationTab_Click(object sender, EventArgs e)
        {
            FillConfiguration();
            Tab.Invoke(new MethodInvoker(delegate { Tab.SelectedIndex = Tab_Config; }));
            lbStatus.Text = "Módulo de configuración";
        }
        private void btReaderTab_Click(object sender, EventArgs e)
        {
            Tab.Invoke(new MethodInvoker(delegate { Tab.SelectedIndex = Tab_Readers; }));
            lbStatus.Text = "Módulo de lectura de antenas";
            Reader.StartReadersSearch();
        }

        private void Reader_ReaderFound(Reader.ReaderNetInfo info)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke((System.Threading.ThreadStart)delegate ()
                {
                    foreach (ListViewItem item in lvReaders.Items)
                    {
                        if (item.Text == info.IP)
                            item.BackColor = Color.Green;
                    }
                });
            }
        }

        private void btToolStripAddReader_Click(object sender, EventArgs e)
        {
            Tab.Invoke(new MethodInvoker(delegate { Tab.SelectedIndex = Tab_AddHardware; }));
            lbStatus.Text = "Agregar nuevo hardware";
        }
        #endregion

        #region Readers
        private object MyLock = new object();
        private bool mStop = false;
        private long totaltags = 0;

        private uint AntCycleCount = 0;
        private int[] AntCycleTime = new int[5];
        private int AntCycleTimeCount = 0;

        private Custom.Control.SortColumnHeader m_colhdrIndex;
        private Custom.Control.SortColumnHeader m_colhdrTimestamp;
        private Custom.Control.SortColumnHeader m_colhdrIp;
        private Custom.Control.SortColumnHeader m_colhdrEpc;
        private Custom.Control.SortColumnHeader m_colhdrRssi;
        private Custom.Control.SortColumnHeader m_colhdrDirection;
        private Custom.Control.SortListView m_sortListView;

        private int HoldTime = 0;
        class HoldTag
        {
            public int time { get; set; } = 0;
            public string epc { get; set; }
            public HoldTag(string _epc, int _time)
            {
                time = _time;
                epc = _epc;
            }
        }
        private List<HoldTag> HoldListItems = new List<HoldTag>();
        private List<HoldTag> lock_HoldItems
        {
            get { lock (MyLock) { return HoldListItems; } }
            set { lock (MyLock) { HoldListItems = value; } }
        }

        private List<CSLibrary.Structures.TagCallbackInfo> InventoryListItems = new List<CSLibrary.Structures.TagCallbackInfo>();
        private List<CSLibrary.Structures.TagCallbackInfo> lock_InvItems
        {
            get { lock (MyLock) { return InventoryListItems; } }
            set { lock (MyLock) { InventoryListItems = value; } }
        }

        private void InitListView()
        {
            this.m_sortListView = new Custom.Control.SortListView();
            // 
            // m_sortListView
            // 
            this.m_sortListView.FullRowSelect = true;
            this.m_sortListView.GridLines = true;
            this.m_sortListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            this.m_sortListView.Location = new System.Drawing.Point(3, 3);
            this.m_sortListView.Name = "m_sortListView";
            this.m_sortListView.Size = new System.Drawing.Size(380, 400);
            this.m_sortListView.TabIndex = 0;
            this.m_sortListView.UseCompatibleStateImageBehavior = false;
            this.m_sortListView.View = System.Windows.Forms.View.Details;
            this.m_sortListView.Font = new Font(FontFamily.GenericSerif, 6);
            this.m_sortListView.SelectedIndexChanged += new System.EventHandler(this.m_sortListView_SelectedIndexChanged);
            m_colhdrIndex = new Custom.Control.SortColumnHeader();
            m_colhdrTimestamp = new Custom.Control.SortColumnHeader();
            m_colhdrIp = new Custom.Control.SortColumnHeader();
            m_colhdrEpc = new Custom.Control.SortColumnHeader();
            m_colhdrRssi = new Custom.Control.SortColumnHeader();
            m_colhdrDirection = new Custom.Control.SortColumnHeader();
            m_sortListView.Columns.AddRange(new ColumnHeader[] {
                m_colhdrIndex,
                m_colhdrTimestamp,
                m_colhdrIp,
                m_colhdrEpc,
                m_colhdrRssi,
                m_colhdrDirection,
            });
            m_colhdrIndex.Text = "";
            m_colhdrIndex.Width = 30;
            m_colhdrTimestamp.Text = "Timestamp";
            m_colhdrTimestamp.Width = 170;
            m_colhdrIp.Text = "IP";
            m_colhdrIp.Width = 110;
            m_colhdrEpc.Text = "EPC";
            m_colhdrEpc.Width = 210;
            m_colhdrRssi.Text = "RSSI";
            m_colhdrRssi.Width = 80;
            m_colhdrDirection.Text = "Direction";
            m_colhdrDirection.Width = 100;
            m_sortListView.SortColumn = 0;
            m_sortListView.Dock = DockStyle.Fill;
            panelReadings.Controls.Add(m_sortListView);
            // Assign specific comparers to each column header.
            m_colhdrIndex.ColumnHeaderSorter = new Custom.Control.ComparerStringAsInt();
            m_colhdrTimestamp.ColumnHeaderSorter = new Custom.Control.ComparerString();
            m_colhdrIp.ColumnHeaderSorter = new Custom.Control.ComparerString();
            m_colhdrEpc.ColumnHeaderSorter = new Custom.Control.ComparerString();
            m_colhdrRssi.ColumnHeaderSorter = new Custom.Control.ComparerStringAsDouble();
            m_colhdrDirection.ColumnHeaderSorter = new Custom.Control.ComparerStringAsInt();
            this.m_sortListView.Sorting = Custom.Control.SortOrder.Ascending;
        }
        private void m_sortListView_SelectedIndexChanged(object sender, EventArgs e)
        {
        }
        private void udEraseTime_SelectedItemChanged(object sender, EventArgs e)
        {
            if (udEraseTime.Text == "") return;
            int interval = tmrRowColor.Interval;
            try
            {
                interval = (int)(Convert.ToDouble(udEraseTime.Text) * 1000);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            if (interval < 100) return;
            tmrRowColor.Stop();
            tmrRowColor.Interval = interval;
            tmrRowColor.Start();
        }
        private void udWaitTime_SelectedItemChanged(object sender, EventArgs e)
        {
            int time = 0;
            int.TryParse(udWaitTime.Text, out time);
            HoldTime = time;
            //var vr = Program.VRList.FindAll(delegate (VirtualReader vv) { return vv.connected = false ; });
            if (HoldTime == 0) return;
            foreach(var v in Program.VRList)
            {
                v.readTime = time;
            }
        }

        #region Reader Control
        public bool prev = false;
        public string previp = "";
        public void Play(string ip)
        {
            var index= Program.Readers.FindIndex(r => r.ip == ip);
            var reader = Program.CS203List.Find(delegate (HighLevelInterface h) { return h.Name == ip || h.IPAddress == ip; });
            var handheld = Program.CS101List.Find(delegate (CS101 hh) { return hh.IP == ip; });
            var vr = Program.VRList.Find(delegate (VirtualReader vv) { return vv.id == ip; });
            var slave = Program.SlaveList.Find(delegate (SlaveReader ss) { return ss.id == ip; });
            if (index > -1)
            {
                if (!Program.Readers[index].connected)
                {
                    Program.Readers[index].started = false;
                    if(Program.Readers[index].model.ToLower() == "cs203" && reader != null)
                    {
                        CSLibrary.Constants.Result ret = CSLibrary.Constants.Result.OK;
                        int time = Environment.TickCount;
                        if ((ret = reader.Connect(ip, 20000)) != CSLibrary.Constants.Result.OK)
                        {
                            reader.Disconnect();
                            Program.Readers[index].connected = false;
                            return;
                        }
                        Program.Readers[index].connected = true;
                        AttachCallback(true, ip);
                    }
                    if (Program.Readers[index].model.ToLower() == "cs469" && reader != null)
                    {
                        CSLibrary.Constants.Result ret = CSLibrary.Constants.Result.OK;
                        int time = Environment.TickCount;
                        if ((ret = reader.Connect(ip, 20000)) != CSLibrary.Constants.Result.OK)
                        {
                            reader.Disconnect();
                            Program.Readers[index].connected = false;
                            return;
                        }
                        Program.Readers[index].connected = true;
                        AttachCallback(true, ip);
                    }
                }
                if (Program.Readers[index].model.ToLower() == "cs101" && handheld != null)
                {
                    //if (handheld == null) return;
                    handheld.Play();
                    Program.Readers[index].connected = true;
                    handheld.NewTagReaded += Handheld_NewTagReaded;
                    AttachCallback(true, ip);
                    return;
                }
                if (Program.Readers[index].model.ToLower() == "virtual" && vr != null)
                {
                    //if (vr == null) return;
                    vr.Play();
                    Program.Readers[index].connected = true;
                    vr.NewTags += Vr_NewTags;
                    vr.connected = true;
                    Program.Readers[index].started = true;
                    AttachCallback(true, ip);
                    return;
                }
                if (Program.Readers[index].model.ToLower() == "remoto" && slave != null)
                {
                    //if (slave == null) return;
                    slave.Play();
                    Program.Readers[index].connected = true;
                    slave.NewTags += Slave_NewTags;
                    slave.connected = true;
                    Program.Readers[index].started = true;
                    AttachCallback(true, ip);
                    return;
                }
                if (reader.State == RFState.IDLE)
                {
                    reader.SetOperationMode(RadioOperationMode.CONTINUOUS);
                    reader.SetSingulationAlgorithmParms(Program.appSetting.Singulation, Program.appSetting.SingulationAlg);
                    //Do Setup on SettingForm

                    reader.Options.TagRanging.multibanks = 0;

                    reader.Options.TagRanging.QTMode = false; // reset to default
                    reader.Options.TagRanging.accessPassword = 0x0; // reset to default

                    reader.SetTagGroup(Program.appSetting.tagGroup);
                    if (Program.appSetting.tagGroup.selected == Selected.ALL)
                    {
                        reader.Options.TagRanging.flags = SelectFlags.ZERO;
                    }
                    else
                    {
                        reader.Options.TagRanging.flags = SelectFlags.SELECT;

                        reader.Options.TagGeneralSelected.flags = SelectMaskFlags.ENABLE_TOGGLE;
                        switch (Program.appSetting.MaskBank)
                        {
                            case 0:
                                reader.Options.TagGeneralSelected.bank = MemoryBank.EPC;
                                reader.Options.TagGeneralSelected.epcMask = new S_MASK(Program.appSetting.Mask);
                                reader.Options.TagGeneralSelected.epcMaskOffset = Program.appSetting.MaskOffset;
                                reader.Options.TagGeneralSelected.epcMaskLength = Program.appSetting.MaskBitLength;
                                break;

                            case 1:
                                reader.Options.TagGeneralSelected.bank = MemoryBank.EPC;
                                reader.Options.TagGeneralSelected.flags |= SelectMaskFlags.ENABLE_PC_MASK;
                                reader.Options.TagGeneralSelected.epcMask = new S_MASK(Program.appSetting.Mask);
                                reader.Options.TagGeneralSelected.epcMaskOffset = Program.appSetting.MaskOffset;
                                reader.Options.TagGeneralSelected.epcMaskLength = Program.appSetting.MaskBitLength;
                                break;

                            case 2:
                            case 3:
                                reader.Options.TagGeneralSelected.bank = (MemoryBank)Program.appSetting.MaskBank;
                                reader.Options.TagGeneralSelected.Mask = CSLibrary.Text.HexEncoding.ToBytes(Program.appSetting.Mask);
                                reader.Options.TagGeneralSelected.MaskOffset = Program.appSetting.MaskOffset;
                                reader.Options.TagGeneralSelected.MaskLength = Program.appSetting.MaskBitLength;
                                break;
                        }
                        reader.StartOperation(Operation.TAG_GENERALSELECTED, true);
                    }
                    if ((Program.Readers[index].model.ToLower() == "cs203" || Program.Readers[index].model.ToLower() == "cs469") && (
                  tgPort1.Checked ||
                  tgPort2.Checked ||
                  tgPort3.Checked ||
                  tgPort4.Checked ||
                  tgPort5.Checked ||
                  tgPort6.Checked ||
                  tgPort7.Checked ||
                  tgPort8.Checked ||
                  tgPort9.Checked ||
                  tgPort10.Checked ||
                  tgPort11.Checked ||
                  tgPort12.Checked ||
                  tgPort13.Checked ||
                  tgPort14.Checked ||
                  tgPort15.Checked
                  ))
                    {
                        reader.AntennaPortSetState(0, tgPort0.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                        reader.AntennaPortSetState(1, tgPort1.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                        reader.AntennaPortSetState(2, tgPort2.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                        reader.AntennaPortSetState(3, tgPort3.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                        reader.AntennaPortSetState(4, tgPort4.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                        reader.AntennaPortSetState(5, tgPort5.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                        reader.AntennaPortSetState(6, tgPort6.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                        reader.AntennaPortSetState(7, tgPort7.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                        reader.AntennaPortSetState(8, tgPort8.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                        reader.AntennaPortSetState(9, tgPort9.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                        reader.AntennaPortSetState(10, tgPort10.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                        reader.AntennaPortSetState(11, tgPort11.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                        reader.AntennaPortSetState(12, tgPort12.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                        reader.AntennaPortSetState(13, tgPort13.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                        reader.AntennaPortSetState(14, tgPort14.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                        reader.AntennaPortSetState(15, tgPort15.Checked ? AntennaPortState.ENABLED : AntennaPortState.DISABLED);
                    }
                    reader.StartOperation(Operation.TAG_RANGING, false);
                    Program.Readers[index].started = true;
                }
                reader.SetPowerLevel((uint)tbPower.Value);
            }
           
        }

        private void Slave_NewTags(object sender, List<Tag> tags)
        {
            //string date = DateTime.Now.ToString();
            foreach (Tag tag in tags)
            {
                try
                {
                    if (this.InvokeRequired)
                    {
                        this.BeginInvoke((System.Threading.ThreadStart)delegate ()
                        {
                            // Do your work here
                            // UI refresh and data processing on other Thread
                            // Notes :  blocking here will cause problem
                            //          Please use asyn call or separate thread to refresh UI
                            if (tag != null)
                            {
                                if ((!Program.appSetting.EnableRssiFilter) ||
                                    (Program.appSetting.EnableRssiFilter && Program.appSetting.RssiFilterThreshold < tag.RSSI))
                                {
                                    Interlocked.Increment(ref totaltags);
                                    S_EPC epc = new S_EPC(tag.EPC);
                                    TagCallbackInfo tci = new TagCallbackInfo(0, tag.RSSI, 1, 300, epc);
                                    UpdateInvUI(tci, tag.IP, tag.TimeStamp);
                                }
                            }
                            else
                            {
                            }
                        });
                    }
                    AntCycleCount++;
                    if (AntCycleTimeCount <= 4)
                    {
                        AntCycleTime[AntCycleTimeCount++] = Environment.TickCount;
                    }
                    else
                    {
                        AntCycleTime[0] = AntCycleTime[1];
                        AntCycleTime[1] = AntCycleTime[2];
                        AntCycleTime[2] = AntCycleTime[3];
                        AntCycleTime[3] = AntCycleTime[4];
                        AntCycleTime[4] = Environment.TickCount;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void Vr_NewTags(object sender, List<Tag> tags)
        {
            string date = DateTime.Now.ToString();
            foreach(Tag tag in tags)
            {
                try
                {
                    if (this.InvokeRequired)
                    {
                        this.BeginInvoke((System.Threading.ThreadStart)delegate ()
                        {
                            // Do your work here
                            // UI refresh and data processing on other Thread
                            // Notes :  blocking here will cause problem
                            //          Please use asyn call or separate thread to refresh UI
                            if (tag != null)
                            {
                                if ((!Program.appSetting.EnableRssiFilter) ||
                                    (Program.appSetting.EnableRssiFilter && Program.appSetting.RssiFilterThreshold < tag.RSSI))
                                {
                                    Interlocked.Increment(ref totaltags);
                                    S_EPC epc = new S_EPC(tag.EPC);
                                    TagCallbackInfo tci = new TagCallbackInfo(0, tag.RSSI, 1, 300, epc);
                                    UpdateInvUI(tci, tag.IP, date);
                                }
                            }
                            else
                            {
                            }
                        });
                    }
                    AntCycleCount++;
                    if (AntCycleTimeCount <= 4)
                    {
                        AntCycleTime[AntCycleTimeCount++] = Environment.TickCount;
                    }
                    else
                    {
                        AntCycleTime[0] = AntCycleTime[1];
                        AntCycleTime[1] = AntCycleTime[2];
                        AntCycleTime[2] = AntCycleTime[3];
                        AntCycleTime[3] = AntCycleTime[4];
                        AntCycleTime[4] = Environment.TickCount;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
           
        }

        private void Handheld_NewTagReaded(Tag tag)
        {
            string date = DateTime.Now.ToString();
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke((System.Threading.ThreadStart)delegate ()
                    {
                        // Do your work here
                        // UI refresh and data processing on other Thread
                        // Notes :  blocking here will cause problem
                        //          Please use asyn call or separate thread to refresh UI
                        if (tag != null)
                        {
                            if ((!Program.appSetting.EnableRssiFilter) ||
                                (Program.appSetting.EnableRssiFilter && Program.appSetting.RssiFilterThreshold < tag.RSSI))
                            {
                                Interlocked.Increment(ref totaltags);
                                S_EPC epc = new S_EPC(tag.EPC);
                                TagCallbackInfo tci = new TagCallbackInfo(0, tag.RSSI, 1, 300, epc);
                                UpdateInvUI(tci, tag.IP, date);
                            }
                        }
                        else
                        {
                        }
                    });
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void Stop(string ip)
        {
            var index = Program.Readers.FindIndex(r => r.ip == ip);
            if (Program.Readers[index].model.ToLower() == "cs203" || Program.Readers[index].model.ToLower() == "cs469")
            {
                var reader = Program.CS203List.Find(delegate (HighLevelInterface h) { return h.Name == ip || h.IPAddress == ip; });
                if (reader.State == RFState.BUSY)
                    reader.StopOperation(true);

                while (reader.State != RFState.IDLE)
                    Thread.Sleep(100);

                // Get current profile for debugging read tag = 0
                {
                    uint profile = 99;

                    reader.GetCurrentLinkProfile(ref profile);

                }
            }
            else if(Program.Readers[index].model.ToLower() == "cs101")
            {
                var handheld = Program.CS101List.Find(delegate (CS101 hh) { return hh.IP == ip; });
                handheld.NewTagReaded -= Handheld_NewTagReaded;
                handheld.Stop();
            }
            else if (Program.Readers[index].model.ToLower() == "virtual")
            {
                var vr = Program.VRList.Find(delegate (VirtualReader hh) { return hh.id == ip; });
                vr.NewTags -= Vr_NewTags;
                vr.Stop();
                vr.connected = false;
            }
            else if (Program.Readers[index].model.ToLower() == "remoto")
            {
                var slave = Program.SlaveList.Find(delegate (SlaveReader hh) { return hh.id == ip; });
                slave.NewTags -= Vr_NewTags;
                slave.Stop();
                slave.connected = false;
            }
            Program.Readers[index].started = false;
        }

        

        public void Clear()
        {
            if (this.InvokeRequired)
            {
                Invoke(new MethodInvoker(Clear));
                return;
            }
            InventoryListItems.Clear();
            m_sortListView.Items.Clear();
            Thread.Sleep(50);
            m_sortListView.Items.Clear();
            Thread.Sleep(50);
            m_sortListView.Items.Clear();
        }
        #endregion

        #region Event Callback
        private void Reset(string ip)
        {
            var index = Program.Readers.FindIndex(r => r.ip == ip && (r.model.ToLower() == "cs203" || r.model.ToLower() == "cs469"));
            var reader = Program.CS203List.Find(delegate (HighLevelInterface h) { return h.Name == ip || h.IPAddress == ip; });
            Result rc = Result.OK;

            if (this.InvokeRequired)
            {
                this.BeginInvoke((System.Threading.ThreadStart)delegate ()
                {
                //-------PENDIENTE-------avisar que la antena entró en reset
                {
                        Application.DoEvents();
                        RETRY:
                    //Reset Reader first, it will shutdown current reader and restart reader
                    //It will also reconfig back previous operation

                    while (reader.Reconnect(1) != Result.OK) ;

                        reader.StartOperation(Operation.TAG_RANGING, false);

                    /*
                                       if ((rc = Program.ReaderXP.Reconnect(10)) == Result.OK)
                                       {
                                            Program.ReaderXP.StartOperation(Operation.TAG_RANGING, false);
                                       }
                                       else
                                       {
                                           //                    ShowMsg(String.Format("ResetReader fail rc = {0}. Do you want to retry?", rc));
                                           if (ShowMsg(String.Format("ResetReader fail rc = {0}. Do you want to retry?", rc)) == DialogResult.Yes)
                                               goto RETRY;

                                           CloseForm();
                                       }
                    */
                    }

                });
            }
            //MessageForm.msgform.CloseForm();
        }

        private delegate DialogResult ShowMsgDeleg(string msg);
        private DialogResult ShowMsg(string msg)
        {
            if (this.InvokeRequired)
            {
                return (DialogResult)this.Invoke(new ShowMsgDeleg(ShowMsg), new object[] { msg });
                //return DialogResult.None;
            }
            return MessageBox.Show(msg, "Retry", MessageBoxButtons.YesNo);
        }

        void ReaderXP_StateChangedEvent(object sender, CSLibrary.Events.OnStateChangedEventArgs e)
        {
            string ip = ((HighLevelInterface)sender).IPAddress;
            var index = Program.Readers.FindIndex(r => r.ip == ip && (r.model.ToLower() == "cs203" || r.model.ToLower() == "cs469" ));
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke((System.Threading.ThreadStart)delegate ()
                    {
                        switch (e.state)
                        {
                            case RFState.IDLE:
                                EnableTimer(false);
                                Program.Readers[index].started = false;
                                if (mStop)
                                    this.Close();
                                break;
                            case RFState.BUSY:
                                EnableTimer(true);
                                Program.Readers[index].started = true;
                                totaltags = 0;
                                break;
                            case RFState.RESET:
                                {
                                    HighLevelInterface reader = (HighLevelInterface)(sender);

                                    while (true)
                                    {
                                        if (reader.Reconnect(1) == Result.OK)
                                            break;

                                        Thread.Sleep(1000);
                                    }
                                }

                            //Use other thread to create progress
                            Reset(ip);
                            //reset = new Thread(new ThreadStart(Reset));
                            //reset.Start();

                            break;
                            case RFState.ABORT:
                            //ControlPanelForm.EnablePannel(false);
                            Program.Readers[index].started = false;
                                break;

                            case RFState.ANT_CYCLE_END:
                                AntCycleCount++;
                                if (AntCycleTimeCount <= 4)
                                {
                                    AntCycleTime[AntCycleTimeCount++] = Environment.TickCount;
                                }
                                else
                                {
                                    AntCycleTime[0] = AntCycleTime[1];
                                    AntCycleTime[1] = AntCycleTime[2];
                                    AntCycleTime[2] = AntCycleTime[3];
                                    AntCycleTime[3] = AntCycleTime[4];
                                    AntCycleTime[4] = Environment.TickCount;
                                }

                                break;
                        }
                    });
                }
            }
            catch(Exception ex) {
                MessageBox.Show(ex.Message);
            }
            
        }

        void ReaderXP_TagInventoryEvent(object sender, CSLibrary.Events.OnAsyncCallbackEventArgs e)
        {
            /*if (!e.info.crcInvalid)
            {
                int thisTick = Environment.TickCount;
                if ((thisTick - lastRingTick) > 250)
                {
                    lastRingTick = thisTick;
                    System.Media.SystemSounds.Beep.Play();
                }
            }*/
            string ip = ((HighLevelInterface)sender).IPAddress;
            string date = DateTime.Now.ToString();
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke((System.Threading.ThreadStart)delegate ()
                    {
                        // Do your work here
                        // UI refresh and data processing on other Thread
                        // Notes :  blocking here will cause problem
                        //          Please use asyn call or separate thread to refresh UI
                        if (!e.info.crcInvalid)
                        {
                            if ((!Program.appSetting.EnableRssiFilter) ||
                                (Program.appSetting.EnableRssiFilter && Program.appSetting.RssiFilterThreshold < e.info.rssi))
                            {
                                Interlocked.Increment(ref totaltags);
                                TagCallbackInfo data = e.info;
                                UpdateInvUI(data, ip, date);
                            }
#if nouse
                    if (Program.appSetting.EnableRssiFilter)
                    {
                        if (Program.appSetting.RssiFilterThreshold < e.info.rssi)
                        {
                            Interlocked.Increment(ref totaltags);
                            TagCallbackInfo data = e.info;
                            UpdateInvUI(data);
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref totaltags);
                        TagCallbackInfo data = e.info;
                        UpdateInvUI(data);
                    }
#endif
                        }
                        else
                        {
                        }
#if nouse

                if (Program.tagLogger != null)
                {
                    Program.tagLogger.Log(CSLibrary.Diagnostics.LogLevel.Info, String.Format("CRC[{0}]:PC[{1}]:EPC[{2}]", e.info.crcInvalid, e.info.pc, e.info.epc));
                }
#endif
                    });
                }
                
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
           
        }

        private void UpdateInvUI(TagCallbackInfo InventoryInformation, string ip, string date)
        {
            if (InventoryInformation.crcInvalid == true)
                return;
            //-- check hold list
            int foundHoldIndex = lock_HoldItems.FindIndex(delegate (HoldTag iepc) { return (iepc.epc.ToString() == InventoryInformation.epc.ToString()); });
            if (foundHoldIndex >= 0) return;

            int foundIndex = lock_InvItems.FindIndex(delegate (CSLibrary.Structures.TagCallbackInfo iepc) { return (iepc.epc.ToString() == InventoryInformation.epc.ToString()); });
            {
                if (foundIndex >= 0 && foundHoldIndex < 1)
                {
                    //found a record
                    lock_InvItems[foundIndex].count++;
                    lock_InvItems[foundIndex].rssi = InventoryInformation.rssi;
                    lock_InvItems[foundIndex].index = foundIndex;
                    lock_InvItems[foundIndex].antennaPort = InventoryInformation.antennaPort;
                    //UI update in separate thread
                    if (HoldTime > 0)
                        HoldListItems.Add(new HoldTag(InventoryInformation.epc.ToString(), HoldTime));
                    UpdateListView(lock_InvItems[foundIndex], ip, date);
                }
                else if (foundHoldIndex < 0)
                {
                    //record no exist
                    //add a record to item list
                    InventoryInformation.index = lock_InvItems.Count;
                    if (tbSearchEPC.Text != "")
                    {
                        if ((InventoryInformation.epc.ToString() + ip + date).Contains(tbSearchEPC.Text))
                            lock_InvItems.Add(InventoryInformation);
                    }
                    else
                    {
                        lock_InvItems.Add(InventoryInformation);
                    }

                    string TagDataStr = InventoryInformation.epc.ToString();
                    string EPCONLY;
                    string TIDONLY = "";
                    string USERONLY = "";

                    //if (TagDataStr.Length > InventoryInformation.pc.EPCLength * 4)
                    //{
                    //    EPCONLY = TagDataStr.Substring(0, (int)InventoryInformation.pc.EPCLength * 4);

                    //}
                    //else
                    EPCONLY = TagDataStr;
                    if (HoldTime > 0)
                        HoldListItems.Add(new HoldTag(InventoryInformation.epc.ToString(), HoldTime));
                    //-----PENDIENTE -------PUBLISH TAGS
                    HTKLibrary.Classes.MDW.Tag tag = new HTKLibrary.Classes.MDW.Tag
                    {
                        direction = -1,
                        epc = InventoryInformation.epc.ToString(),
                        erasetime = 5,
                        ip = ip,
                        rssi = InventoryInformation.rssi,
                        timestamp = DateTime.Now.ToString()
                    };
                    Thread p = new Thread(() => publish.Tag(tag));
                    //AttachCallback(true, ip);
                    p.Start();
                    //UI update in separate thread
                    string _index = InventoryInformation.index.ToString();
                    string _pc = "300";
                    string _xpc_w1 = InventoryInformation.xpc_w1.ToString();
                    string _xpc_w2 = InventoryInformation.xpc_w2.ToString();
                    string _epc = InventoryInformation.epc.ToString();
                    string _tdi = TIDONLY;
                    string _user = USERONLY;
                    string _rssi = InventoryInformation.rssi.ToString();
                    string _count = InventoryInformation.count.ToString();
                    string _antenna_port = InventoryInformation.antennaPort.ToString();
                    string _crc16 = InventoryInformation.crc16.ToString("X4");
                    LVAddItem(_index, _epc, _rssi, "-1" , ip, date);
                }
            }
        }
        private void AttachCallback(bool en, string ip)
        {
            var index = Program.Readers.FindIndex(r => r.ip == ip && (r.model.ToLower() == "cs203"|| r.model.ToLower() == "cs469"));
            var reader = Program.CS203List.Find(delegate (HighLevelInterface h) { return h.Name == ip || h.IPAddress == ip; });
            if (en)
            {
                if(reader!=null)
                {
                    reader.OnStateChanged += new EventHandler<CSLibrary.Events.OnStateChangedEventArgs>(ReaderXP_StateChangedEvent);
                    reader.OnAsyncCallback += new EventHandler<CSLibrary.Events.OnAsyncCallbackEventArgs>(ReaderXP_TagInventoryEvent);
                }
                publish.ToPublish += Publish_ToPublish;
            }
            else
            {
                if(reader!=null)
                {
                    reader.OnAsyncCallback -= new EventHandler<CSLibrary.Events.OnAsyncCallbackEventArgs>(ReaderXP_TagInventoryEvent);
                    reader.OnStateChanged -= new EventHandler<CSLibrary.Events.OnStateChangedEventArgs>(ReaderXP_StateChangedEvent);
                }
                publish.ToPublish -= Publish_ToPublish;
            }
        }

        public HTKLibrary.Classes.MDW.Tag lastTag = new HTKLibrary.Classes.MDW.Tag();
        private void Publish_ToPublish(object sender, HTKLibrary.Classes.MDW.Tag tag)
        {
            if (cbPublishSQL.Checked)
            {
                Connectivity.SQL.Write(tag);
            }
            if (cbPublishXML.Checked)
            {
                HTKLibrary.Comunications.Net35.DB.XML<HTKLibrary.Classes.MDW.Tag>.Write(tag,Program.configManager.XMLPath);
            }
            if (cbPublishCSV.Checked)
            {
                if(tag != lastTag)
                    HTKLibrary.Comunications.Net35.DB.CSV.Write(tag, Program.configManager.CSVPath);
            }
            if(cbPublishWebService.Checked)
            {
                //if(rbVersion2.Checked)
                //{
                    restClient.AddTag(tag);
                //}
                //if (rbVersion1.Checked)
                //{
                //    legacyRestClient.PostData = JsonConvert.SerializeObject(new LegacyTag(tag.epc, tag.rssi.ToString(), tag.ip, tbServiceUser.Text,5, "-1", tag.timestamp));
                //    legacyRestClient.MakeRequest("/api/Tags");
                //}
            }
            if(cbPublishWebSocket.Checked && socketClient.Connected)
            {
                socketClient.SendMessage<HTKLibrary.Classes.MDW.Tag>(tag);
            }
        }

        #endregion

        #region Delegate

        private delegate void LVAddItemDeleg(string index, string epc, string rssi, string direction, string ip, string date);
        //private void LVAddItem(string index, string pc, string epc, string rssi, string count, string antennaPort, string ip, string date)
        //{
        //    if (this.InvokeRequired)
        //    {
        //        this.BeginInvoke(new LVAddItemDeleg(LVAddItem), new object[] { index, pc, epc, rssi, count, antennaPort, ip, date });
        //        return;
        //    }
        //    lock (MyLock)
        //    {
        //        if (tbSearchEPC.Text != "")
        //        {
        //            if (!(epc + ip + date).Contains(tbSearchEPC.Text))
        //                return;
        //        }
        //        ListViewItem item = new ListViewItem(index);
        //        item.Font = new Font("Courier New", 12, FontStyle.Regular);
        //        //item.SubItems.Add(pc);
        //        item.SubItems.Add(date);
        //        item.SubItems.Add(ip);
        //        item.SubItems.Add(epc);
        //        item.SubItems.Add(rssi);
        //        item.SubItems.Add(count);
        //        //item.SubItems.Add(antennaPort);
        //        item.Tag = Environment.TickCount;
        //        item.ForeColor = Color.Green;
        //        item.Font = new Font("Microsoft Sans Serif", 8.5f, FontStyle.Bold);
        //        //item.ForeColor = Color.White;
        //        m_sortListView.Items.Add(item);
        //    }
        //}

        private void LVAddItem(string index, string epc, string rssi, string direction, string ip, string date)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new LVAddItemDeleg(LVAddItem), new object[] { index, epc, rssi, direction, ip, date });
                return;
            }
            lock (MyLock)
                //----Filtro
            {
                bool found = false;
                if (tbSearchEPC.Text != "")
                {
                    if (!(epc + ip + date).Contains(tbSearchEPC.Text))
                        return;
                }
                ListViewItem item = new ListViewItem(index);
                item.Font = new Font("Courier New", 12, FontStyle.Regular);
                //item.SubItems.Add(pc);
                //item.SubItems.Add(xpc_w1);
                //item.SubItems.Add(xpc_w2);
                item.SubItems.Add(date);
                item.SubItems.Add(ip);
                item.SubItems.Add(epc);
                //item.SubItems.Add(tid);
                //item.SubItems.Add(user);
                item.SubItems.Add(rssi);
                item.SubItems.Add(direction);
                //item.SubItems.Add(antennaPort);
                //item.SubItems.Add(crc16);
                item.Tag = Environment.TickCount;
                item.ForeColor = Color.Green;
                item.Font = new Font("Microsoft Sans Serif", 8.5f, FontStyle.Bold);
                //item.ForeColor = Color.White;
                for (int index1 = 0; index1 < m_sortListView.Items.Count; index1++)
                {
                    try
                    {
                        if (m_sortListView.Items[index1].SubItems[3].Text == epc)
                        {
                            found = true;
                            m_sortListView.Items[index1].SubItems[2].Text = ip;
                            m_sortListView.Items[index1].SubItems[4].Text = rssi;
                            m_sortListView.Items[index1].SubItems[5].Text = "-1";
                            m_sortListView.Items[index1].SubItems[1].Text = date;
                            m_sortListView.Items[index1].Tag = Environment.TickCount;
                            m_sortListView.Items[index1].ForeColor = Color.Green;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
                if (!found)
                {
                    try
                    {
                        m_sortListView.Items.Add(item);
                    }
                    catch(Exception ex)
                    {

                    }
                }
            }
        }

        private delegate void UpdateListViewDeleg(TagCallbackInfo item, string ip, string date);
        private void UpdateListView(TagCallbackInfo item, string ip, string date)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new UpdateListViewDeleg(UpdateListView), new object[] { item });
                return;
            }
            lock (MyLock)
            {
                bool found = false;
                for (int index = 0; index < m_sortListView.Items.Count; index++)
                {
                    try
                    {
                        if (m_sortListView.Items[index].SubItems[3].Text == item.epc.ToString())
                        {
                            found = true;
                            m_sortListView.Items[index].SubItems[2].Text = ip;
                            m_sortListView.Items[index].SubItems[4].Text = item.rssi.ToString();
                            m_sortListView.Items[index].SubItems[5].Text = "-1";
                            m_sortListView.Items[index].SubItems[1].Text = date;
                            m_sortListView.Items[index].Tag = Environment.TickCount;
                            m_sortListView.Items[index].ForeColor = Color.Green;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
                if (!found)
                {
                    //ListViewItem lvitem = new ListViewItem(lock_InvItems.Count.ToString());
                    //lvitem.Font = new Font("Courier New", 12, FontStyle.Regular);
                    //lvitem.SubItems.Add(date);
                    //lvitem.SubItems.Add(ip);
                    //lvitem.SubItems.Add(item.epc.ToString());
                    //lvitem.SubItems.Add(item.rssi.ToString());
                    //lvitem.SubItems.Add("1");
                    //lvitem.Tag = Environment.TickCount;
                    //lvitem.ForeColor = Color.Green;
                    //lvitem.Font = new Font("Microsoft Sans Serif", 8.5f, FontStyle.Bold);
                    //m_sortListView.Items.Add(lvitem);
                }
            }
        }

        private delegate void EnableTimerDeleg(bool en);
        private void EnableTimer(bool en)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new EnableTimerDeleg(EnableTimer), new object[] { en });
                return;
            }
            //tmrRowColor.Enabled = tmr_updatelist.Enabled = tmr_hold.Enabled = en;
            tmr_updatelist.Enabled = tmr_hold.Enabled = en;
        }

        #endregion

        #region Timer

        List<ListViewItem> eraselist = new List<ListViewItem>();
        private void tmr_updatelist_Tick(object sender, EventArgs e)
        {
            if(this.InvokeRequired)
            {
                this.BeginInvoke((System.Threading.ThreadStart)delegate ()
                {
                    //tsl_uid.Text = "Tag read = " + lock_InvItems.Count;
                    Interlocked.Exchange(ref totaltags, 0);
                });
            }
        }
        private void tmrRowColor_Tick(object sender, EventArgs e)
        {
#if !DETECT_TAG_BY_TIME
            for (int j = 0; j < eraselist.Count; j++)
            {
                try
                {
                    m_sortListView.Items.Remove(eraselist[j]);
                    InventoryListItems.RemoveAt(j);
                    var found = lock_InvItems.Find(t => t.epc.ToString() == eraselist[j].SubItems[3].Text); InventoryListItems.RemoveAt(j);
                    if (found != null) lock_InvItems.Remove(found);
                }
                catch (Exception ex)
                {
                    //MessageBox.Show(ex.Message);
                }
            }
            eraselist = new List<ListViewItem>();
            for (int i = 0; i < m_sortListView.Items.Count; i++)
            {

                if (tbSearchEPC.Text != "")
                {
                    if (!m_sortListView.Items[i].SubItems[1].Text.Contains(tbSearchEPC.Text) &&
                          !m_sortListView.Items[i].SubItems[2].Text.Contains(tbSearchEPC.Text) &&
                          !m_sortListView.Items[i].SubItems[3].Text.Contains(tbSearchEPC.Text)
                          )
                        eraselist.Add(m_sortListView.Items[i]);
                }
                // Change tag color (OrangeRed < AntCycleTime[0] > OrangeRed < AntCycleTime[1] > YellowGreen < AntCycleTime[2] > LimeGreen < AntCycleTime[3] > Green < AntCycleTime[4] > Green)
                if (AntCycleTimeCount <= 1)
                {
                    m_sortListView.Items[i].ForeColor = Color.Green;
                }
                else
                {
                    int j;

                    for (j = AntCycleTimeCount - 2; j >= 0; j--)
                    {
                       
                        if ((int)m_sortListView.Items[i].Tag > AntCycleTime[j])
                        {
                            switch (AntCycleTimeCount - j)
                            {
                                case 2:
                                    m_sortListView.Items[i].ForeColor = Color.Green;
                                    break;

                                case 3:
                                    m_sortListView.Items[i].ForeColor = Color.LimeGreen;
                                    break;

                                case 4:
                                    m_sortListView.Items[i].ForeColor = Color.YellowGreen;
                                    break;

                                default:
                                    m_sortListView.Items[i].ForeColor = Color.OrangeRed;
                                    eraselist.Add(m_sortListView.Items[i]);
                                    break;
                            }

                            break;
                        }
                    }

                    if (j < 0)
                    {
                        m_sortListView.Items[i].ForeColor = Color.OrangeRed;
                        eraselist.Add(m_sortListView.Items[i]);
                    }
                }
            }
#else

            int curTime = Environment.TickCount;
            for (int i = 0; i < m_sortListView.Items.Count; i++)
            {
                int timeDiff = curTime - (int)m_sortListView.Items[i].Tag;
                if (timeDiff > 5000)
                {
                    m_sortListView.Items[i].ForeColor = Color.OrangeRed;
                }
                else if (timeDiff <= 5000 && timeDiff > 4000)
                {
                    m_sortListView.Items[i].ForeColor = Color.OrangeRed;
                }
                else if (timeDiff <= 4000 && timeDiff > 3000)
                {
                    m_sortListView.Items[i].ForeColor = Color.YellowGreen;
                }
                else if (timeDiff <= 3000 && timeDiff > 2000)
                {
                    m_sortListView.Items[i].ForeColor = Color.LimeGreen;
                }
                else if (timeDiff <= 1000 && timeDiff >= 0)
                {
                    m_sortListView.Items[i].ForeColor = Color.Green;
                }
            }
            //Application.DoEvents();
#endif
        }
        private void tmr_hold_Tick(object sender, EventArgs e)
        {
            HoldListItems.Select(c => { c.time--; return c; }).ToList();
            HoldListItems.RemoveAll(d => d.time < 1);
        }
        #endregion

        #region UpdateUI
        delegate void AntennaUpdater();
        public void UpdateUI_Antennas()
        {
            try
            {
                if (InvokeRequired)
                {
                    // We're not in the UI thread, so we need to call Invoke
                    Invoke(new AntennaUpdater(UpdateUI_Antennas));
                    return;
                }
                //Clear List
                lvReaders.Items.Clear();

                ////Generate new Items
                //ListViewItem[] items = new ListViewItem[copy.Count];

                //for (int i = 0; i < copy.Count; i++)
                //{
                //    ListViewItem item = new ListViewItem();
                //    item.Text = copy[i].IP;
                //    item.SubItems.Add(copy[i].Model + ", " + copy[i].Alias);
                //    item.ImageIndex = copy[i].Connected ? 2 : 0;
                //    items[i] = item;
                //}

                //Populate List
                //lvReaders.Items.AddRange(items);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        #endregion

        #region Buttons
        private void btConnectReader_Click(object sender, EventArgs e)
        {
            if (lvReaders.SelectedItems.Count > 0)
            {
                var ip = lvReaders.SelectedItems[0].Text;
                var subitem = lvReaders.SelectedItems[0].SubItems[1].Text;
                var model = subitem.Split(',')[0].Trim();
                var alias = subitem.Split(',')[1].Trim();

                var index = Program.Readers.FindIndex(r => r.ip == ip);
                //var reader = Program.CS203List.Find(delegate (HighLevelInterface h) { return h.Name == ip || h.IPAddress == ip; });
                if (index < 0) return;

                if (!Program.Readers[index].started)
                {
                    Play(ip);
                }
                else
                {
                    //btConnectReader.BackColor = ThemeColor;
                    //try
                    //{
                    //    Stop(ip);
                    //}
                    //catch (Exception ex) { }
                    //Thread.Sleep(100);
                    //Clear();
                }
            }
        }
        private void btDisconnectReader_Click(object sender, EventArgs e)
        {
            if (lvReaders.SelectedItems.Count > 0)
            {
                var ip = lvReaders.SelectedItems[0].Text;
                var subitem = lvReaders.SelectedItems[0].SubItems[1].Text;
                var model = subitem.Split(',')[0].Trim();
                var alias = subitem.Split(',')[1].Trim();

                var index = Program.Readers.FindIndex(r => r.ip == ip);
                var reader = Program.CS203List.Find(delegate (HighLevelInterface h) { return h.Name == ip || h.IPAddress == ip; });
                var vr = Program.VRList.Find(delegate (VirtualReader v) { return v.id == ip; });
                var slave = Program.SlaveList.Find(delegate (SlaveReader s) { return s.id == ip; });
                if (index < 0) return;

                if (!Program.Readers[index].started)
                {
                    //Play(ip);
                }
                else
                {
                    try
                    {
                        Stop(ip);
                    }
                    catch(Exception ex) { MessageBox.Show(ex.Message); }
                    Thread.Sleep(100);
                    Clear();
                    Thread c = new Thread(() => ClearTags(ip));
                    c.Start();
                }
            }
        }
        object ScrollLock = new object();
       

        private void tbPower_Scroll(object sender, EventArgs e)
        {
            lock (ScrollLock)
            {
                if (lvReaders.SelectedItems.Count > 0)
                {
                    var ip = lvReaders.SelectedItems[0].Text;
                    var subitem = lvReaders.SelectedItems[0].SubItems[1].Text;
                    var model = subitem.Split(',')[0].Trim();
                    var alias = subitem.Split(',')[1].Trim();

                    var index = Program.Readers.FindIndex(r => r.ip == ip && (r.model.ToLower() == "cs203" || r.model.ToLower() == "cs469"));
                    var reader = Program.CS203List.Find(delegate (HighLevelInterface h) { return h.Name == ip || h.IPAddress == ip; });
                    if (index < 0) return;

                    if (Program.Readers[index].started)
                    {
                        Stop(ip);
                        Thread.Sleep(200);
                        Play(ip);
                    }

                }
            }
        }

        private void ClearTags(string ip)
        {
            Thread.Sleep(1000);
            InventoryListItems.Clear();
            if(m_sortListView.InvokeRequired)
            {
                m_sortListView.Invoke(new MethodInvoker( m_sortListView.Items.Clear ));
            }
            else
            {
                m_sortListView.Items.Clear();
            }
        }
        #endregion

        #endregion

        #region Add/Remove Hardware
        private void btAddReader_Click(object sender, EventArgs e)
        {
            int suma = Program.CS101List.Count + Program.CS203List.Count;
            if(suma >= 6)
            {
                MessageBox.Show("Ha llegado al límite de equipos permitidos. Favor de ponerse en contacto con su proveedor.");
                return;
            }
            if (tbAddIP.Text == "")
            {
                lbStatus.Text = "Ingrese la direciión IP del lector";
                return;
            }
            if (cbAddReaderModels.Text != "")
            {
                string ip = tbAddIP.Text;
                string alias = tbAddAlias.Text;
                string model = cbAddReaderModels.Text;
                bool[] ports = new bool[16] {true, false, false, false,
                                            false, false, false, false,
                                            false, false, false, false,
                                            false, false, false, false };


                ReaderModel readermodel = new ReaderModel()
                {
                    ip = ip,
                    alias = alias,
                    connected = false, 
                    model = model 
                };
                Program.Readers.Add(readermodel);
                if (model.ToLower() == "cs203")
                {
                    HighLevelInterface CS203 = new HighLevelInterface();
                    CS203.Name = ip;
                    Program.CS203List.Add(CS203);
                }
                else if (model.ToLower() == "cs469")
                {
                    HighLevelInterface CS469 = new HighLevelInterface();
                    CS469.Name = ip;
                    Program.CS203List.Add(CS469);
                }
                else if(model.ToLower() == "cs101")
                {
                    CS101 cs101 = new CS101(ip);
                    cs101.Alias = alias;
                    Program.CS101List.Add(cs101);
                }
                else if(model.ToLower() == "virtual")
                {
                    VirtualReader vr = new VirtualReader(ip);
                    vr.alias = alias;
                    Program.VRList.Add(vr);
                }
                else if (model.ToLower() == "remoto")
                {
                    SlaveReader slave = new SlaveReader(ip, serverTags);
                    slave.alias = alias;
                    Program.SlaveList.Add(slave);
                }

                tbAddIP.Text = "";
                tbAddAlias.Text = "";
                cbAddReaderModels.Text = "";
                //---aumetar cuenta de hardware
                ListViewItem item = new ListViewItem(ip);
                item.SubItems.Add(model + ", " + alias);
                item.ImageIndex = 0;
                lvReaders.Items.Add(item);
                Tab.Invoke(new MethodInvoker(delegate { Tab.SelectedIndex = Tab_Readers; }));
                lbStatus.Text = "Módulo de lectura de antenas";
                FillConfiguration();
            }
            else
            {
                lbStatus.Text = "Seleccione un modelo de antena";
            }
        }
        private void btToolStripRemoveReader_Click(object sender, EventArgs e)
        {
            if (lvReaders.SelectedItems.Count < 1) return;
            var ip = lvReaders.SelectedItems[0].Text;
            Program.Readers.RemoveAll(x => x.ip == ip);
            Program.CS203List.RemoveAll(x => x.Name == ip || x.IPAddress == ip);
            lvReaders.Items.Remove(lvReaders.SelectedItems[0]);
        }
        #endregion

        #region Configuration
        private void btOpenDirConfigXML_Click(object sender, EventArgs e)
        {
            DialogResult result = SelectFolder.ShowDialog();

            if(!string.IsNullOrEmpty(SelectFolder.SelectedPath))
            {
                tbConfXMLDir.Invoke(new MethodInvoker(delegate { tbConfXMLDir.Text = SelectFolder.SelectedPath; }));
            }
        }
        private void btOpenDirConfigCSV_Click(object sender, EventArgs e)
        {
            DialogResult result = SelectFolder.ShowDialog();

            if (!string.IsNullOrEmpty(SelectFolder.SelectedPath))
            {
                tbConfCSVDir.Invoke(new MethodInvoker(delegate { tbConfCSVDir.Text = SelectFolder.SelectedPath; }));
            }
        }
        private void btSaveConfWService_Click(object sender, EventArgs e)
        {
            Program.configManager.WebServiceURL = tbConfWServiceURL.Text;
        }
        private void btSaveConfSQL_Click(object sender, EventArgs e)
        {
            Program.configManager.SQLServer = tbConfSQLServer.Text;
            Program.configManager.SQLDatabase = tbConfSQLDB.Text;
            Program.configManager.SQLUser = tbConfSQLUser.Text;
            Program.configManager.SQLPassword = tbConfSQLPass.Text;
        }
        private void btSaveConfWSocket_Click(object sender, EventArgs e)
        {
            Program.configManager.SocketIP = tbConfWsocketIP.Text;
            int port = 0;
            int.TryParse(tbConfWSocketPort.Text, out port);
            Program.configManager.SocketPort = port;
        }
        private void btSaveConfXML_Click(object sender, EventArgs e)
        {
            Program.configManager.XMLPath = tbConfXMLDir.Text;
        }
        private void btSaveConfCSV_Click(object sender, EventArgs e)
        {
            Program.configManager.CSVPath = tbConfCSVDir.Text;
        }



        #endregion

        private void cbPublishWebService_CheckedChanged(object sender, EventArgs e)
        {
            if (Program.configManager.WebServiceURL == "") return;
            if(cbPublishWebService.Checked)
            {
                if(rbVersion2.Checked)
                {
                    Activate();
                    if (!Program.configManager.Activated)
                    {
                        if (Program.configManager.User == "" || Program.configManager.Password == "")
                        {
                            MessageBox.Show("Por favor ingrese las credenciales para el web service");
                            using (Login login = new Login())
                            {
                                login.ShowDialog();
                            }
                        }
                        Activate();
                    }
                }
                if(rbVersion1.Checked)
                {
                    legacyRestClient = new RestClient(Program.configManager.WebServiceURL, RestClient.HttpVerb.POST);
                }
            }
        }
        static void Activate()
        {
            //mdw = new HTKLibrary.Classes.MDW.MDWClient(Program.configManager.User, Program.configManager.Password, Program.configManager.MacAddress);
            //restClient = new MDWRestClient(Program.mdwEmail, Program.mdwPwd, Program.configManager.WebServiceURL, mdw);
            //MDWRestClient client = new MDWRestClient("middleware@htk-id.com", "Middleware2016!", "http://192.168.100.62", mdw);
            mdw = new HTKLibrary.Classes.MDW.MDWClient("eleazar.q@htk-id.com", "Middleware2016!", Program.configManager.MacAddress);
            MDWRestClient client = new MDWRestClient("middleware@htk-id.com", "Middleware2016!", "http://200.53.143.165", mdw);
            restClient = new MDWRestClient("eleazar.q@htk-id.com", "Middleware2016!", "http://200.53.143.165", mdw);
            //MDWRestClient client = new MDWRestClient("middleware@htk-id.com", "Middleware2016!", "http://localhost:81", mdw);
            string token = restClient.LoginRestClient();
            if (token != "")
            {
                Program.configManager.Token = token;
                var id = restClient.LoginMDWClient();
                if (id != "")
                {
                    restClient.mdw_client_id = id;
                    Program.configManager.Activated = true;
                    Program.configManager.UserId = id;
                }
            }
            else
                Program.configManager.Activated = false;
        }

        private void cbPublishWebSocket_CheckedChanged(object sender, EventArgs e)
        {
            if (Program.configManager.SocketIP == "") return;
            if(cbPublishWebSocket.Checked)
            {
                //socketClient = new SocketClient(Program.configManager.SocketIP, Program.configManager.SocketPort);
                socketClient = new SocketClient(tbTestIp.Text, Convert.ToInt32(tbTestPort.Text));
                //socketClient = new SocketClient("192.168.169.5",503);
                socketClient.Start();
                //socketClient.ReceiveMessages();
            }
            else
            {
                socketClient.Dispose();
            }
           

        }

        private void rbVersion1_CheckedChanged(object sender, EventArgs e)
        {
            lbServiceUser.Visible = rbVersion1.Checked;
            tbServiceUser.Visible = rbVersion1.Checked;
        }

        private void cbActuators_CheckedChanged(object sender, EventArgs e)
        {
            cbGPO0.Enabled = !cbGPO0.Enabled;
            cbGPO1.Enabled = !cbGPO1.Enabled;
        }

        private void cbGPO0_CheckedChanged(object sender, EventArgs e)
        {
            if (lvReaders.SelectedItems.Count > 0)
            {
                var ip = lvReaders.SelectedItems[0].Text;
                var subitem = lvReaders.SelectedItems[0].SubItems[1].Text;
                var model = subitem.Split(',')[0].Trim();
                var alias = subitem.Split(',')[1].Trim();

                var index = Program.Readers.FindIndex(r => r.ip == ip);
                //var reader = Program.CS203List.Find(delegate (HighLevelInterface h) { return h.Name == ip || h.IPAddress == ip; });
                if (index < 0) return;

                if (!Program.Readers[index].started)
                {
                    CS203.SetGPO0(Program.Readers[index].ip, cbGPO0.Checked);
                }
            }
        }
        private void cbGPO1_CheckedChanged(object sender, EventArgs e)
        {
            if (lvReaders.SelectedItems.Count > 0)
            {
                var ip = lvReaders.SelectedItems[0].Text;
                var subitem = lvReaders.SelectedItems[0].SubItems[1].Text;
                var model = subitem.Split(',')[0].Trim();
                var alias = subitem.Split(',')[1].Trim();

                var index = Program.Readers.FindIndex(r => r.ip == ip);
                //var reader = Program.CS203List.Find(delegate (HighLevelInterface h) { return h.Name == ip || h.IPAddress == ip; });
                if (index < 0) return;

                if (!Program.Readers[index].started)
                {
                    CS203.SetGPO1(Program.Readers[index].ip, cbGPO1.Checked);
                }
            }
        }
    }
}