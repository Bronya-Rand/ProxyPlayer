using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;

namespace ProxyPlayer.Media
{
    public sealed class TextureCache : IDisposable
    {
        private sealed class Entry
        {
            public byte[]? LastBytes;
            public IDalamudTextureWrap? Texture;
            public int Generation;
        }

        private readonly Dictionary<string, Entry> entries = [];

        public void UpdateIfChanged(string key, byte[]? bytes)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                entry = new Entry();
                entries[key] = entry;
            }

            if (entry.LastBytes != null && bytes != null &&
                entry.LastBytes.AsSpan().SequenceEqual(bytes.AsSpan()))
                return;

            entry.LastBytes = bytes;
            entry.Generation++; // Invalidate any load for this key

            entry.Texture?.Dispose();
            entry.Texture = null;

            if (bytes is not { Length: > 0 })
                return;

            // Fire and forget the texture load, we don't need to await it here.
            _ = LoadTextureAsync(key, entry, bytes, entry.Generation);
        }
        public IDalamudTextureWrap? GetTexture(string key)
        {
            if (entries.TryGetValue(key, out var entry))
                return entry.Texture;
            return null;
        }
        private static async Task LoadTextureAsync(string key, Entry entry, byte[] bytes, int generation)
        {
            try
            {
                var wrap = await Plugin.TextureProvider.CreateFromImageAsync(bytes);

                // Check if the entry has changed since we started loading
                // If so, drop the loaded texture and return early
                if (entry.Generation != generation)
                {
                    wrap.Dispose();
                    return;
                }

                entry.Texture?.Dispose();
                entry.Texture = wrap;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to load texture for blob key {key}", key);
            }
        }
        public void Evict(string key)
        {
            if (entries.Remove(key, out var entry))
            {
                entry.Generation++;
                entry.Texture?.Dispose();
            }
        }
        public void Dispose()
        {
            foreach (var entry in entries.Values)
                entry.Texture?.Dispose();
            entries.Clear();
        }
    }
}
