///-------------------------------
/// FeedOS C# Client API    
/// copyright QuantHouse 2012 
///-------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
// FeedOS
using FeedOSAPI.Types;

namespace FeedOS_Managed_Sample
{
    public class PrinterMBL : PrinterBase
    {
        #region Types and constants

        private enum BidOrAskSide
        {
            BidSide,
            AskSide
        };

        public enum MBLOutputMode
        {
            Events,
            Cache,
            EventsAndCache
        };

        private const int HALFBOOK_CHAR_SIZE = 39;
        private bool m_DoMergeLayers;
        private uint m_MergeTargetLayerId;
        private bool m_Verbose;
        private MBLOutputMode m_OutputMode;

        #endregion
        
        #region Static data

        private static Dictionary<BidOrAskSide, string> s_Legend;
        private static char[] s_CompleteEmptyLine;
        private static char[] s_UncompleteEmptyLine;
        private static CultureInfo s_CultureInfo = null;

        #endregion

        #region Static tools

        static PrinterMBL()
        {
            s_Legend = new Dictionary<BidOrAskSide, string>(2);
            s_Legend[BidOrAskSide.BidSide] = "BID";
            s_Legend[BidOrAskSide.AskSide] = "ASK";
            s_CompleteEmptyLine = new char[HALFBOOK_CHAR_SIZE];
            s_UncompleteEmptyLine = new char[HALFBOOK_CHAR_SIZE];
            for (int i = 0; i < HALFBOOK_CHAR_SIZE; i++)
            {
                s_CompleteEmptyLine[i] = '*';
                s_UncompleteEmptyLine[i] = ' ';
            }

            s_CultureInfo = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        }

        private static void PrintInstrumentAndBidAskSizesAndTimestamps(string prefix, 
                                                                      uint instrumentCode,
                                                                      uint layerid,
                                                                      int bidSize, 
                                                                      int askSize, 
                                                                      UTCTimestamps timestamps)
        {
            Console.Write(prefix + "\t" + DumpInstrument(instrumentCode) + " L(" + layerid + ") bid(" + bidSize + ") ask(" + askSize + ") " + DumpTimestamps(timestamps));
        }

        private static void PrintOtherValues(List<TagNumAndValue> otherValues)
        {
            if (null != otherValues && (0 != otherValues.Count))
            {
                Console.Write("OtherValues:");
                foreach (TagNumAndValue tagNumAndVal in otherValues)
                {
                    string tagName = FeedOSManaged.API.TagNum2String(tagNumAndVal.Num);
                    // "Known" tag case
                    if (null != tagName)
                    {
                        if (null != tagNumAndVal.Value)
                        {
                            Console.Write("\t" + tagName + " : " + tagNumAndVal.Value);
                        }
                        else
                        {
                            // Means the tag value will be erased after update on MBLLayer 
                            Console.Write("\t*" + tagName);    
                        }
                    }
                    // "Unknown" tag case
                    else
                    {
                        Console.Write("\t" + (uint)tagNumAndVal.Num + " : " + tagNumAndVal.Value);
                    }
                }
                Console.Write("\n");
            }
        }

        private static string DumpMBLHeader(uint instrumentCode, uint layer_id)
        {
            return DumpInstrument(instrumentCode) + " L(" + layer_id + ") "; 
        }

        private static void PrintMBLOrderBookEntry(BidOrAskSide side, int entry_level, MBLOrderBookEntry entry, bool complete)
        {
            string prefix = (BidOrAskSide.BidSide == side) ? "\n\t" + entry_level + "\t" : "\t";
            if (null == entry)
            {
                Console.Write("{0}",prefix);
                if (complete)
                {
                    Console.Write(s_CompleteEmptyLine);
                }
                else
                {
                    Console.Write(s_UncompleteEmptyLine);     
                }
            }
            else
            {
                string price_str = null;
                if (!MBLOrderBookEntry.isASpecialValuePrice(entry.Price, ref price_str))
                {
                    price_str = entry.Price.Num.ToString("F4", s_CultureInfo);
                }
                string nb_orders_str = null;
                if (!MBLOrderBookEntry.isASpecialNbOrders(entry.Qty.NbOrders,ref nb_orders_str))
                {
                    nb_orders_str = entry.Qty.NbOrders.Num.ToString();
                }
                Console.Write("{0} {1,3} {2,9} x {3,6} @ {4,6} {5}",
                        prefix,
                        s_Legend[side],
                        price_str,
                        entry.Qty.CumulatedUnits.Num,
                        nb_orders_str,
                        s_CultureInfo);
            }
        }

