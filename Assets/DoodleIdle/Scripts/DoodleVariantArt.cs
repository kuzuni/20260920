using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public static class DoodleVariantArt
    {
        public static readonly string[] Skills = { "Eggplant", "Durian", "Brick", "BeachBall", "Shuriken", "IceSnakeHead", "IceSnakeSegment", "PurpleFireArrow", "BlueMolotov", "RedCloud", "RedCloudB", "RedLightning" };
        public static readonly string[] Companions = { "SporeFairy", "FrostFox", "BrickGolem", "ShurikenTanuki", "BeeKnight" };
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        // Pixel regions in the generated 1254px atlas (top-left origin), including its uneven gutters.
        static readonly Rect[] regions = {
            new Rect(20,190,295,210), new Rect(340,140,275,300), new Rect(630,195,300,220), new Rect(965,165,275,265),
            new Rect(40,525,275,265), new Rect(355,550,260,220), new Rect(640,570,248,220), new Rect(890,565,352,205),
            new Rect(45,815,280,365), new Rect(315,915,300,235), new Rect(630,915,310,235), new Rect(955,950,285,175)
        };
        public static Sprite Get(string key)
        {
            if (cache.TryGetValue(key, out var sprite)) return sprite;
            int index=Array.IndexOf(Skills,key), companion=Array.IndexOf(Companions,key);
            if(index<0 && companion<0)return null;
            var texture=Resources.Load<Texture2D>("DoodleIdle/"+(index>=0?"SkillVariants":"CompanionVariants"));
            Rect region;
            if(index>=0) { region=regions[index]; region.y=texture.height-region.y-region.height; }
            else { float w=texture.width/3f,h=texture.height/2f; region=new Rect(companion%3*w,(1-companion/3)*h,w,h); }
            var pixels=texture.GetPixels32(); int x0=texture.width,y0=texture.height,x1=-1,y1=-1;
            for(int y=(int)region.yMin;y<Mathf.Min(texture.height,region.yMax);y++)
                for(int x=(int)region.xMin;x<Mathf.Min(texture.width,region.xMax);x++)
                    if(pixels[y*texture.width+x].a>32){x0=Mathf.Min(x0,x);y0=Mathf.Min(y0,y);x1=Mathf.Max(x1,x);y1=Mathf.Max(y1,y);}
            if(x1<x0)throw new InvalidOperationException("Empty variant art: "+key);
            var rect=new Rect(x0,y0,x1-x0+1,y1-y0+1);
            sprite=Sprite.Create(texture,rect,Vector2.one*.5f,Mathf.Max(rect.width,rect.height));sprite.name=key;
            cache[key]=sprite;return sprite;
        }
    }
}
