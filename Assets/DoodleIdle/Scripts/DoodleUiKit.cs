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
        public static readonly Color Paper = new Color(1f,1f,.99f);
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
        /// <summary>Action area stays reachable while the owning window's long content scrolls.</summary>
        public static RectTransform Footer(RectTransform body,string name,float height)
        {
            var window=body.GetComponentInParent<DoodleUiWindow>();
            if(!window) return Column(body,name,8,0);
            var r=Rect(window.inner,name);r.anchorMin=r.anchorMax=new Vector2(.5f,0);r.pivot=new Vector2(.5f,0);r.anchoredPosition=new Vector2(0,12);r.sizeDelta=new Vector2(window.content.sizeDelta.x+8,height);window.footer=r;
            var layout=r.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=8;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            var viewport=body.parent as RectTransform;if(viewport)viewport.offsetMin=new Vector2(viewport.offsetMin.x,height+24);
            var rail=window.inner.Find("Scroll position") as RectTransform;if(rail)rail.offsetMin=new Vector2(rail.offsetMin.x,height+26);
            return r;
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
            b.onClick.AddListener(()=>click?.Invoke()); var label=Text(r,text,27,TextAnchor.MiddleCenter,height-8); Stretch(label.rectTransform,5,4,5,4); return b;
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
            var r=Box(parent,"Slot: "+name,locked ? new Color(.38f,.39f,.40f) : Color.Lerp(Rarity(rarity),Color.white,.55f),height); var b=r.gameObject.AddComponent<Button>(); b.onClick.AddListener(()=>click?.Invoke());
            r.GetComponent<Outline>().effectColor=locked?Ink:Color.Lerp(Rarity(rarity),Ink,.30f);r.GetComponent<Outline>().effectDistance=new Vector2(3,-3);
            var t=Text(r,GradeName(rarity),20,TextAnchor.UpperLeft,25); Stretch(t.rectTransform,6,height-29,5,3); t.color=locked?Color.white:Ink;
            var im=Icon(r,icon,58); Stretch(im.rectTransform,12,33,12,27); if(locked) im.color=Color.black;
            var gauge=Gauge(r,count+"/"+Mathf.Max(1,needed),count/(float)Mathf.Max(1,needed),24); Stretch(gauge,5,5,5,height-29);gauge.GetComponentInChildren<Text>().resizeTextMaxSize=21;
            if(equipped) { var mark=Box(r,"Equipped check",new Color(.40f,.73f,.15f));mark.GetComponent<Image>().sprite=Circle;mark.anchorMin=mark.anchorMax=Vector2.one;mark.anchoredPosition=new Vector2(-15,-16);mark.sizeDelta=new Vector2(30,30);var tick=Rect(mark,"White check");Stretch(tick);tick.gameObject.AddComponent<DoodleUiCheck>().raycastTarget=false; }
            if(locked) { var mark=Rect(r,"Locked padlock"); mark.anchorMin=mark.anchorMax=new Vector2(1,0);mark.anchoredPosition=new Vector2(-17,41);mark.sizeDelta=new Vector2(20,25);mark.gameObject.AddComponent<DoodleUiPadlock>().raycastTarget=false; }
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
            string[] gear={"Heart","Shield","Speed","Clover","VineClub","ClothClub","SpikeClub","IronClub","ClothArmor","LeatherArmor","WoodArmor","DarkArmor","RedClub","CrystalClub","BoneClub","SunRelic"};
            int gearIndex=Array.IndexOf(gear,key);
            if(gearIndex>=0) value=Cell("UI/GearIcons",gearIndex,4,4);
            else if(index>=0) value=Cell("UI/Icons",index,4,4);
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
                        if(tex) value=tex.isReadable?Cell(key,0,1,1):Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),Vector2.one*.5f,100);
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
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DoodleUiCheck : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Stroke(vh,new Vector2(-8,-1),new Vector2(-2,-7),4);Stroke(vh,new Vector2(-2,-7),new Vector2(9,8),4);
        }
        static void Stroke(VertexHelper vh,Vector2 from,Vector2 to,float width)
        {
            Vector2 d=(to-from).normalized,n=new Vector2(-d.y,d.x)*width*.5f;int start=vh.currentVertCount;Color c=Color.white;
            vh.AddVert(from-n,c,Vector2.zero);vh.AddVert(from+n,c,Vector2.zero);vh.AddVert(to+n,c,Vector2.zero);vh.AddVert(to-n,c,Vector2.zero);vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DoodleUiPadlock : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Quad(vh,-9,-11,9,5,UiKit.Ink);Quad(vh,-7,-9,7,3,Color.white);
            Quad(vh,-6,4,-3,11,Color.white);Quad(vh,3,4,6,11,Color.white);Quad(vh,-4,9,4,12,Color.white);Quad(vh,-1,-6,1,-1,UiKit.Ink);
        }
        static void Quad(VertexHelper vh,float l,float b,float r,float t,Color c) { int s=vh.currentVertCount;vh.AddVert(new Vector2(l,b),c,Vector2.zero);vh.AddVert(new Vector2(l,t),c,Vector2.zero);vh.AddVert(new Vector2(r,t),c,Vector2.zero);vh.AddVert(new Vector2(r,b),c,Vector2.zero);vh.AddTriangle(s,s+1,s+2);vh.AddTriangle(s,s+2,s+3); }
    }
    [Serializable] public sealed class UiReward { public string name,icon; public int amount,rarity; }
}