        private static void Print(string prefix, MBLSnapshot snapshot)
        {
            if (null != snapshot)
            {
                foreach (MBLLayer layer in snapshot.Layers)
                {
                    Print(prefix, snapshot.InstrumentCode, layer);
                }
            }
        }

        private static void Print(string prefix, uint instrumentCode, MBLLayer layer)
        {
            int bidSize = (null != layer.BidLimits) ? layer.BidLimits.Count : 0;
            int askSize = (null != layer.AskLimits) ? layer.AskLimits.Count : 0;
            int maxVisibleDepth = (int)(layer.MaxVisibleDepth.Num);
            bool is_complete = false;
            // MaxVisibleDepth can be negative when dealing with a non-finite book 
            if (maxVisibleDepth < 0)
            {
                is_complete = true;
            }
            else
            {
                if (bidSize == maxVisibleDepth && askSize == maxVisibleDepth)
                {
                    is_complete = true;
                }
            }

            PrintInstrumentAndBidAskSizesAndTimestamps(prefix,instrumentCode,layer.LayerId, bidSize,askSize,layer.Timestamps);
            List<MBLOrderBookEntry> bidLimits = layer.BidLimits;
            List<MBLOrderBookEntry> askLimits = layer.AskLimits;
            MBLOrderBookEntry cur_bid_entry, cur_ask_entry;
            if (null != bidLimits || null != askLimits)
            {
            	if (maxVisibleDepth < 0)
                {
                    maxVisibleDepth = Math.Max (bidSize,askSize);
                }
                for (int cur_depth = 0; cur_depth < maxVisibleDepth; cur_depth++)
                {
                    if (cur_depth < bidSize)
                    {
                        cur_bid_entry = bidLimits[cur_depth];
                    }
                    else
                    {
                        cur_bid_entry = null;
                    }
                    PrintMBLOrderBookEntry(BidOrAskSide.BidSide, cur_depth, cur_bid_entry, is_complete);
                    if (cur_depth < askSize)
                    {
                        cur_ask_entry = askLimits[cur_depth];
                    }
                    else
                    {
                        cur_ask_entry = null;
                    }
                    PrintMBLOrderBookEntry(BidOrAskSide.AskSide, cur_depth, cur_ask_entry, is_complete);
                }
            }
            Console.WriteLine("");
            PrintOtherValues(layer.OtherValues);
        }

        private static void Print(string prefix, MBLDeltaRefresh delta)
        {
            if (null != delta)
            {
                Console.Write(prefix + " " + DumpMBLHeader(delta.InstrumentCode, delta.LayerId));

                string price_str = null;
                if (!MBLOrderBookEntry.isASpecialValuePrice(delta.Price, ref price_str))
                {
                    price_str = delta.Price.Num.ToString("F4", s_CultureInfo);
                }
                string nb_orders_str = null;
                if (!MBLOrderBookEntry.isASpecialNbOrders(delta.Qty.NbOrders, ref nb_orders_str))
                {
                    nb_orders_str = delta.Qty.NbOrders.Num.ToString();
                }
                // Here CONT means that the current MBLDelta is not the last MBLDeltaRefresh that has been generated in a row to reflect market changes in 
                // aggregated orderbook. (can be interesting when being concerned by book integrity)
                Console.WriteLine(" " + DumpTimestamps(delta.Timestamps) + "\n" + MBLDeltaRefresh.s_ActionNames[delta.Action] + " level=" + delta.Level.Num
                    + " price=" + price_str + " qty=" + delta.Qty.CumulatedUnits.Num + " nb_orders=" + nb_orders_str + (delta.ContinuationFlag ? " CONT ":" "));
                if (null != delta.OtherValues && delta.OtherValues.Count > 0)
                {
                    PrintOtherValues(delta.OtherValues);
                }
            }
        }

