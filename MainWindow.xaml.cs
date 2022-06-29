using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using System.IO;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Threading;
using System.Security.Permissions;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Research.DynamicDataDisplay;
using System.ComponentModel;
using PBZ40Panel.VoltageViewModel;
using MahApps.Metro.Controls;
using Microsoft.Research.DynamicDataDisplay.Charts;

namespace PBZ40Panel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        SerialPort serial = new SerialPort();

        public System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
        double crtVoltage = 0, crtCurrent = 0;
        EnumerableDataSource<VoltagePoint> ds;
        public VoltagePointCollection voltagePointCollection = new VoltagePointCollection();
        public string Path = System.AppDomain.CurrentDomain.BaseDirectory;
        public MainWindow()
        {
            InitializeComponent();
            tbc_control.IsEnabled = false;
            cbx_autoupd.IsEnabled = false;
            this.DataContext = this;
            string[] PortNames = SerialPort.GetPortNames();
            foreach (string portname in PortNames)
            {
                Cbx_coms.Items.Add(portname);
            }
            if (PortNames.Length != 0)
                Cbx_coms.Text = PortNames[0];
            dispatcherTimer.Tick += new EventHandler(updatePowerTimer_Tick);
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(500);
            //updateCollectionTimer.Interval = TimeSpan.FromMilliseconds(10);
            //updateCollectionTimer.Tick += new EventHandler(updateCollectionTimer_Tick);
            updateGetexecTimer.Interval = TimeSpan.FromSeconds(1);
            updateGetexecTimer.Tick += new EventHandler(updateGetexecTimer_Tick);

            updateGetlogTimer.Interval = TimeSpan.FromMilliseconds(500);
            updateGetlogTimer.Tick += new EventHandler(updateGetlogTimer_Tick);

            ds = new EnumerableDataSource<VoltagePoint>(voltagePointCollection);
            ds.SetXMapping(x => x.Timestamp);
            ds.SetYMapping(y => y.Voltage);
            plotter_log.AddLineGraph(ds, Colors.Green, 1, "VOL"); // to use this method you need "using Microsoft.Research.DynamicDataDisplay;"

            ds = new EnumerableDataSource<VoltagePoint>(voltagePointCollection);
            ds.SetXMapping(x => x.Timestamp);
            ds.SetYMapping(y => y.Current);
            plotter_log.AddLineGraph(ds, Colors.Red, 1, "CRT"); // to use this method you need "using Microsoft.Research.DynamicDataDisplay;"


            plotter.Children.Remove(plotter.MouseNavigation);
            plotter_log.LegendVisible = false;


            vAxis.LabelProvider = new ToStringLabelProvider();
            vAxis.LabelProvider.LabelStringFormat = "{0}V";
            vAxis.LabelProvider.SetCustomFormatter(info => (info.Tick).ToString());
            hAxis.LabelProvider = new ToStringLabelProvider();
            hAxis.LabelProvider.LabelStringFormat = "{0}";
            hAxis.LabelProvider.SetCustomFormatter(info => {

                if (info.Tick >= 0)
                {
                    DateTime dateTime = new DateTime((long)(info.Tick * 10000000.0f));
                    if (dateTime.Millisecond != 0)
                    {
                        if (dateTime.DayOfYear > 1) return (dateTime.DayOfYear - 1).ToString() + "-" + dateTime.ToString("HH:mm:ss.fff");
                        else if (dateTime.Hour >= 1) return dateTime.ToString("HH:mm:ss.fff");
                        else if (dateTime.Minute >= 1) return dateTime.ToString("mm:ss.fff");
                        else if (dateTime.Second >= 0) return dateTime.ToString("ss.fff");
                        else return dateTime.Millisecond.ToString() + "ms";
                    }
                    if (dateTime.DayOfYear > 1) return (dateTime.DayOfYear - 1).ToString() + "-" + dateTime.ToString("HH:mm:ss");
                    else if (dateTime.Hour >= 1) return dateTime.ToString("HH:mm:ss");
                    else if (dateTime.Minute >= 1) return dateTime.ToString("mm:ss");
                    else if (dateTime.Second >= 0) return dateTime.Second.ToString() + "s";
                    else return "bug";
                }
                else
                    return "";
            });

            hAxislog.LabelProvider = new ToStringLabelProvider();
            hAxislog.LabelProvider.LabelStringFormat = "{0}";
            hAxislog.LabelProvider.SetCustomFormatter(info => {

                if (info.Tick >= 0)
                {
                    DateTime dateTime = new DateTime((long)(info.Tick * 10000000.0f));
                    if (dateTime.Millisecond != 0)
                    {
                        if (dateTime.DayOfYear > 1) return (dateTime.DayOfYear - 1).ToString() + "-" + dateTime.ToString("HH:mm:ss.fff");
                        else if (dateTime.Hour >= 1) return dateTime.ToString("HH:mm:ss.fff");
                        else if (dateTime.Minute >= 1) return dateTime.ToString("mm:ss.fff");
                        else if (dateTime.Second >= 0) return dateTime.ToString("ss.fff");
                        else return dateTime.Millisecond.ToString() + "ms";
                    }
                    if (dateTime.DayOfYear > 1) return (dateTime.DayOfYear - 1).ToString() + "-" + dateTime.ToString("HH:mm:ss");
                    else if (dateTime.Hour >= 1) return dateTime.ToString("HH:mm:ss");
                    else if (dateTime.Minute >= 1) return dateTime.ToString("mm:ss");
                    else if (dateTime.Second >= 0) return dateTime.Second.ToString() + "s";
                    else return "bug";
                }
                else
                    return "";
            });
        }

        private void btn_start_Click(object sender, RoutedEventArgs e)
        {
            if (serial.IsOpen)
                serial.Close();
            serial.PortName = Cbx_coms.Text;
            serial.BaudRate = 19200;
            serial.ReadTimeout = 200;
            serial.WriteTimeout = 200;
            try
            {
                serial.Open();
            }
            catch (Exception)
            {
                lbl_tip.Content = "Failed to establish communication!";
                return;
            }
            if (serial.IsOpen)
            {
                try
                {
                    serial.Write("\n");
                }
                catch (Exception)
                {
                    lbl_tip.Content = "Please connect to an available serial port to connect";
                    return;
                }

                string resp = SendrecvCMD("*IDN?");
                if (resp.IndexOf("KIKUSUI") == -1)
                {
                    lbl_tip.Content = "Please connect to an available serial port to connect";
                    return;
                }
                lbl_tip.Content = GetNumberAlpha(resp) + " Connected! ";

                tbc_control.IsEnabled = true;
                cbx_autoupd.IsEnabled = true;
                lbl_tip.Content = "Communication establish successfully";

                resp = SendrecvCMD("VOLT?");
                tbx_vol.Text = double.Parse(GetNumberAlpha(resp)).ToString();

                resp = SendrecvCMD("OUTP?");
                if (double.Parse(GetNumberAlpha(resp)) == 1)
                    rbt_on.IsChecked = true;
                else
                    rbt_off.IsChecked = true;

                resp = SendrecvCMD("VOLT:LIM:UPP?");
                tbx_setmaxvol.Text = double.Parse(GetNumberAlpha(resp)).ToString();

                resp = SendrecvCMD("CURR:LIM:UPP?");
                tbx_setmaxcrt.Text = double.Parse(GetNumberAlpha(resp)).ToString();

                resp = SendrecvCMD("VOLT:LIM:LOW?");
                tbx_setminvol.Text = double.Parse(GetNumberAlpha(resp)).ToString();

                resp = SendrecvCMD("CURR:LIM:LOW?");
                tbx_setmincrt.Text = double.Parse(GetNumberAlpha(resp)).ToString();

                resp = SendrecvCMD("*IDN?");
                lbl_tip.Content = GetNumberAlpha(resp) + " Connected! ";

                SendCMD("SYST:REMOTE", 1);
                if (cbx_autoupd.IsChecked == true)
                    dispatcherTimer.Start();

            }
            else
                lbl_tip.Content = "Failed to establish communication!";
        }

        private void btn_refrash_Click(object sender, RoutedEventArgs e)
        {
            string[] PortNames = SerialPort.GetPortNames();
            Cbx_coms.Items.Clear();
            foreach (string portname in PortNames)
            {
                Cbx_coms.Items.Add(portname);
            }
            if (PortNames.Length != 0)
                Cbx_coms.Text = PortNames[0];
        }

        public static string GetNumberAlpha(string source)
        {
            string pattern = "[ -z]";
            string strRet = "";
            MatchCollection results = Regex.Matches(source, pattern);
            foreach (var v in results)
            {
                strRet += v.ToString();
            }
            if (strRet == "")
                return "0";
            return strRet;
        }
        public void updatePowerTimer_Tick(object sender, EventArgs e)
        {
            string resp = SendrecvCMD("READ:VOLT?");
            crtVoltage = double.Parse(GetNumberAlpha(resp));
            if (crtVoltage != 0)
                lbl_vol.Content = "Voltage:" + crtVoltage.ToString("F4") + "V";
            else
                lbl_vol.Content = "Voltage:OFF";
            resp = SendrecvCMD("READ:CURR?");
            crtCurrent = double.Parse(GetNumberAlpha(resp));
            if (crtCurrent != 0)
                lbl_crt.Content = "Current:" + crtCurrent.ToString("F4") + "A";
            else
                lbl_crt.Content = "Current:OFF";
        }

        async void Delay(int ms)
        {
            await Task.Delay(ms);
        }
        string SendrecvCMD(string cmd)
        {
            SendCMD(cmd);
            string answer = "";
            try
            {
                answer = serial.ReadLine();
            }
            catch (Exception)
            {
                answer = "";
            }
            tbx_serial.AppendText(DateTime.Now.ToString("[HH:mm:ss.fff]:") + "[RX]" + answer + "\n"); tbx_serial.ScrollToEnd();
            return answer;
        }

        struct ERR { public int index; public string descrip; };

        ERR SendrecvERR()
        {
            ERR err=new ERR();
            if (serial.IsOpen == false)
            {
                err.index = 0xff; err.descrip = "Serial not open!"; return err;
            }
            serial.ReadTimeout = 2000;
            serial.DiscardInBuffer();
            tbx_serial.AppendText(DateTime.Now.ToString("[HH:mm:ss.fff]:") + "[TX]" + "SYST:ERR?" + "\n"); tbx_serial.ScrollToEnd();
            serial.Write("SYST:ERR?" + "\n");
            string answer = "0,\"Timeout\"";
            try
            {
                answer = serial.ReadLine();
            }
            catch (Exception)
            {
            }
            tbx_serial.AppendText(DateTime.Now.ToString("[HH:mm:ss.fff]:") + "[RX]" + answer + "\n"); tbx_serial.ScrollToEnd();
            string[] vs = GetNumberAlpha(answer).Split(new char[] { ',', '\"' });
            err.index = Convert.ToInt32(vs[0]);
            err.descrip = vs[2] + "!";
            serial.ReadTimeout = 200;
            return err;
        }
        void SendCMD(string cmd)
        {
            if (serial.IsOpen == false)
                return;
            serial.DiscardInBuffer();
            tbx_serial.AppendText(DateTime.Now.ToString("[HH:mm:ss.fff]:") + "[TX]" + cmd + "\n"); tbx_serial.ScrollToEnd();
            serial.Write(cmd + "\n");
        }
        void SendCMD(string cmd, int timeout)
        {
            SendCMD(cmd);
            if (timeout != 0)
                Thread.Sleep(timeout);
        }

        private void btn_stop_Click(object sender, RoutedEventArgs e)
        {
            dispatcherTimer.Stop();
            if (serial.IsOpen)
                serial.Close();

            tbc_control.IsEnabled = false;
            lbl_tip.Content = "Communication has been disconnected!";
        }

        private void btn_setvol_Click(object sender, RoutedEventArgs e)
        {
            string cmd = "VOLT " + tbx_vol.Text;
            SendCMD(cmd);
            lbl_tip.Content = "Set voltage to " + tbx_vol.Text + "V";
        }

        private void cbx_autoupd_Checked(object sender, RoutedEventArgs e)
        {
            if (cbx_autoupd.IsChecked == true)
            {
                if (dispatcherTimer.IsEnabled == false)
                    dispatcherTimer.Start();
            }
        }
        private void cbx_autoupd_Unchecked(object sender, RoutedEventArgs e)
        {
            dispatcherTimer.Stop();
            lbl_crt.Content = "Current:N/A";
            lbl_vol.Content = "Voltage:N/A";
        }

        private void rbt_on_Checked(object sender, RoutedEventArgs e)
        {
            if (rbt_on.IsChecked == true)
                SendCMD("OUTP ON");
            lbl_tip.Content = "Power output on!";
        }

        private void rbt_off_Checked(object sender, RoutedEventArgs e)
        {
            if (rbt_off.IsChecked == true)
                SendCMD("OUTP OFF");
            if (lbl_tip != null)
                lbl_tip.Content = "Power output off!";
        }

        private void btn_r01v_Click(object sender, RoutedEventArgs e)
        {
            tbx_vol.Text = ((Convert.ToDouble(tbx_vol.Text) * 10 - 1) / 10).ToString();
            string cmd = "VOLT " + tbx_vol.Text;
            SendCMD(cmd);
            lbl_tip.Content = "Set voltage to " + tbx_vol.Text + "V";
        }

        private void btn_p01v_Click(object sender, RoutedEventArgs e)
        {
            tbx_vol.Text = ((Convert.ToDouble(tbx_vol.Text) * 10 + 1) / 10).ToString();
            string cmd = "VOLT " + tbx_vol.Text;
            SendCMD(cmd);
            lbl_tip.Content = "Set voltage to " + tbx_vol.Text + "V";
        }
        private void btn_r05v_Click(object sender, RoutedEventArgs e)
        {
            tbx_vol.Text = ((Convert.ToDouble(tbx_vol.Text) * 10 - 5) / 10).ToString();
            string cmd = "VOLT " + tbx_vol.Text;
            SendCMD(cmd);
            lbl_tip.Content = "Set voltage to " + tbx_vol.Text + "V";
        }

        private void btn_p05v_Click(object sender, RoutedEventArgs e)
        {
            tbx_vol.Text = ((Convert.ToDouble(tbx_vol.Text) * 10 + 5) / 10).ToString();
            string cmd = "VOLT " + tbx_vol.Text;
            SendCMD(cmd);
            lbl_tip.Content = "Set voltage to " + tbx_vol.Text + "V";
        }

        private void cbx_topmost_Checked(object sender, RoutedEventArgs e)
        {
            this.Topmost = true;
        }

        private void cbx_topmost_Unchecked(object sender, RoutedEventArgs e)
        {
            this.Topmost = false;
        }

        private void btn_setmaxvol_Click(object sender, RoutedEventArgs e)
        {
            string cmd = "VOLT:LIM:UPP " + tbx_setmaxvol.Text;
            SendCMD(cmd);
            lbl_tip.Content = "Set Limit upper voltage to " + tbx_setmaxvol.Text + "V";
        }

        private void btn_setmaxcrt_Click(object sender, RoutedEventArgs e)
        {
            string cmd = "CURR:LIM:UPP " + tbx_setmaxcrt.Text;
            SendCMD(cmd);
            lbl_tip.Content = "Set Limit upper current to " + tbx_setmaxcrt.Text + "V";
        }

        private void btn_setminvol_Click(object sender, RoutedEventArgs e)
        {
            string cmd = "VOLT:LIM:LOW " + tbx_setminvol.Text;
            SendCMD(cmd);
            lbl_tip.Content = "Set Limit lower voltage to " + tbx_setminvol.Text + "V";
        }

        private void btn_setmincrt_Click(object sender, RoutedEventArgs e)
        {
            string cmd = "CURR:LIM:LOW " + tbx_setmincrt.Text;
            SendCMD(cmd);
            lbl_tip.Content = "Set Limit lower current to " + tbx_setmincrt.Text + "V";
        }

        private double _maxVoltagelog;
        public double MaxVoltagelog
        {
            get { return _maxVoltagelog; }
            set { _maxVoltagelog = value; this.OnPropertyChanged("MaxVoltagelog"); }
        }

        private double _minVoltagelog;
        public double MinVoltagelog
        {
            get { return _minVoltagelog; }
            set { _minVoltagelog = value; this.OnPropertyChanged("MinVoltagelog"); }
        }

        private double _maxVoltage;
        public double MaxVoltage
        {
            get { return _maxVoltage; }
            set { _maxVoltage = value; this.OnPropertyChanged("MaxVoltage"); }
        }

        private double _minVoltage;
        public double MinVoltage
        {
            get { return _minVoltage; }
            set { _minVoltage = value; this.OnPropertyChanged("MinVoltage"); }
        }

        private double _timeline1;
        public double Timeline1
        {
            get { return _timeline1; }
            set { _timeline1 = value; this.OnPropertyChanged("Timeline1"); }
        }

        private double _timeline2;
        public double Timeline2
        {
            get { return _timeline2; }
            set { _timeline2 = value; this.OnPropertyChanged("Timeline2"); }
        }

        private double _timeline3;
        public double Timeline3
        {
            get { return _timeline3; }
            set { _timeline3 = value; this.OnPropertyChanged("Timeline3"); }
        }
        private double _timestamp;
        public double Timestamp
        {
            get { return _timestamp; }
            set { _timestamp = value; this.OnPropertyChanged("Timestamp"); }
        }

        #region INotifyPropertyChanged members

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                this.PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        #endregion

        LineGraph graphAcc_view = new LineGraph();
        //LineGraph graphAcc_rel = new LineGraph();

        double inittime, offtime, t1time, t3time, endtime;
        double nomv, minv;

        void get_parameter()
        {
            inittime = Convert.ToDouble(tbx_ginit.Text);
            offtime = Convert.ToDouble(tbx_goff.Text);
            endtime = Convert.ToDouble(tbx_gend.Text);
            t1time = Convert.ToDouble(tbx_gt1.Text);
            t3time = Convert.ToDouble(tbx_gt3.Text);

            nomv = Convert.ToDouble(tbx_gnom.Text);
            minv = Convert.ToDouble(tbx_gmin.Text);
        }
        //        选择当前序列
        //PROG:NAME "1"
        //清除序列
        //PROG:EDIT:DEL
        //添加步数
        //PROG:EDIT:ADD 5
        //设置循环次数
        //PROG:EDIT:LOOP 5
        //选择某一步
        //PROG:EDIT:SEL 1
        //设置电压
        //PROG:EDIT:STEP:VOLT 12
        //设置时间
        //PROG:EDIT:STEP:TIME 1

        //启动序列
        //PROG:EXEC:STAT {RUN|PAUSE|STOP|CONT
        //    }
        //    查询状态
        //    PROG:EXEC?
        //{状态，运行时间，循环次数，当前步，当前序列
        //}
        void set_program()
        {
            SendCMD("PROG:NAME \"1\"", 2);
            SendCMD("PROG:EDIT:DEL", 2);
            SendCMD("PROG:EDIT:ADD 5", 2);
            SendCMD("PROG:EDIT:LOOP 10001", 2);
            SendCMD("PROG:EDIT:STEP:SEL 1", 2);
            SendCMD("PROG:EDIT:STEP:VOLT " + nomv.ToString(), 2);
            SendCMD("PROG:EDIT:STEP:TIME " + (inittime / 1000).ToString(), 2);

            SendCMD("PROG:EDIT:STEP:SEL 2", 2);
            SendCMD("PROG:EDIT:STEP:VOLT " + "0", 2);
            SendCMD("PROG:EDIT:STEP:TIME " + (offtime / 1000).ToString(), 2);

            SendCMD("PROG:EDIT:STEP:SEL 3", 2);
            SendCMD("PROG:EDIT:STEP:VOLT " + nomv.ToString(), 2);
            SendCMD("PROG:EDIT:STEP:TIME " + (t3time / 1000).ToString(), 2);

            SendCMD("PROG:EDIT:STEP:SEL 4", 2);
            SendCMD("PROG:EDIT:STEP:VOLT " + minv.ToString(), 2);
            SendCMD("PROG:EDIT:STEP:TIME " + (t1time / 1000).ToString(), 2);

            SendCMD("PROG:EDIT:STEP:SEL 5", 2);
            SendCMD("PROG:EDIT:STEP:VOLT " + nomv.ToString(), 2);
            SendCMD("PROG:EDIT:STEP:TIME " + (endtime / 1000).ToString(), 2);
        }

        private void btn_gview_Click(object sender, RoutedEventArgs e)
        {
            SendCMD("*CLS");
            plotter.Children.Remove(graphAcc_view);
            ObservableDataSource<Point> currentDataFrame = new ObservableDataSource<Point>();
            graphAcc_view = plotter.AddLineGraph(currentDataFrame, Colors.Red, 1, "VIEW");  //注册绘图图线，配置粗细颜色以及显示名称

            get_parameter();

            MaxVoltage = nomv;
            MinVoltage = minv;

            Point point = new Point(0, 0);
            point.X = 0 / 1000;
            point.Y = nomv;
            currentDataFrame.AppendAsync(base.Dispatcher, point);

            point.X = inittime / 1000;
            point.Y = nomv;
            currentDataFrame.AppendAsync(base.Dispatcher, point);
            point.X = inittime / 1000;
            point.Y = 0;
            currentDataFrame.AppendAsync(base.Dispatcher, point);

            point.X = (inittime + offtime) / 1000;
            point.Y = 0;
            currentDataFrame.AppendAsync(base.Dispatcher, point);
            point.X = (inittime + offtime) / 1000;
            point.Y = nomv;
            currentDataFrame.AppendAsync(base.Dispatcher, point);
            Timeline1 = (inittime + offtime) / 1000;
            point.X = (inittime + offtime + t3time) / 1000;
            point.Y = nomv;
            currentDataFrame.AppendAsync(base.Dispatcher, point);
            point.X = (inittime + offtime + t3time) / 1000;
            point.Y = minv;
            currentDataFrame.AppendAsync(base.Dispatcher, point);
            Timeline2 = (inittime + offtime + t3time) / 1000;
            point.X = (inittime + offtime + t1time + t3time) / 1000;
            point.Y = minv;
            currentDataFrame.AppendAsync(base.Dispatcher, point);
            point.X = (inittime + offtime + t1time + t3time) / 1000;
            point.Y = nomv;
            currentDataFrame.AppendAsync(base.Dispatcher, point);
            Timeline3 = (inittime + offtime + t1time + t3time) / 1000;
            point.X = (inittime + offtime + t1time + t3time + endtime) / 1000;
            point.Y = nomv;
            currentDataFrame.AppendAsync(base.Dispatcher, point);

            plotter.FitToView();
            set_program();
            btn_vstart.IsEnabled = true;
            ERR err = SendrecvERR();
            if (err.index == 0)
            {
                lbl_tip.Content = "Sequence import device successfully!";
                EXECstate = "Standby";
            }
            else
            {
                lbl_tip.Content = err.descrip;
                EXECstate = "Error";
            }
        }


        private void plotter_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point mousePos = ccg_graph.Position;
            var transform = plotter.Viewport.Transform;
            Point mousePosInData = mousePos.ScreenToData(transform);
            double yValue = mousePosInData.Y;

            if (updateGetexecTimer.IsEnabled == false)
            {
                lbl_tip.Content = "Set voltage to " + yValue.ToString("F1") + "V";
                tbx_vol.Text = yValue.ToString("F1");
                string cmd = "VOLT " + tbx_vol.Text;
                SendCMD(cmd);
            }
        }

        private void btn_vstop_Click(object sender, RoutedEventArgs e)
        {
            if (updateGetexecTimer.IsEnabled)
            {
                Timestamp = 0;
                updateGetexecTimer.Stop();
                SendCMD("PROG:EXEC:STAT STOP", 150);
                lbl_tip.Content = "Successfully ended the process!";
                getexec();
                if (cbx_offaftexec.IsChecked == true)
                {
                    rbt_off.IsChecked = true;
                }
            }
            btn_vstart.IsEnabled = true;
            btn_gview.IsEnabled = true;
            btn_vstop.IsEnabled = false;
            cbx_autoupd.IsEnabled = true;
            if (cbx_autolog.IsChecked == true)
                swlog(false);
        }


        //DispatcherTimer updateCollectionTimer = new DispatcherTimer();
        DispatcherTimer updateGetexecTimer = new DispatcherTimer();
        DispatcherTimer updateGetlogTimer = new DispatcherTimer();
        long timestamp = 0, timestamprun = 0;

        private void btn_13halfv_Click(object sender, RoutedEventArgs e)
        {
            tbx_vol.Text = "13.5";
            string cmd = "VOLT " + tbx_vol.Text;
            SendCMD(cmd);
            lbl_tip.Content = "Set voltage to " + tbx_vol.Text + "V";
        }

        private void btn_5v_Click(object sender, RoutedEventArgs e)
        {
            tbx_vol.Text = "5";
            string cmd = "VOLT " + tbx_vol.Text;
            SendCMD(cmd);
            lbl_tip.Content = "Set voltage to " + tbx_vol.Text + "V";
        }

        private void btn_17v_Click(object sender, RoutedEventArgs e)
        {
            tbx_vol.Text = "17";
            string cmd = "VOLT " + tbx_vol.Text;
            SendCMD(cmd);
            lbl_tip.Content = "Set voltage to " + tbx_vol.Text + "V";
        }

        private void btn_cls_Click(object sender, RoutedEventArgs e)
        {
            SendCMD("*CLS");
            lbl_tip.Content = "Clear all DTC!";
        }

        private void btn_12v_Click(object sender, RoutedEventArgs e)
        {
            tbx_vol.Text = "12";
            string cmd = "VOLT " + tbx_vol.Text;
            SendCMD(cmd);
            lbl_tip.Content = "Set voltage to " + tbx_vol.Text + "V";
        }

        //private System.Threading.Timer threadingTimer;
        private void btn_vstart_Click(object sender, RoutedEventArgs e)
        {

            //ds.SetXMapping(x => x.Timestamp);
            //ds.SetYMapping(y => y.Voltage);
            //graphAcc_rel = plotter.AddLineGraph(ds, Colors.OliveDrab, 1, "REL"); // to use this method you need "using Microsoft.Research.DynamicDataDisplay;"
            rbt_on.IsChecked = true;
            get_parameter();
            //updateCollectionTimer.Start();
            updateGetexecTimer.Start();
            //threadingTimer = new System.Threading.Timer(new System.Threading.TimerCallback(ThreadMethod), null, -1, 30);

            SendCMD("PROG:EXEC:STAT RUN", 2);
            timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            timestamprun = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            lbl_tip.Content = "Successfully started the process!";
            btn_gview.IsEnabled = false;
            btn_vstop.IsEnabled = true;
            btn_vstart.IsEnabled = false;
            if (cbx_autolog.IsChecked == true)
                swlog(true);
        }
        public string EXECstate
        {
            get { return tbx_runstate.Text; }
            set
            {
                tbx_runstate.Text = value;
                if (value == "Standby")
                    tbx_runstate.Background = Brushes.CadetBlue;
                else if (value == "RUN")
                    tbx_runstate.Background = Brushes.GreenYellow;
                else if (value == "STOP")
                    tbx_runstate.Background = Brushes.Yellow;
                else if (value == "Error")
                    tbx_runstate.Background = Brushes.Red;
            }
        }

        void getexec()
        {
            string resp = SendrecvCMD("PROG:EXEC?");
            if (resp != "")
            {
                string originalstr = GetNumberAlpha(resp);
                char[] delimiterChars = { '{', ',', '}' };
                string[] usermsg = originalstr.Split(delimiterChars);

                EXECstate = usermsg[0];
                long runtime = (long)(double.Parse(usermsg[1]) * 10000000.0f);
                DateTime dateTime = new DateTime(runtime);

                tbx_steptime.Text = dateTime.ToString("HH:mm:ss.ff");
                tbx_repection.Text = usermsg[2];
                if (usermsg[3] != "-1")
                    tbx_step.Text = usermsg[3];
                else tbx_step.Text = "NON";

                //point.X = (inittime + offtime + t1time + t3time + endtime) / 1000;
                switch (usermsg[3])
                {
                    case "1": Timestamp = double.Parse(usermsg[1]); break;
                    case "2": Timestamp = double.Parse(usermsg[1]) + inittime / 1000; break;
                    case "3": Timestamp = double.Parse(usermsg[1]) + (inittime + offtime) / 1000; break;
                    case "4": Timestamp = double.Parse(usermsg[1]) + (inittime + offtime + t3time) / 1000; break;
                    case "5": Timestamp = double.Parse(usermsg[1]) + (inittime + offtime + t3time + t1time) / 1000; break;
                }
            }
            else
            {
                EXECstate = "Error";
                if (updateGetexecTimer.IsEnabled)
                {
                    Timestamp = 0;
                    updateGetexecTimer.Stop();
                    lbl_tip.Content = "Error!";
                }
                cbx_autoupd.IsEnabled = true;
                //if (updateGetexecTimer.IsEnabled)
                //    updateGetexecTimer.Stop();
                btn_vstart.IsEnabled = true;
                btn_gview.IsEnabled = true;
                btn_vstop.IsEnabled = false;
            }
        }

        //void updateCollectionTimer_Tick(object sender, EventArgs e)
        //{
        //    Timestamp = (new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds() - timestamp)/1000;
        //    //if(timestamp*30<inittime)
        //    //voltagePointCollection.Add(new VoltagePoint(crtVoltage, Timestamp));
        //    //else if (timestamp * 30 < inittime)
        //    //    voltagePointCollection.Add(new VoltagePoint(nomv, timestamp * 30));
        //    //Timestamp += 10;
        //    if (Timestamp > ((inittime + offtime + t1time + t3time + endtime)/1000))
        //    {
        //        timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
        //        //voltagePointCollection.Clear();
        //    }
        //}
        void updateGetexecTimer_Tick(object sender, EventArgs e)
        {
            getexec();
            DateTime dateTime = new DateTime((new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds() - timestamprun) * 10000);
            tbx_runtime.Text = dateTime.ToString("HH:mm:ss.ff");
        }
        long timestamplog = 0;

        private void btn_readvolt_Click(object sender, RoutedEventArgs e)
        {
            string resp = SendrecvCMD("READ:VOLT?");
            crtVoltage = double.Parse(GetNumberAlpha(resp));
            if (crtVoltage != 0)
                lbl_vol.Content = "Voltage:" + crtVoltage.ToString("F4") + "V";
            else
                lbl_vol.Content = "Voltage:OFF";
        }

        private void TabItem_GotFocus(object sender, RoutedEventArgs e)
        {
            tbx_serial.ScrollToEnd();
        }

        private void btn_readerr_Click(object sender, RoutedEventArgs e)
        {
            ERR err = SendrecvERR();
            if (err.index == 0)
            {
                lbl_tip.Content = "No error!";
            }
            else
            {
                lbl_tip.Content = err.descrip;
            }
        }

        bool logcmd = true;

        void swlog(bool b)
        {
            if (b)
            {
                if (logcmd == false) return;
                btn_controllog.Content = "Stop Log";
                voltagePointCollection.Clear();
                updateGetlogTimer.Start();
                timestamplog = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
                //voltagePointCollection.Add(new VoltagePoint(30, 0));
                //voltagePointCollection.Add(new VoltagePoint(0, 0));

                plotter_log.FitToView();
                if (cbx_autoupd.IsChecked == false)
                    cbx_autoupd.IsChecked = true;
                cbx_autoupd.IsEnabled = false;
                //voltagePointCollection.Clear();
                voltagePointCollection.Add(new VoltagePoint(crtVoltage, crtCurrent, 0));
                logcmd = false;
            }
            else
            {
                if (logcmd == true) return;
                cbx_autoupd.IsEnabled = true;
                btn_controllog.Content = "Start Log";
                updateGetlogTimer.Stop();
                logcmd = true;
                log();
                lbl_tip.Content = "Log is saved by " + Path + "log!";
                //Logmsg();
                //Path
            }
        }
        //private void SaveImageToFile(BitmapSource image, string filePath)
        //{
        //    BitmapEncoder encoder = new JpegBitmapEncoder();
        //    encoder.Frames.Add(BitmapFrame.Create(image));

        //    using (var stream = new FileStream(filePath, FileMode.Create))
        //    {
        //        encoder.Save(stream);
        //    }
        //}
        void log()
        {
            string filepath = Path + "\\log" + "\\CV_B_" + System.DateTime.Now.ToString("MM-dd-HH_mm_ss_ff") + ".txt";
            string bmppath = Path + "\\log" + "\\CV_B_" + System.DateTime.Now.ToString("MM-dd-HH_mm_ss_ff") + ".png";
            // 判断文件是否存在，不存在则创建，否则读取值显示到txt文档
            if (!System.IO.File.Exists(filepath))
            {
                FileStream fs1 = new FileStream(filepath, FileMode.Create, FileAccess.Write);//创建写入文件 
                StreamWriter sw = new StreamWriter(fs1);
                string title = "Time[s]\tVoltage[V]\tCurrent[A]";
                sw.WriteLine(title);
                foreach (var v in voltagePointCollection)
                {
                    sw.WriteLine(v.Timestamp.ToString("F3") + "\t" + v.Voltage.ToString("F3") + "\t" + v.Current.ToString("F3"));
                }
                sw.Close();
                fs1.Close();
                //SaveImageToFile(plotter_log.CreateScreenshot(), bmppath);
                plotter_log.SaveScreenshot(bmppath);
                //plotter_log.CopyScreenshotToClipboard();
                //RenderTargetBitmap rtbmp = new RenderTargetBitmap((int)plotter_log.ActualWidth, (int)plotter_log.ActualHeight, plotter_log.ActualWidth/4, plotter_log.ActualHeight/4, PixelFormats.Default);
                //rtbmp.Render(plotter_log);
                //PngBitmapEncoder encode = new PngBitmapEncoder();
                //encode.Frames.Add(BitmapFrame.Create(rtbmp));
                //MemoryStream ms = new MemoryStream();
                //encode.Save(ms);
                //System.Drawing.Image MyImage = System.Drawing.Image.FromStream(ms);
                //MyImage.Save(bmppath);
            }
            else
            {
                FileStream fs = new FileStream(filepath, FileMode.Append, FileAccess.Write);
                StreamWriter sr = new StreamWriter(fs);
                string title = "Time[s]\tVoltage[V]\tCurrent[A]";
                sr.WriteLine(title);
                foreach (var v in voltagePointCollection)
                {
                    sr.WriteLine(v.Timestamp.ToString("F3") + "\t" + v.Voltage.ToString("F3") + "\t" + v.Current.ToString("F3"));
                }
                sr.Close();
                fs.Close();
                //SaveImageToFile(plotter_log.CreateScreenshot(), bmppath);
                plotter_log.SaveScreenshot(bmppath);
                //plotter_log.CopyScreenshotToClipboard();
            }
        }

        private void btn_controllog_Click(object sender, RoutedEventArgs e)
        {
            if (logcmd)
            {
                swlog(true);
            }
            else
            {
                swlog(false);
            }

        }
        void updateGetlogTimer_Tick(object sender, EventArgs e)
        {
            double TimestampLog = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds() - timestamplog;
            if (crtVoltage != 0 && crtCurrent != 0)
                voltagePointCollection.Add(new VoltagePoint(crtVoltage, crtCurrent, TimestampLog / 1000));
            if (MaxVoltagelog < crtVoltage)
                MaxVoltagelog = crtVoltage;
            if (MinVoltagelog > crtVoltage)
                MinVoltagelog = crtVoltage;
        }
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (updateGetexecTimer.IsEnabled)
            {
                if (cbx_offaftexec.IsChecked == true)
                    SendCMD("OUTP OFF", 5);
                updateGetexecTimer.Stop();
                SendCMD("PROG:EXEC:STAT STOP");
            }

        }
        private void btn_analyze_Click(object sender, RoutedEventArgs e)
        {
            string path = "";
            var openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "LOG Files (*.txt)|*.txt"
            };
            var result = openFileDialog.ShowDialog();
            if (result == true)
            {
                path = openFileDialog.FileName;
            }
            else return;
            ShowLOG showlog = new ShowLOG(this, path);
            showlog.Show();
        }
    }
}
