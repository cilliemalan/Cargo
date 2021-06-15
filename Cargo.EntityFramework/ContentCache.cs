using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cargo
{
    /// <summary>
    /// Used to cache content inside cargo
    /// </summary>
    public sealed class ContentCache
    {
        private static volatile ContentCache instance;
        private static object syncRoot = new Object();

        /// <summary>
        /// Items cached
        /// </summary>
        public List<ContentItem> ContentItems { get { return contentItems;  } }
        private List<ContentItem> contentItems;

        private ContentCache() { }


        /// <summary>
        /// Since we only want one cache per application. Singleton pattern implementation chosen.
        /// </summary>
        public static ContentCache Instance
        {
            get
            { 
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                        {
                            instance = new ContentCache();
                        }
                    }
                }

                return instance;
            }
        }


        /// <summary>
        /// Init the content items for the cache
        /// </summary>
        public static void InitContentItems(List<ContentItem> items)
        {
            lock (syncRoot)
            {
                if (instance != null)
                { 
                    instance.contentItems = items;
                }
            }
        }

        /// <summary>
        /// Remove an item from the cache
        /// </summary>
        /// <param name="item">Item to remove</param>
        public void RemoveItem(ContentItem item)
        {
            lock (syncRoot)
            {
                instance.contentItems.Remove(item);
            }
        }

        /// <summary>
        /// Add an item from the cache
        /// </summary>
        /// <param name="item">Item to add</param>
        public void AddItem(ContentItem item)
        {
            lock (syncRoot)
            {
                //Be safe, we do not know where other threads are in this method
                if (!instance.contentItems.Contains(item))
                {
                    instance.contentItems.Add(item);
                }
            }
        }


        /// <summary>
        /// Find a item and update it
        /// </summary>
        /// <param name="item">Item to update</param>
        public void UpdateItem(ContentItem item)
        {
            lock (syncRoot)
            {
                var cachedItem = instance.contentItems
                                         .Find( x => x.Key.Equals(item.Key) && x.Location.Equals(item.Location));

                if (cachedItem != null)
                {
                    cachedItem.Content = item.Content;
                    cachedItem.OriginalContent = item.OriginalContent;
                }
            }
        }

        /// <summary>
        /// Find a item and update its content
        /// </summary>
        /// <param name="item">Item to update</param>
        public void UpdateItemContent(ContentItem item)
        {
            lock (syncRoot)
            {
                var cachedItem = instance.contentItems
                                         .Find(x => x.Key.Equals(item.Key) && x.Location.Equals(item.Location));

                if (cachedItem != null)
                {
                    cachedItem.Content = item.Content;
                }
            }
        }

    }
}
