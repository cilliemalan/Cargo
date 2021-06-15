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
    /// Allows using an Entity Framework data context as a data source for Cargo. The
    /// relevant <see cref="DbContext"/> instance needs to have the needed entities mapped
    /// by calling <see cref="CargoEntityFrameworkExtensions.MapCargoContent(DbModelBuilder, string, string)"/>.
    /// </summary>
    public class EntityFrameworkCargoDataSource : CargoDataSourceBase
    {
        private string _rxId = @"^(.*)\/(.+)";
        private DbContext _dataContext;
        private bool _ownsContext;
        private ContentCache _cache;

        private DbSet<ContentItem> ContentItems
        {
            get
            {
                return _dataContext.Set<ContentItem>();
            }
        }

        /// <summary>
        /// Cached DB items. Used not to overwhelm the DB. Stored as a list inside a singleton
        /// </summary>
        private List<ContentItem> ContentItemsCached
        {
            get
            {
                //If the cache is empty, populate it from the DB
                if (_cache.ContentItems == null)
                {
                    ContentCache.InitContentItems(_dataContext.Set<ContentItem>().Select(AddIds).ToList());
                }

                return _cache.ContentItems;
            }
        }

        /// <summary>
        /// Create a new <see cref="EntityFrameworkCargoDataSource"/> given an instance of <see cref="DbContext"/>. 
        /// </summary>
        /// <param name="dataContext">
        /// The <see cref="DbContext"/> to use. The <see cref="DbContext"/>
        /// must have been prepared by calling <see cref="CargoEntityFrameworkExtensions.MapCargoContent(DbModelBuilder, string, string)"/>.
        /// </param>
        /// <param name="ownsContext">If set to <c>true</c> the context will be disposed when this instance is disposed.</param>
        public EntityFrameworkCargoDataSource(DbContext dataContext, bool ownsContext = true)
        {
            _dataContext = dataContext;
            _ownsContext = ownsContext;

            //Get the cache
            _cache = ContentCache.Instance;
        }

        /// <inheritdoc />
        public override ContentItem Get(string location, string key)
        {
            ValidateLocation(location);
            ValidateKey(key);

            return AddIds(ContentItemsCached.Find(x => x.Location.Equals(location) &&
                                                       x.Key.Equals(key)));
        }

        /// <inheritdoc />
        public override ICollection<ContentItem> GetAllContent()
        {
            return ContentItemsCached;
        }

        /// <inheritdoc />
        public override ICollection<ContentItem> GetAllContentForLocation(string location)
        {
            ValidateLocation(location);

            return ContentItemsCached
                .Where(x => x.Location == location && x.OriginalContent != null)
                .ToList();
        }

        /// <inheritdoc />
        public override ICollection<string> GetAllLocations()
        {
            return ContentItemsCached
                .Select(x => x.Location)
                .Distinct()
                .ToList();
        }

        /// <inheritdoc />
        public override ContentItem GetById(string id)
        {
            ValidateId(id);

            string location, key;
            ParseId(id, out location, out key);
            ValidateLocation(location);
            ValidateKey(key);

            return AddIds(ContentItemsCached.Find(x => x.Location.Equals(location) &&
                                                       x.Key.Equals(key)));
        }

        /// <inheritdoc />
        public override void Remove(IEnumerable<string> contentItemIds)
        {
            foreach(var item in GetMultipleById(contentItemIds))
            {
                if(item != null)
                {
                    ContentItems.Remove(item);
                    _cache.RemoveItem(item);
                }
            }

            _dataContext.SaveChanges();
        }

        /// <inheritdoc />
        public override ContentItem GetOrCreate(string location, string key, string defaultContent)
        {
            ValidateKey(key);
            ValidateLocation(location);
            defaultContent = defaultContent ?? "";

            //Search the cache in memory to reduce DB calls
            var cachedContentItem = ContentItemsCached.Find(x => x.Location.Equals(location) &&
                                                           x.Key.Equals(key));

            if (cachedContentItem == null)
            {
                var newContentItem = ContentItems.Add(new ContentItem
                {
                    Content = defaultContent,
                    Key = key,
                    Location = location,
                    Id = GetId(location, key),
                    OriginalContent = defaultContent
                });

                _dataContext.SaveChanges();

                _cache.AddItem(newContentItem); //Update the cache as well
            }

            if(cachedContentItem.OriginalContent != defaultContent)
            {
                var contentItem = ContentItems.Find(location, key);

                contentItem.OriginalContent = defaultContent;
                _dataContext.SaveChanges();

                _cache.UpdateItemContent(contentItem); //Update the cache as well
            }

            return cachedContentItem;
        }

        /// <inheritdoc />
        public override void Set(IEnumerable<ContentItem> contentItems)
        {
            bool anyset = false;
            foreach(var item in contentItems)
            {
                ValidateKey(item.Key);
                ValidateLocation(item.Location);

                var contentItem = ContentItems.Find(item.Location, item.Key);

                if (contentItem == null)
                {
                    contentItem = ContentItems.Add(new ContentItem
                    {
                        Content = item.Content,
                        Key = item.Key,
                        Location = item.Location,
                        Id = GetId(item.Location, item.Key),
                        OriginalContent = item.OriginalContent
                    });

                    anyset = true;
                    _cache.AddItem(contentItem); //Add item to current cache
                }
                else
                {
                    if (contentItem.Content != item.Content)
                    {
                        contentItem.Content = item.Content;
                        contentItem.OriginalContent = item.OriginalContent;

                        anyset = true;
                        _cache.UpdateItem(contentItem); //Update item in current cache
                    }
                }
            }

            if(anyset)
            {
                _dataContext.SaveChanges();
            }
        }

        /// <inheritdoc />
        public override void SetById(IEnumerable<KeyValuePair<string, string>> idContentPairs)
        {
            foreach (var item in idContentPairs)
            {
                ValidateId(item.Key);

                string id = item.Key;
                string location, key;
                ParseId(id, out location, out key);
                ValidateLocation(location);
                ValidateKey(key);

                var contentItem = AddIds(ContentItems.Find(location, key));

                if (contentItem != null)
                {
                    contentItem.Content = item.Value;

                    _cache.UpdateItemContent(contentItem); //Update the cached value
                }
            }

            _dataContext.SaveChanges();
        }

        private static ContentItem AddIds(ContentItem ci)
        {
            if (ci != null) ci.Id = GetId(ci.Location, ci.Key);

            return ci;
        }

        private IEnumerable<ContentItem> GetMultipleById(IEnumerable<string> contentItemIds)
        {
            foreach (var id in contentItemIds)
            {
                ValidateId(id);

                string location, key;
                ParseId(id, out location, out key);
                ValidateLocation(location);
                ValidateKey(key);

                var contentItem = ContentItemsCached.Find(x => x.Location.Equals(location) &&
                                                               x.Key.Equals(key));

                if (contentItem != null) yield return contentItem;
            }
        }

        private static string GetId(string location, string key)
        {
            return $"{location}/{key}";
        }

        private void ParseId(string id, out string location, out string key)
        {
            ValidateId(id);

            //note the greedy match of the first group
            var m = Regex.Match(id, _rxId);
            location = m.Groups[1].Value;
            if (location == "") location = null;
            key = m.Groups[2].Value;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                if(_dataContext != null && _ownsContext)
                {
                    _dataContext.Dispose();
                    _dataContext = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