        private static void Print(string prefix, MBLOverlapRefresh overlap)
        {
            if (null != overlap)
            {
                Console.Write(prefix + " " + DumpMBLHeader(overlap.InstrumentCode, overlap.LayerId));
                int bidSize = (null != overlap.BidLimits) ? overlap.BidLimits.Count : 0;
                int askSize = (null != overlap.AskLimits) ? overlap.AskLimits.Count : 0;
                if (0 == bidSize && 0 == askSize) return ;

                List<MBLOrderBookEntry> bidLimits = overlap.BidLimits;
                List<MBLOrderBookEntry> askLimits = overlap.AskLimits;

                bool bidComplete = false;
                int bidStartLevel = 0;
                MBLOverlapRefresh.splitOrderBookChangeIndicator(overlap.BidChangeIndicator,ref bidComplete,ref bidStartLevel);
                string bidCompletionSign = bidComplete ? "*" : "+";
                bool askComplete = false;
                int askStartLevel = 0;
                MBLOverlapRefresh.splitOrderBookChangeIndicator(overlap.AskChangeIndicator,ref askComplete,ref askStartLevel);
                string askCompletionSign = askComplete ? "*" : "+";
                Console.WriteLine(" bid("+bidStartLevel+bidCompletionSign+bidSize+") ask("+askStartLevel+askCompletionSign+askSize+") "+ DumpTimestamps(overlap.Timestamps));
                
                int max_depth = Math.Max(bidStartLevel + bidSize, askStartLevel+ askSize) + 1 ;

                MBLOrderBookEntry curBidEntry, curAskEntry;
                for (int curDepth = 0; curDepth < max_depth; curDepth++)
                {
                    if (curDepth >= bidStartLevel && curDepth < bidStartLevel+bidSize)
                    {
                        curBidEntry = bidLimits[curDepth - bidStartLevel];
                    }
                    else
                    {
                        curBidEntry = null;
                    }
                    // Do not display star lines between top book and start level
                    if (curDepth < bidStartLevel)
                    {
                        bidComplete = false;
                    }
                    PrintMBLOrderBookEntry(BidOrAskSide.BidSide, curDepth, curBidEntry, bidComplete);
                    if (curDepth >= askStartLevel && curDepth < askStartLevel + askSize)
                    {
                        curAskEntry = askLimits[curDepth - askStartLevel];
                    }
                    else
                    {
                        curAskEntry = null;
                    }
                    // Do not display star lines between top book and start level
                    if (curDepth < askStartLevel)
                    {
                        askComplete = false;
                    }
                    PrintMBLOrderBookEntry(BidOrAskSide.AskSide, curDepth, curAskEntry, askComplete);
                }
                Console.WriteLine("");
                if (null != overlap.OtherValues && overlap.OtherValues.Count > 0)
                {
                    PrintOtherValues(overlap.OtherValues);
                }
            }
        }

        #endregion

        #region Instance data
        private Dictionary<uint, MBLSnapshot> m_SnapshotList = new Dictionary<uint, MBLSnapshot>();
        #endregion

        #region Construction

        public PrinterMBL()
        {
            m_DoMergeLayers = false;
            m_MergeTargetLayerId = OrderBookLayerId.ORDERBOOK_LAYER_CUSTOM;
            m_Verbose = false;
            m_OutputMode = MBLOutputMode.Events;
        }

        #endregion

        #region Properties

        public bool DoMergeLayers
        {
            get { return m_DoMergeLayers; }
            set { m_DoMergeLayers = value; }
        }

        public uint MergeTargetLayerId
        {
            get { return m_MergeTargetLayerId; }
            set { m_MergeTargetLayerId = value; }
        }

        public bool Verbose
        {
            get { return m_Verbose; }
            set { m_Verbose = value; }
        }

        public MBLOutputMode OutputMode
        {
            get { return m_OutputMode; }
            set { m_OutputMode = value; }
        }

        #endregion

        #region Internal Tools

        protected void UpdateAndPrintMergedLayer(uint instrumentCode)
        {
            MBLSnapshot snapshot = m_SnapshotList[instrumentCode];

            // Recreate merged layer
            snapshot.RemoveLayer(MergeTargetLayerId);
            snapshot.MergeAllLayers(    MergeTargetLayerId,
                                        MBLSnapshot.DONT_USE_LATEST_SERVER_TIMESTAMP,
                                        MBLSnapshot.DONT_USE_LATEST_MARKET_TIMESTAMP,
                                        MBLSnapshot.MERGE_OTHER_VALUES,
                                        MBLSnapshot.PRESERVE_MERGED_LAYERS);

            // Print newly merged layer
            MBLLayer merged_layer = snapshot.getOrCreateLayer(MergeTargetLayerId);
            Print("OB", instrumentCode, merged_layer);
        }

        private MBLSnapshot GetOrCreateSnapshot(uint instrumentCode)
        {
            MBLSnapshot snapshot = null;
            if (!m_SnapshotList.TryGetValue(instrumentCode, out snapshot))
            {
                snapshot = new MBLSnapshot(instrumentCode, new List<MBLLayer>());
                m_SnapshotList.Add(instrumentCode, snapshot);
            }

            return snapshot;
        }

