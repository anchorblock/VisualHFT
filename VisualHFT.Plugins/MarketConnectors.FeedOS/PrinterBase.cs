///-------------------------------
/// FeedOS C# Client API    
/// copyright QuantHouse 2012 
///-------------------------------
using System;
using System.Text;

using FeedOSAPI.Types;

namespace FeedOS_Managed_Sample
{
    public class PrinterBase
    {
        protected static string s_DatePattern = "yyyy-MM-dd";
        protected static string s_TimeSecPattern = "HH:mm:ss:fff";
        protected static string s_TimeMicrosecPattern = "HH:mm:ss:ffffff";
        protected static string s_ShortDateTimePattern = s_DatePattern + " " + s_TimeSecPattern;
        protected static string s_FullDateTimePattern = s_DatePattern + " " + s_TimeMicrosecPattern;

        private static long s_OriginOfTime_CE = 621355968000000000;

        protected static string DumpInstrument(uint instrumentCode)
        {
            return (FeedOSManaged.API.FOSMarketIDInstrument(instrumentCode) + "/" + FeedOSManaged.API.LocalCodeInstrument(instrumentCode));
        }

        protected static string DumpTimestamp(DateTime timestamp, bool full)
        {
            string format = full ? s_FullDateTimePattern : s_ShortDateTimePattern;
            return (s_OriginOfTime_CE != ((DateTime)timestamp).Ticks) ? timestamp.ToString(format) : "(null)"; 
        }

        protected static string DumpTimestamps(UTCTimestamps timestamps)
        {
            string marketTS_str = "null";
            string serverTS_str = "null";
            if(timestamps != null)
            {
                  if(timestamps.Market != null)
                  {
                      marketTS_str = DumpTimestamp((DateTime)timestamps.Market, false);
                  }
                  if(timestamps.Server != null)
                  {
                      serverTS_str = DumpTimestamp((DateTime)timestamps.Server, true);
                  }
            }

            return "TimesUTC(Market=" + marketTS_str + ",Server=" + serverTS_str + ")";
        }

        #region no constructor
        protected PrinterBase()
        {}
        #endregion
    }
}
