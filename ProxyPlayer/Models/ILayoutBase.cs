using System;
using System.Numerics;
using ProxyPlayer.Media;
using ProxyPlayer.Shared;

namespace ProxyPlayer.Models
{
    public interface ILayoutBase
    {
        Vector2 CoverArtDimensions { get; }
        Action? OnRequestSessionListOpen { get; set; }
        void Draw(MediaState state, PipeClient pipeClient, TextureCache texture);
    }
}