        #endregion

        #region Realtime Notification Handlers

        public void MBLAddedInstrumentsHandler(uint requestId, List<uint> addedInstruments)
        {
            if (null != addedInstruments)
            {
                foreach (uint code in addedInstruments)
                {
                    Console.WriteLine("Subscribing MBL for instrument: " + code);
                }
            }
        }

        public void MBLFullRefreshHandler(uint requestId, List<MBLSnapshot> snapshots)
        {
            m_SnapshotList.Clear();
            if (null != snapshots)
            {
                foreach (MBLSnapshot snapshot in snapshots)
                {
                    m_SnapshotList.Add(snapshot.InstrumentCode, snapshot);
                    
                    // Show event
                    if (OutputMode != MBLOutputMode.Cache)
                    {
                        Console.WriteLine("snapshot");
                        Print("OF", snapshot);
                    }

                    // Show cache
                    if (OutputMode != MBLOutputMode.Events)
                    {
                        if ( DoMergeLayers )
                        {
                            UpdateAndPrintMergedLayer(snapshot.InstrumentCode);
                        }
                        else
                        {
							// nb: layers in each snapshot should be checked for tag HasContinuationFlag.
							// When set, this flag indicates the corresponding snapshot occurred while
							// a multi-part snapshot was processed. A series of DeltaRefresh notifications is
							// expected to follow, and the first DeltaRefresh with ContinuationFlag==false
							// will indicate all chunks were received.
							// In the meantime, the received book is only partially correct, and possibly crossed.
                            Print("OB", snapshot);
                        }
                    }
                }
            }
        }

        public void MBLOverlapRefreshHandler(uint requestId, MBLOverlapRefresh overlap)
        {
            if (null != overlap)
            {
                MBLSnapshot snapshot = GetOrCreateSnapshot(overlap.InstrumentCode);
                snapshot.Update(overlap);

                // Show event
                if (OutputMode != MBLOutputMode.Cache)
                {
                    Console.WriteLine("overlap");
                    Print("OR", overlap);
                }

                // Show cache
                if (OutputMode != MBLOutputMode.Events)
                {
                    if ( DoMergeLayers )
                    {
                        UpdateAndPrintMergedLayer(snapshot.InstrumentCode);
                    }
                    else
                    {
                        Print("OB", snapshot);
                    }
                }
            }
        }

        public void MBLDeltaRefreshHandler(uint requestId, MBLDeltaRefresh delta)
        {
            if (null != delta)
            {
                MBLSnapshot snapshot = GetOrCreateSnapshot(delta.InstrumentCode);
                snapshot.Update(delta);

                // Show event
                if (OutputMode != MBLOutputMode.Cache)
                {
                    Console.WriteLine("delta");
                    Print("OD", delta);
                }

                // Show cache
                if (OutputMode != MBLOutputMode.Events)
                {
                    if ( DoMergeLayers )
                    {
                        UpdateAndPrintMergedLayer(snapshot.InstrumentCode);
                    }
                    else
                    {
						// nb: delta.ContinuationFlag==true indicates the DeltaRefresh is incomplete. A series
						// of other DeltaRefresh notifications is expected to follow and finalize the image. The
						// first DeltaRefresh with ContinuationFlag==false indicates all chuncks were received ; 
						// in the meantime, the book is only partially correct and may possibly be crossed.
	                    Print("OB", snapshot);
                    }
                }
            }
        }

        public void MBLMaxVisibleDepthHandler(uint requestId, MBLMaxVisibleDepth maxVisibleDepth)
        {
            if (null != maxVisibleDepth)
            {
                MBLSnapshot snapshot = GetOrCreateSnapshot(maxVisibleDepth.InstrumentCode);
                snapshot.Update(maxVisibleDepth);

                // Show event
                if (OutputMode != MBLOutputMode.Cache)
                {
                    Console.WriteLine("MBLMaxVisibleDepth event: " + DumpInstrument(maxVisibleDepth.InstrumentCode) +
                                      ", Layer=" + maxVisibleDepth.LayerId +
                                      ", maxVisibleDepth=" + maxVisibleDepth.MaxVisibleDepth.Num);
                }

                // Show cache
                if (OutputMode != MBLOutputMode.Events)
                {
                    if ( DoMergeLayers )
                    {
                        UpdateAndPrintMergedLayer(snapshot.InstrumentCode);
                    }
                    else
                    {
                        Print("OB", snapshot);
                    }
                }
            }
        }

        #endregion
    }
}
