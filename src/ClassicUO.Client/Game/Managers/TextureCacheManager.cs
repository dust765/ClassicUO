using ClassicUO.Configuration;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace ClassicUO.Game.Managers
{
    public static class TextureCacheManager
    {
        private static readonly Dictionary<string, CachedTexture> _textureCache = new Dictionary<string, CachedTexture>();
        private static readonly List<KeyValuePair<string, CachedTexture>> _cleanupScratch = new List<KeyValuePair<string, CachedTexture>>(1024);
        private static readonly object _cacheLock = new object();
        private static int _maxCacheSize = 1000;
        private static long _lastCleanupTime = 0;
        private static readonly long _cleanupInterval = 30000; // 30 seconds
        
        private class CachedTexture
        {
            public Texture2D Texture { get; set; }
            public DateTime LastAccessed { get; set; }
            public int AccessCount { get; set; }
            public long Size { get; set; }
        }
        
        public static void SetMaxCacheSize(int size)
        {
            _maxCacheSize = size;
        }
        
        public static Texture2D GetOrCreateTexture(string key, Func<Texture2D> textureFactory)
        {
            if (!ProfileManager.CurrentProfile?.EnableTextureCaching ?? true)
            {
                return textureFactory();
            }
            
            lock (_cacheLock)
            {
                CleanupIfNeeded();
                
                if (_textureCache.TryGetValue(key, out var cached))
                {
                    cached.LastAccessed = DateTime.Now;
                    cached.AccessCount++;
                    return cached.Texture;
                }
                
                var texture = textureFactory();
                if (texture != null)
                {
                    var size = EstimateTextureSize(texture);
                    _textureCache[key] = new CachedTexture
                    {
                        Texture = texture,
                        LastAccessed = DateTime.Now,
                        AccessCount = 1,
                        Size = size
                    };
                }
                
                return texture;
            }
        }
        
        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                foreach (KeyValuePair<string, CachedTexture> kv in _textureCache)
                {
                    kv.Value.Texture?.Dispose();
                }
                _textureCache.Clear();
            }
        }
        
        public static void RemoveTexture(string key)
        {
            lock (_cacheLock)
            {
                if (_textureCache.TryGetValue(key, out var cached))
                {
                    cached.Texture?.Dispose();
                    _textureCache.Remove(key);
                }
            }
        }
        
        public static int GetCacheSize()
        {
            lock (_cacheLock)
            {
                return _textureCache.Count;
            }
        }
        
        public static long GetCacheMemoryUsage()
        {
            lock (_cacheLock)
            {
                long sum = 0;
                foreach (KeyValuePair<string, CachedTexture> kv in _textureCache)
                {
                    sum += kv.Value.Size;
                }

                return sum;
            }
        }
        
        private static void CleanupIfNeeded()
        {
            var now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            if (now - _lastCleanupTime < _cleanupInterval)
                return;
                
            _lastCleanupTime = now;
            
            if (_textureCache.Count <= _maxCacheSize)
                return;

            int removeCount = _textureCache.Count - _maxCacheSize;
            _cleanupScratch.Clear();
            foreach (KeyValuePair<string, CachedTexture> kv in _textureCache)
            {
                _cleanupScratch.Add(kv);
            }

            _cleanupScratch.Sort(CompareCachedTextureEviction);

            for (int i = 0; i < removeCount && i < _cleanupScratch.Count; i++)
            {
                KeyValuePair<string, CachedTexture> kvp = _cleanupScratch[i];
                kvp.Value.Texture?.Dispose();
                _textureCache.Remove(kvp.Key);
            }

            _cleanupScratch.Clear();
        }

        private static int CompareCachedTextureEviction(KeyValuePair<string, CachedTexture> a, KeyValuePair<string, CachedTexture> b)
        {
            int c = a.Value.LastAccessed.CompareTo(b.Value.LastAccessed);
            return c != 0 ? c : a.Value.AccessCount.CompareTo(b.Value.AccessCount);
        }
        
        private static long EstimateTextureSize(Texture2D texture)
        {
            if (texture == null) return 0;
            
            // Rough estimation: width * height * 4 bytes per pixel
            return (long)texture.Width * texture.Height * 4;
        }
    }
}
