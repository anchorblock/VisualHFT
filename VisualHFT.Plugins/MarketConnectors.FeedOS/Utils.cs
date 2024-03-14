using FeedOSAPI.Types;
using VisualHFT.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketConnectors.FeedOS
{
    public class FeedosOrderBookMapper
    {
        public VisualHFT.Model.OrderBook MapOrderBook(FeedOSAPI.Types.OrderBook feedosOrderBook, uint instrumentCode)
        {
            VisualHFT.Model.OrderBook visualHFTOrderBook = new VisualHFT.Model.OrderBook();

            // Map symbol, decimal places, provider ID, provider name, etc.
            visualHFTOrderBook.Symbol = GetNormalizedSymbol(instrumentCode);
            visualHFTOrderBook.DecimalPlaces = 2; // Adjust based on the instrument
            visualHFTOrderBook.ProviderID = 1; // Adjust based on your provider settings
            visualHFTOrderBook.ProviderName = "FeedOS";

            // Map bids and asks
            List<VisualHFT.Model.BookItem> bids = MapBookItems(feedosOrderBook.BidLimits, true);
            List<VisualHFT.Model.BookItem> asks = MapBookItems(feedosOrderBook.AskLimits, false);

            visualHFTOrderBook.LoadData(asks, bids);

            return visualHFTOrderBook;
        }


        private string GetNormalizedSymbol(uint instrumentCode)
        {
            return instrumentCode.ToString();
        }
        public VisualHFT.Model.OrderBook UpdateOrderBook(VisualHFT.Model.OrderBook visualHFTOrderBook, OrderBookRefresh orderBookRefresh)
        {
            // Update the bids
            if (orderBookRefresh.BidLimits != null)
            {
                foreach (var entry in orderBookRefresh.BidLimits)
                {
                    UpdateBookItem(visualHFTOrderBook.Bids.ToList(), entry, true);
                }
            }

            // Update the asks
            if (orderBookRefresh.AskLimits != null)
            {
                foreach (var entry in orderBookRefresh.AskLimits)
                {
                    UpdateBookItem(visualHFTOrderBook.Asks.ToList(), entry, false);
                }
            }

            return visualHFTOrderBook;
        }
        private List<VisualHFT.Model.BookItem> MapBookItems(List<FeedOSAPI.Types.OrderBookEntryExt> entries, bool isBid)
        {
            return entries.Select(entry => new VisualHFT.Model.BookItem
            {
                Price = (double)entry.Price,
                Size = (double)entry.Size,
                LocalTimeStamp = DateTime.Now,
                ServerTimeStamp = DateTime.Now, // Adjust based on the timestamp provided by FeedOS
                Symbol = "", // Set the symbol if available
                DecimalPlaces = 2, // Adjust based on the instrument
                IsBid = isBid,
                ProviderID = 1 // Adjust based on your provider settings
            }).ToList();
        }

        public VisualHFT.Model.OrderBook UpdateOrderBook(VisualHFT.Model.OrderBook visualHFTOrderBook, OrderBookDeltaRefresh orderBookDeltaRefresh)
        {
            switch (orderBookDeltaRefresh.Action)
            {
                case OrderBookDeltaAction.OrderBookDeltaAction_ALLClearFromLevel:
                    ClearFromLevel(visualHFTOrderBook, orderBookDeltaRefresh.Level);
                    break;
                case OrderBookDeltaAction.OrderBookDeltaAction_BidClearFromLevel:
                    ClearFromLevel(visualHFTOrderBook, orderBookDeltaRefresh.Level, true);
                    break;
                case OrderBookDeltaAction.OrderBookDeltaAction_AskClearFromLevel:
                    ClearFromLevel(visualHFTOrderBook, orderBookDeltaRefresh.Level, false);
                    break;
                case OrderBookDeltaAction.OrderBookDeltaAction_BidInsertAtLevel:
                    InsertAtLevel(visualHFTOrderBook, orderBookDeltaRefresh.Level, orderBookDeltaRefresh.Price, orderBookDeltaRefresh.Qty, true);
                    break;
                case OrderBookDeltaAction.OrderBookDeltaAction_AskInsertAtLevel:
                    InsertAtLevel(visualHFTOrderBook, orderBookDeltaRefresh.Level, orderBookDeltaRefresh.Price, orderBookDeltaRefresh.Qty, false);
                    break;
                case OrderBookDeltaAction.OrderBookDeltaAction_BidRemoveLevel:
                    RemoveLevel(visualHFTOrderBook, orderBookDeltaRefresh.Level, true);
                    break;
                case OrderBookDeltaAction.OrderBookDeltaAction_AskRemoveLevel:
                    RemoveLevel(visualHFTOrderBook, orderBookDeltaRefresh.Level, false);
                    break;
                case OrderBookDeltaAction.OrderBookDeltaAction_BidChangeQtyAtLevel:
                    ChangeQtyAtLevel(visualHFTOrderBook, orderBookDeltaRefresh.Level, orderBookDeltaRefresh.Qty, true);
                    break;
                case OrderBookDeltaAction.OrderBookDeltaAction_AskChangeQtyAtLevel:
                    ChangeQtyAtLevel(visualHFTOrderBook, orderBookDeltaRefresh.Level, orderBookDeltaRefresh.Qty, false);
                    break;
                case OrderBookDeltaAction.OrderBookDeltaAction_BidRemoveLevelAndAppend:
                    RemoveLevelAndAppend(visualHFTOrderBook, orderBookDeltaRefresh.Level, true);
                    break;
                case OrderBookDeltaAction.OrderBookDeltaAction_AskRemoveLevelAndAppend:
                    RemoveLevelAndAppend(visualHFTOrderBook, orderBookDeltaRefresh.Level, false);
                    break;
            }

            return visualHFTOrderBook;
        }
        private void ClearFromLevel(VisualHFT.Model.OrderBook orderBook, byte level, bool isBid = true)
        {
            var bookItems = isBid ? orderBook.Bids.ToList() : orderBook.Asks.ToList();
            if (level < bookItems.Count)
            {
                bookItems.RemoveRange(level, bookItems.Count - level);
                if (isBid)
                    orderBook.LoadData(orderBook.Asks, bookItems);
                else
                    orderBook.LoadData(bookItems, orderBook.Bids);
            }
        }
        private void InsertAtLevel(VisualHFT.Model.OrderBook orderBook, byte level, double price, double qty, bool isBid)
        {
            var newItem = new BookItem
            {
                Price = price,
                Size = qty,
                LocalTimeStamp = DateTime.Now,
                ServerTimeStamp = DateTime.Now,
                IsBid = isBid,
                ProviderID = orderBook.ProviderID,
                Symbol = orderBook.Symbol,
                DecimalPlaces = orderBook.DecimalPlaces
            };

            var bookItems = isBid ? orderBook.Bids.ToList() : orderBook.Asks.ToList();
            if (level < bookItems.Count)
                bookItems.Insert(level, newItem);
            else
                bookItems.Add(newItem);

            if (isBid)
                orderBook.LoadData(orderBook.Asks, bookItems);
            else
                orderBook.LoadData(bookItems, orderBook.Bids);
        }

        private void RemoveLevel(VisualHFT.Model.OrderBook orderBook, byte level, bool isBid)
        {
            var bookItems = isBid ? orderBook.Bids.ToList() : orderBook.Asks.ToList();
            if (level < bookItems.Count)
            {
                bookItems.RemoveAt(level);
                if (isBid)
                    orderBook.LoadData(orderBook.Asks, bookItems);
                else
                    orderBook.LoadData(bookItems, orderBook.Bids);
            }
        }

        private void ChangeQtyAtLevel(VisualHFT.Model.OrderBook orderBook, byte level, double qty, bool isBid)
        {
            var bookItems = isBid ? orderBook.Bids.ToList() : orderBook.Asks.ToList();
            if (level < bookItems.Count)
            {
                bookItems[level].Size = qty;
                if (isBid)
                    orderBook.LoadData(orderBook.Asks, bookItems);
                else
                    orderBook.LoadData(bookItems, orderBook.Bids);
            }
        }


        private void RemoveLevelAndAppend(VisualHFT.Model.OrderBook orderBook, byte level, bool isBid)
        {
            var bookItems = isBid ? orderBook.Bids.ToList() : orderBook.Asks.ToList();
            if (level < bookItems.Count)
            {
                bookItems.RemoveAt(level);
                // Append "worst visible" limit if available
                // You may need to implement the logic to get the "worst visible" limit from Feedos
                if (isBid)
                    orderBook.LoadData(orderBook.Asks, bookItems);
                else
                    orderBook.LoadData(bookItems, orderBook.Bids);
            }
        }
        private void UpdateBookItem(List<VisualHFT.Model.BookItem> bookItems, OrderBookEntryExt entry, bool isBid)
        {
            var existingItem = bookItems.FirstOrDefault(item => item.Price == (double)entry.Price);
            if (existingItem != null)
            {
                existingItem.Size = (double)entry.Size;
            }
            else
            {
                bookItems.Add(new VisualHFT.Model.BookItem
                {
                    Price = (double)entry.Price,
                    Size = (double)entry.Size,
                    LocalTimeStamp = DateTime.Now,
                    ServerTimeStamp = DateTime.Now,
                    Symbol = "",
                    DecimalPlaces = 2,
                    IsBid = isBid,
                    ProviderID = 1
                });
            }
        }
        private void DeleteBookItem(List<VisualHFT.Model.BookItem> bookItems, uint level)
        {
            var itemToRemove = bookItems.FirstOrDefault(item => item.Price == (double)level);
            if (itemToRemove != null)
            {
                bookItems.Remove(itemToRemove);
            }
        }


    }
}