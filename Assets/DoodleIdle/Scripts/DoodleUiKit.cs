using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    /// <summary>Shared, real uGUI widgets. No screen reference is a runtime texture.</summary>
    public static class UiKit
    {
        public static readonly Color Ink = new Color(.10f,.095f,.08f);
        public static readonly Color Paper = new Color(1f,.995f,.965f);
        public static readonly Color Blue = new Color(.64f,.84f,.97f);
        public static readonly Color Green = new Color(.71f,.89f,.54f);
        public static readonly Color Yellow = new Color(1f,.9f,.46f);
        public static readonly Color Red = new Color(1f,.32f,.31f);
        public static Font Font;
        static readonly Dictionary<string, Sprite> art = new Dictionary<string, Sprite>();
        static Sprite frame, circle;
        static readonly string[] gradeNames = { "일반", "고급", "희귀", "영웅", "전설" };
        public static Color Rarity(int grade) => new[] { new Color(.96f,.92f,.80f), new Color(.76f,.96f,.66f), new Color(.68f,.85f,1), new Color(.86f,.72f,.98f), new Color(1,.89f,.48f) }[Mathf.Clamp(grade,0,4)];
        public static string GradeName(int grade) => gradeNames[Mathf.Clamp(grade,0,4)];
        public static RectTransform Rect(Transform parent,string name)
        {
            var r = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false); return r;
        }
        public static void Stretch(RectTransform rect,float left=0,float bottom=0,float right=0,float top=0)
        { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=new Vector2(left,bottom); rect.offsetMax=new Vector2(-right,-top); }
        public static void Height(Transform child,float height)
        { var l=child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>(); l.minHeight=height; l.preferredHeight=height; l.flexibleHeight=0; }
        public static void Flexible(Transform child,float weight=1)
        { var l=child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>(); l.minWidth=0; l.preferredWidth=0; l.flexibleWidth=weight; }
        public static RectTransform Box(Transform parent,string name,Color color,float height=0)
        {
            var r=Rect(parent,name); var im=r.gameObject.AddComponent<Image>(); im.sprite=Frame; im.type=Image.Type.Sliced; im.color=color;
            var outline=r.gameObject.AddComponent<Outline>(); outline.effectColor=Ink; outline.effectDistance=new Vector2(2,-2); outline.useGraphicAlpha=true;
            Flexible(r); if(height>0) Height(r,height); return r;
        }
        public static RectTransform Column(Transform parent,string name,float spacing=8,float padding=8)
        {
            var r=Rect(parent,name); var g=r.gameObject.AddComponent<VerticalLayoutGroup>(); g.spacing=spacing; g.padding=new RectOffset((int)padding,(int)padding,(int)padding,(int)padding);
            g.childControlHeight=true; g.childControlWidth=true; g.childForceExpandWidth=true; g.childForceExpandHeight=false;
            r.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize; Flexible(r); return r;
        }
        public static RectTransform Row(Transform parent,string name,float height=52,float spacing=8)
        {
            var r=Rect(parent,name); var g=r.gameObject.AddComponent<HorizontalLayoutGroup>(); g.spacing=spacing; g.childAlignment=TextAnchor.MiddleCenter;
            g.childControlWidth=g.childControlHeight=true; g.childForceExpandWidth=false; g.childForceExpandHeight=false; Height(r,height); Flexible(r); return r;
        }
        public static Text Text(Transform parent,string text,int size=24,TextAnchor align=TextAnchor.MiddleLeft,float height=36)
        {
            var r=Rect(parent,"Text: "+text); var t=r.gameObject.AddComponent<Text>(); t.font=Font; t.text=text; t.fontSize=size; t.color=Ink; t.alignment=align;
            t.raycastTarget=false; t.supportRichText=true; t.resizeTextForBestFit=true; t.resizeTextMinSize=Mathf.Min(16,size); t.resizeTextMaxSize=size;
            t.horizontalOverflow=HorizontalWrapMode.Wrap; t.verticalOverflow=VerticalWrapMode.Truncate; Height(r,height); Flexible(r); return t;
        }
        public static Button Button(Transform parent,string text,Action click,Color? color=null,float height=52)
        {
            var r=Box(parent,text,color ?? Blue,height); var b=r.gameObject.AddComponent<Button>(); b.targetGraphic=r.GetComponent<Image>();
            var c=b.colors; c.highlightedColor=new Color(1,.98f,.89f); c.pressedColor=new Color(.78f,.78f,.78f); c.disabledColor=new Color(.64f,.64f,.64f); b.colors=c;
            b.onClick.AddListener(()=>click?.Invoke()); var label=Text(r,text,23,TextAnchor.MiddleCenter,height-8); Stretch(label.rectTransform,5,4,5,4); return b;
        }
        public static Image Icon(Transform parent,string resource,float size=64)
        {
            var r=Rect(parent,"Icon: "+resource); var im=r.gameObject.AddComponent<Image>(); im.sprite=Art(resource); im.preserveAspect=true; im.raycastTarget=false;
            var l=r.gameObject.AddComponent<LayoutElement>(); l.minWidth=l.preferredWidth=size; l.minHeight=l.preferredHeight=size; l.flexibleWidth=0; l.flexibleHeight=0; r.sizeDelta=Vector2.one*size; return im;
        }
        public static RectTransform Gauge(Transform parent,string text,float fraction,float height=24)
        {
            var r=Box(parent,"Quantity gauge",new Color(.76f,.76f,.73f),height);
            var fill=Rect(r,"Fill"); var image=fill.gameObject.AddComponent<Image>(); image.sprite=Frame; image.type=Image.Type.Sliced; image.color=Green; image.raycastTarget=false;
            fill.anchorMin=Vector2.zero; fill.anchorMax=new Vector2(Mathf.Clamp01(fraction),1); fill.offsetMin=new Vector2(2,2); fill.offsetMax=new Vector2(-2,-2);
            var label=Text(r,text,18,TextAnchor.MiddleCenter,height); Stretch(label.rectTransform,3,0,3,0); return r;
        }
        public static RectTransform Grid(Transform parent,string name,int columns=4,float cellHeight=112)
        {
            var r=Rect(parent,name); var grid=r.gameObject.AddComponent<GridLayoutGroup>(); grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount=columns;
            grid.spacing=Vector2.one*8; grid.cellSize=new Vector2(100,cellHeight); r.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            r.gameObject.AddComponent<DoodleUiGrid>(); Flexible(r); return r;
        }
        public static Button Slot(Transform parent,string name,string icon,int rarity,int count,int needed,bool equipped,bool locked,Action click,float height=112)
        {
            var r=Box(parent,"Slot: "+name,locked ? new Color(.32f,.33f,.34f) : Rarity(rarity),height); var b=r.gameObject.AddComponent<Button>(); b.onClick.AddListener(()=>click?.Invoke());
            var t=Text(r,GradeName(rarity),16,TextAnchor.UpperLeft,22); Stretch(t.rectTransform,5,height-25,5,2); t.color=locked?Color.white:Ink;
            var im=Icon(r,icon,58); Stretch(im.rectTransform,14,28,14,22); if(locked) im.color=Color.black;
            var gauge=Gauge(r,count+"/"+Mathf.Max(1,needed),count/(float)Mathf.Max(1,needed),19); Stretch(gauge,5,5,5,height-24);
            if(equipped) { var mark=Text(r,"✓",24,TextAnchor.UpperRight,26); mark.color=new Color(.1f,.48f,.07f); Stretch(mark.rectTransform,5,height-30,3,0); }
            if(locked) { var mark=Text(r,"잠김",14,TextAnchor.LowerRight,20); mark.color=Color.white; Stretch(mark.rectTransform,4,25,4,height-46); }
            return b;
        }
        public static Sprite Circle => circle ? circle : circle=Shape(true);
        public static Sprite Frame => frame ? frame : frame=Shape(false);
        static Sprite Shape(bool round)
        {
            const int n=96; var tex=new Texture2D(n,n,TextureFormat.RGBA32,false); var p=new Color[n*n];
            for(int y=0;y<n;y++) for(int x=0;x<n;x++) {
                float dx=Mathf.Abs(x-47.5f),dy=Mathf.Abs(y-47.5f);
                float d=round? Mathf.Sqrt(dx*dx+dy*dy)-45 : new Vector2(Mathf.Max(0,dx-34),Mathf.Max(0,dy-34)).magnitude-12;
                p[y*n+x]=new Color(1,1,1,Mathf.Clamp01(1-d));
            }
            tex.SetPixels(p); tex.Apply(); tex.name=round?"Doodle circle":"Doodle rounded frame";
            return Sprite.Create(tex,new Rect(0,0,n,n),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,round?Vector4.zero:Vector4.one*16);
        }
        public static Sprite Art(string key)
        {
            if(string.IsNullOrEmpty(key)) key="Player"; if(art.TryGetValue(key,out var cached)) return cached;
            string[] atlas={"Diamond","Armor","ArmorMetal","Relic","Stats","Pvp","Dungeon","Shop","Attendance","Roulette","Buffs","Quests","Chat","Settings","Close","Key"};
            int index=Array.IndexOf(atlas,key); Sprite value=null;
            if(index>=0) value=Cell("UI/Icons",index,4,4);
            else {
                switch(key) {
                    case "Player": case "Companion": case "Cloud": value=Cell("Characters",0,3,3); break;
                    case "Club": case "Attack": value=Cell("Characters",4,3,3); break;
                    case "Banana": value=Cell("Characters",5,3,3); break;
                    case "Stone": value=Cell("Characters",6,3,3); break;
                    case "Gold": value=Art("GoldCoin"); break;
                    case "Skill": value=Art("Fireball"); break;
                    case "Mushroom": value=Art("MushroomA"); break;
                    case "Bat": value=Art("BatA"); break;
                    case "Shield": value=Art("ArmorMetal"); break;
                    case "Heart": value=Art("Relic"); break;
                    case "Speed": value=Art("Arrow"); break;
                    default:
                        var tex=Resources.Load<Texture2D>("DoodleIdle/"+key);
                        if(tex) value=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),Vector2.one*.5f,100);
                        break;
                }
            }
            if(!value) value=Cell("Characters",0,3,3); art[key]=value; return value;
        }
        static Sprite Cell(string path,int index,int cols,int rows)
        {
            var tex=Resources.Load<Texture2D>("DoodleIdle/"+path); if(!tex) return null;
            int w=tex.width/cols,h=tex.height/rows,x=index%cols*w,y=(rows-1-index/cols)*h;
            var pixels=tex.GetPixels32(); int left=x+w,right=x,bottom=y+h,top=y;
            for(int yy=y;yy<y+h;yy++) for(int xx=x;xx<x+w;xx++) if(pixels[yy*tex.width+xx].a>32) { left=Mathf.Min(left,xx); right=Mathf.Max(right,xx); bottom=Mathf.Min(bottom,yy); top=Mathf.Max(top,yy); }
            if(left>right) return null;
            return Sprite.Create(tex,new Rect(left,bottom,right-left+1,top-bottom+1),Vector2.one*.5f,100);
        }
    }
    public sealed class DoodleUiGrid : MonoBehaviour
    {
        void OnEnable()=>Resize(); void OnRectTransformDimensionsChange()=>Resize();
        void Resize() { var grid=GetComponent<GridLayoutGroup>(); if(!grid)return; float w=((RectTransform)transform).rect.width; if(w>0) grid.cellSize=new Vector2(Mathf.Max(1,(w-grid.padding.horizontal-grid.spacing.x*(grid.constraintCount-1))/grid.constraintCount),grid.cellSize.y); }
    }
    [Serializable] public sealed class UiReward { public string name,icon; public int amount,rarity; }
}
