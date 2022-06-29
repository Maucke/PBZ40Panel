using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Research.DynamicDataDisplay.Common;

namespace PBZ40Panel.VoltageViewModel
{
    public class VoltagePointCollection : RingArray<VoltagePoint>
    {
        private const int TOTAL_POINTS = 20000;//预留20s

        public VoltagePointCollection()
            : base(TOTAL_POINTS) // here i set how much values to show 
        {
        }
    }

    public class VoltagePoint
    {
        public double Timestamp { get; set; }
        public double Voltage { get; set; }
        public double Current { get; set; }

        public VoltagePoint(double voltage,double current, double Timestamp)
        {
            this.Timestamp = Timestamp;
            this.Current = current;
            this.Voltage = voltage;
        }
    }
}
