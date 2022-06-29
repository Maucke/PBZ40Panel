using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Shapes;
using MahApps.Metro.Controls;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.Charts;
using Microsoft.Research.DynamicDataDisplay.DataSources;

namespace PBZ40Panel
{
    /// <summary>
    /// ShowLOG.xaml 的交互逻辑
    /// </summary>
    public partial class ShowLOG : MetroWindow
    {
        MainWindow parent;
        public ShowLOG(MainWindow parent,string path)
        {
            InitializeComponent();
            this.parent = parent;
            wdo_log.Title = path;
            ObservableDataSource<Point> voltageDataFrame = new ObservableDataSource<Point>();
            ObservableDataSource<Point> currentDataFrame = new ObservableDataSource<Point>();
            plotter_show.AddLineGraph(voltageDataFrame, Colors.Green, 1, "V ");  //注册绘图图线，配置粗细颜色以及显示名称
            plotter_show.AddLineGraph(currentDataFrame, Colors.Red, 1, "A ");  //注册绘图图线，配置粗细颜色以及显示名称
            StreamReader sr = new StreamReader(path, Encoding.GetEncoding("GB2312"));
            String line = sr.ReadLine();
            Point point = new Point(0, 0);
            Point pointcrt = new Point(0, 0);
            while ((line = sr.ReadLine()) != null)
            {
                string[] parts = line.Split('\t');
                point.X = Convert.ToDouble(parts[0]);
                point.Y = Convert.ToDouble(parts[1]);
                pointcrt.X = Convert.ToDouble(parts[0]);
                pointcrt.Y = Convert.ToDouble(parts[2]);

                voltageDataFrame.AppendAsync(base.Dispatcher, point);
                currentDataFrame.AppendAsync(base.Dispatcher, pointcrt);
            }

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
            plotter_show.FitToView();
        }
    }
}
