using VisualHFT.Model;
using VisualHFT.UserSettings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketConnectors.FeedOS.Model
{
    public class PlugInSettings : ISetting
    {
        // Implementing the ISetting interface
        public string Symbol
        {
            get { return Instruments.FirstOrDefault(); }
            set
            {
                if (Instruments.Count == 0)
                {
                    Instruments.Add(value);
                }
                else
                {
                    if (!Instruments.Contains(value))
                    {
                        Instruments[0] = value;
                    }
                    else
                    {
                        // Handle duplicate instrument or ignore
                        // You may want to throw an exception, log a warning, or take other action
                        // For now, let's just ignore the duplicate
                        Console.WriteLine($"Duplicate instrument '{value}' ignored.");
                    }
                }
            }
        }

        // Additional properties for your specific implementation
        public string HostIP { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int RequestId { get; set; }

        public List<string> Instruments { get; }

        // Implementing the rest of the interface
        public Provider Provider { get; set; }
        public AggregationLevel AggregationLevel { get; set; }

        public PlugInSettings()
        {
            // Initialize Instruments property
            Instruments = new List<string>();
        }
    }
}
