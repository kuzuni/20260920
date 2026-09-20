using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace DoodleIdle
{
    /// <summary>Persistent local UI model with replaceable service modules and real combat bridge.</summary>
    public sealed partial class DoodleUi : MonoBehaviour
    {
        DoodleIdleGame game;
        Font font;
        public long Gold = 125480;
        public int Diamonds = 1250;
        public string PlayerName = "먼지고양이";
        public string ActivePage { get; private set; }
        public bool HasOverlay => overlayStack.Count>0;
        public bool BlocksGameplay => !string.IsNullOrEmpty(ActivePage) || HasOverlay || Time.frameCount<=consumeThroughFrame || releaseLatch;
        public Canvas Canvas { get; private set; }
        public RectTransform SafeRoot => safe;
        RectTransform root,safe,pageLayer,overlayLayer,nav,header,skillDock,shortcuts,mission;
        Text walletGold,walletDiamond,profile,missionText,buffGold,buffAttack,toast;
        readonly List<GameObject> overlayStack=new List<GameObject>();
        readonly List<Image> hudMasks=new List<Image>();
        readonly List<Image> hudIcons=new List<Image>();
        readonly List<Image> navIcons=new List<Image>();
        readonly string[] pages={"Stats","Equipment","Skills","Companions","Relics","Dungeons","Pvp","Shop"};
        readonly string[] titles={"스탯","장비","스킬","동료","유물","던전","PVP","상점"};
        readonly string[] navArt={"Stats","ArmorMetal","Banana","Player","Relic","Dungeon","Pvp","Shop"};
        int consumeThroughFrame,lastKills;
        bool releaseLatch,initialized;
        float toastUntil;
        Vector2 previousSize;
        Rect previousSafe;
        Action<RectTransform> fullscreenBuilder;
        string fullscreenTitle;

        public void Initialize(DoodleIdleGame owner,Transform canvasRoot)
        {
            if(initialized)return; initialized=true; game=owner; root=(RectTransform)canvasRoot; Canvas=root.GetComponent<Canvas>();
            font=Resources.Load<Font>("DoodleIdle/InterfaceFont"); UiKit.Font=font;
            long.TryParse(PlayerPrefs.GetString("DoodleUi.Gold","125480"),out Gold); Diamonds=PlayerPrefs.GetInt("DoodleUi.Diamonds",1250);
            InitCollections(); InitCommerce(); InitServices();
            safe=UiKit.Rect(root,"Safe area"); UiKit.Stretch(safe);
            BuildMain();
            pageLayer=UiKit.Rect(root,"Primary modal layer"); UiKit.Stretch(pageLayer);
            BuildNavigation();
            overlayLayer=UiKit.Rect(root,"Detail and fullscreen layer"); UiKit.Stretch(overlayLayer);
            toast=UiKit.Text(root,"",24,TextAnchor.MiddleCenter,58); toast.color=Color.white; toast.gameObject.AddComponent<Outline>().effectColor=Color.black;
            Anchor(toast.rectTransform,new Vector2(.5f,1),new Vector2(0,-115),new Vector2(620,58));
            Relayout(true); RefreshHud();
        }
        void BuildMain()
        {
            header=UiKit.Row(safe,"Profile and currencies",78,10); Anchor(header,new Vector2(.5f,1),new Vector2(0,-50),new Vector2(680,78));
            var p=UiKit.Box(header,"Profile",UiKit.Paper,74); var row=UiKit.Row(p,"Profile contents",70,5); UiKit.Stretch(row,7,2,7,2);
            UiKit.Icon(row,"Player",56); profile=UiKit.Text(row,"",23,TextAnchor.MiddleLeft,62); UiKit.Flexible(p,1.55f);
            var gold=UiKit.Box(header,"Gold wallet",UiKit.Paper,52); var g=UiKit.Row(gold,"Gold",48,1); UiKit.Stretch(g,4,1,4,1); UiKit.Icon(g,"Gold",31); walletGold=UiKit.Text(g,"",21,TextAnchor.MiddleCenter,40);
            var diamond=UiKit.Box(header,"Diamond wallet",UiKit.Paper,52); var d=UiKit.Row(diamond,"Diamonds",48,1); UiKit.Stretch(d,4,1,4,1); UiKit.Icon(d,"Diamond",31); walletDiamond=UiKit.Text(d,"",21,TextAnchor.MiddleCenter,40);
            var settings=IconButton(header,"Settings","설정","Settings",()=>ShowPage("Settings"),50); FixedWidth(settings.transform,50);
            var buffs=UiKit.Row(safe,"Timed buffs",52,8); Anchor(buffs,new Vector2(0,1),new Vector2(78,-119),new Vector2(134,52));
            var bg=UiKit.Column(buffs,"Gold buff",0,0); UiKit.Icon(bg,"Gold",30); buffGold=UiKit.Text(bg,"",16,TextAnchor.MiddleCenter,20);
            var ba=UiKit.Column(buffs,"Attack buff",0,0); UiKit.Icon(ba,"Club",30); buffAttack=UiKit.Text(ba,"",16,TextAnchor.MiddleCenter,20);
            shortcuts=UiKit.Column(safe,"Activities",9,0); Anchor(shortcuts,new Vector2(0,1),new Vector2(12,-161),new Vector2(68,340)); shortcuts.pivot=new Vector2(0,1);
            string[] ids={"Attendance","Roulette","Buffs","Quests","Chat"},names={"출석","룰렛","버프","퀘스트","채팅"};
            for(int i=0;i<ids.Length;i++) { string id=ids[i]; IconButton(shortcuts,id,names[i],id,()=>ShowPage(id),66); }
            mission=UiKit.Box(safe,"Mission",UiKit.Paper,70); Anchor(mission,new Vector2(1,0),new Vector2(-145,207),new Vector2(270,86));
            var m=UiKit.Row(mission,"Mission row",80,6); UiKit.Stretch(m,8,4,8,4); UiKit.Icon(m,"Quests",37); missionText=UiKit.Text(m,"",21,TextAnchor.MiddleLeft,74);
            skillDock=UiKit.Row(safe,"Eight equipped cooldowns",65,8); Anchor(skillDock,new Vector2(.5f,0),new Vector2(0,128),new Vector2(680,65));
            skillDock.gameObject.AddComponent<DoodleUiSquareRow>();
            for(int i=0;i<8;i++) {
                var r=UiKit.Box(skillDock,"Skill status "+i,UiKit.Paper,65); r.GetComponent<Image>().sprite=UiKit.Circle;
                var icon=UiKit.Icon(r,"Banana",48); UiKit.Stretch(icon.rectTransform,8,8,8,8); hudIcons.Add(icon);
                var mask=UiKit.Rect(r,"Clockwise cooldown mask"); UiKit.Stretch(mask,3,3,3,3); var im=mask.gameObject.AddComponent<Image>(); im.sprite=UiKit.Circle; im.type=Image.Type.Filled; im.fillMethod=Image.FillMethod.Radial360; im.fillClockwise=true; im.fillOrigin=(int)Image.Origin360.Top; im.color=new Color(.06f,.07f,.1f,.58f); im.raycastTarget=false; hudMasks.Add(im);
            }
        }
        void BuildNavigation()
        {
            nav=UiKit.Box(root,"Bottom navigation",UiKit.Paper,84); var row=UiKit.Row(nav,"Navigation buttons",78,2); UiKit.Stretch(row,5,3,5,3);
            for(int i=0;i<pages.Length;i++) { string id=pages[i]; var b=IconButton(row,id,titles[i],navArt[i],()=>ShowPage(id),76); navIcons.Add(b.transform.Find("Icon: "+navArt[i]).GetComponent<Image>()); }
        }
        Button IconButton(Transform parent,string id,string label,string icon,Action click,float height)
        {
            var b=UiKit.Button(parent,"",click,UiKit.Paper,height); b.name=id; var im=UiKit.Icon(b.transform,icon,40); UiKit.Stretch(im.rectTransform,7,25,7,5);
            var text=b.GetComponentInChildren<Text>(); text.text=label; text.resizeTextMaxSize=19; UiKit.Stretch(text.rectTransform,1,2,1,height-25); return b;
        }
        static void FixedWidth(Transform t,float width) { var l=t.GetComponent<LayoutElement>()??t.gameObject.AddComponent<LayoutElement>(); l.minWidth=l.preferredWidth=width; l.flexibleWidth=0; }
        static void Anchor(RectTransform r,Vector2 anchor,Vector2 offset,Vector2 size) { r.anchorMin=r.anchorMax=anchor; r.pivot=Vector2.one*.5f; r.anchoredPosition=offset; r.sizeDelta=size; }
        public void Relayout(bool force=false)
        {
            if(!initialized)return;
            var size=root.rect.size; var area=Screen.safeArea;
            if(!force && size==previousSize && area==previousSafe)return;
            previousSize=size; previousSafe=area;
            bool capture=Canvas.renderMode==RenderMode.ScreenSpaceCamera && Canvas.worldCamera && Canvas.worldCamera.targetTexture;
            safe.anchorMin=capture?Vector2.zero:new Vector2(area.x/Mathf.Max(1,Screen.width),area.y/Mathf.Max(1,Screen.height));
            safe.anchorMax=capture?Vector2.one:new Vector2(area.xMax/Mathf.Max(1,Screen.width),area.yMax/Mathf.Max(1,Screen.height)); safe.offsetMin=safe.offsetMax=Vector2.zero;
            float w=Mathf.Min(700,safe.rect.width-24);
            header.sizeDelta=new Vector2(w,78); skillDock.sizeDelta=new Vector2(w,65);
            nav.anchorMin=new Vector2(safe.anchorMin.x,safe.anchorMin.y); nav.anchorMax=new Vector2(safe.anchorMax.x,safe.anchorMin.y); nav.pivot=new Vector2(.5f,0); nav.offsetMin=new Vector2(12,10); nav.offsetMax=new Vector2(-12,94);
            if(size.x>900) { nav.anchorMin=nav.anchorMax=new Vector2(.5f,safe.anchorMin.y); nav.sizeDelta=new Vector2(760,84); nav.anchoredPosition=new Vector2(0,10); }
            foreach(var window in root.GetComponentsInChildren<DoodleUiWindow>()) window.Reflow(safe);
        }
        public void ShowPage(string id)
        {
            ConsumeGesture(); ClearOverlays();
            ActivePage=ActivePage==id?null:id; RenderPage(); RefreshHud();
        }
        public void ClosePage() { ConsumeGesture(); ClearOverlays(); ActivePage=null; RenderPage(); }
        public void RefreshPage() { if(!string.IsNullOrEmpty(ActivePage)) RenderPage(); RefreshHud(); }
        void RenderPage()
        {
            ClearChildren(pageLayer);
            nav.gameObject.SetActive(ActivePage!="Chat");
            for(int i=0;i<navIcons.Count;i++) navIcons[i].sprite=UiKit.Art(ActivePage==pages[i]?"Close":navArt[i]);
            if(string.IsNullOrEmpty(ActivePage))return;
            bool full=ActivePage=="Chat";
            var dim=Dim(pageLayer,"Dim: "+ActivePage,()=>ClosePage(),full?1:.54f);
            var body=Window(dim,Title(ActivePage),full,()=>ClosePage()); BuildPage(body);
        }
        static string Title(string id)
        {
            string[] ids={"Stats","Equipment","Skills","Companions","Relics","Dungeons","Pvp","Shop","Attendance","Roulette","Buffs","Quests","Chat","Settings"};
            string[] names={"스탯","장비","스킬","동료","유물","던전","PVP","상점","출석 보상","룰렛","버프","퀘스트","채팅","설정"}; int i=Array.IndexOf(ids,id); return i<0?id:names[i];
        }
        void BuildPage(RectTransform body)
        {
            switch(ActivePage) {
                case "Stats": BuildStats(body); break; case "Equipment": BuildEquipment(body); break; case "Skills": BuildSkills(body); break; case "Companions": BuildCompanions(body); break; case "Relics": BuildRelics(body); break;
                case "Dungeons": BuildDungeons(body); break; case "Pvp": BuildPvp(body); break; case "Shop": BuildShop(body); break; case "Attendance": BuildAttendance(body); break; case "Roulette": BuildRoulette(body); break; case "Buffs": BuildBuffs(body); break; case "Quests": BuildQuests(body); break; case "Chat": BuildChat(body); break; case "Settings": BuildSettings(body); break;
            }
        }
        RectTransform Dim(Transform parent,string name,Action close,float alpha)
        {
            var dim=UiKit.Rect(parent,name); UiKit.Stretch(dim); var im=dim.gameObject.AddComponent<Image>(); im.color=new Color(.03f,.025f,.015f,alpha); dim.gameObject.AddComponent<Button>().onClick.AddListener(()=>close()); return dim;
        }
        RectTransform Window(RectTransform dim,string title,bool full,Action close)
        {
            var panel=UiKit.Box(dim,"Panel: "+title,UiKit.Paper); panel.GetComponent<LayoutElement>().ignoreLayout=true;
            float w=Mathf.Min(610,safe.rect.width-44),h=Mathf.Min(940,Mathf.Max(200,safe.rect.height-335));
            if(full) UiKit.Stretch(panel); else Anchor(panel,new Vector2(.5f,.5f),new Vector2(0,-5),new Vector2(w,h));
            var block=panel.gameObject.AddComponent<Button>(); block.transition=Selectable.Transition.None; // Stop the dim handler receiving panel clicks.
            var inner=UiKit.Rect(panel,"Safe panel contents");
            if(full) { inner.anchorMin=safe.anchorMin; inner.anchorMax=safe.anchorMax; inner.offsetMin=inner.offsetMax=Vector2.zero; }
            else UiKit.Stretch(inner);
            var titleText=UiKit.Text(inner,title,42,TextAnchor.MiddleCenter,62); titleText.rectTransform.anchorMin=new Vector2(0,1); titleText.rectTransform.anchorMax=Vector2.one; titleText.rectTransform.pivot=new Vector2(.5f,1); titleText.rectTransform.offsetMin=new Vector2(56,-76); titleText.rectTransform.offsetMax=new Vector2(-56,-8);
            var x=UiKit.Button(inner,"",close,Color.clear,46); x.name="Close "+title; x.GetComponent<Outline>().enabled=false; Anchor((RectTransform)x.transform,new Vector2(1,1),new Vector2(-35,-34),new Vector2(46,46)); var icon=UiKit.Icon(x.transform,"Close",40); UiKit.Stretch(icon.rectTransform,3,3,3,3);
            var viewport=UiKit.Rect(inner,"Viewport"); UiKit.Stretch(viewport,17,18,17,82); viewport.gameObject.AddComponent<RectMask2D>(); var bg=viewport.gameObject.AddComponent<Image>(); bg.color=Color.clear;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=38; scroll.viewport=viewport;
            var content=UiKit.Column(viewport,"Scrollable content",10,4); content.anchorMin=new Vector2(.5f,1); content.anchorMax=new Vector2(.5f,1); content.pivot=new Vector2(.5f,1); content.anchoredPosition=Vector2.zero; content.sizeDelta=new Vector2(full?Mathf.Min(760,safe.rect.width-42):w-42,0);
            scroll.content=content;
            var responsive=panel.gameObject.AddComponent<DoodleUiWindow>(); responsive.full=full; responsive.content=content; responsive.inner=inner; responsive.Reflow(safe);
            return content;
        }
        public void ShowDetail(string title,Action<RectTransform> build)
        {
            ConsumeGesture(); var dim=Dim(overlayLayer,"Detail dim: "+title,CloseDetail,.56f); overlayStack.Add(dim.gameObject); var body=Window(dim,title,false,CloseDetail); build(body);
        }
        public void CloseDetail() { ConsumeGesture(); if(overlayStack.Count==0)return; var last=overlayStack[overlayStack.Count-1]; overlayStack.RemoveAt(overlayStack.Count-1); last.SetActive(false); Destroy(last); RefreshHud(); }
        public void ShowFullscreen(string title,Action<RectTransform> build)
        {
            if(fullscreenBuilder!=null && overlayStack.Count>0) CloseDetail();
            ConsumeGesture(); fullscreenBuilder=build; fullscreenTitle=title; var dim=Dim(overlayLayer,"Fullscreen: "+title,()=>{},1); overlayStack.Add(dim.gameObject); build(Window(dim,title,true,CloseFullscreen));
        }
        public void CloseFullscreen() { CloseDetail(); fullscreenBuilder=null; fullscreenTitle=null; RefreshPage(); }
        public void ShowRewards(string title,List<UiReward> rewards)
        {
            ConsumeGesture(); var dim=Dim(overlayLayer,"Reward dim",CloseDetail,.77f); overlayStack.Add(dim.gameObject);
            var area=UiKit.Column(dim,"Floating rewards",20,4); Anchor(area,new Vector2(.5f,.5f),Vector2.zero,new Vector2(Mathf.Min(670,safe.rect.width-42),300));
            var reflow=area.gameObject.AddComponent<DoodleUiRewardLayout>(); reflow.safe=safe;
            var text=UiKit.Text(area,title,43,TextAnchor.MiddleCenter,70); text.color=Color.white; text.gameObject.AddComponent<Outline>().effectColor=UiKit.Ink;
            var row=UiKit.Row(area,"Individual rewards",170,20);
            foreach(var reward in rewards) {
                var host=UiKit.Rect(row,"Reward "+reward.name); FixedWidth(host,Mathf.Min(166,(safe.rect.width-90)/Mathf.Max(1,rewards.Count))); UiKit.Height(host,166);
                var halo=UiKit.Rect(host,"Golden hand drawn rays"); UiKit.Stretch(halo,-24,-24,-24,-24); var rays=halo.gameObject.AddComponent<DoodleRewardRays>(); rays.color=new Color(1,.84f,.21f,.92f); rays.raycastTarget=false;
                var card=UiKit.Box(host,"Reward frame",UiKit.Rarity(reward.rarity)); UiKit.Stretch(card,3,3,3,3); card.GetComponent<Image>().raycastTarget=false;
                var icon=UiKit.Icon(card,reward.icon,88); UiKit.Stretch(icon.rectTransform,13,47,13,11);
                var count=UiKit.Text(card,reward.amount.ToString("N0"),28,TextAnchor.MiddleCenter,36); UiKit.Stretch(count.rectTransform,5,5,5,124);
            }
            var hint=UiKit.Text(area,"화면을 터치하면 닫힙니다",23,TextAnchor.MiddleCenter,46); hint.color=Color.white;
        }
        public void Toast(string message) { if(!toast)return; toast.text=message; toastUntil=Time.unscaledTime+3.5f; toast.transform.SetAsLastSibling(); }
        public void Save() { PlayerPrefs.SetString("DoodleUi.Gold",Gold.ToString()); PlayerPrefs.SetInt("DoodleUi.Diamonds",Diamonds); SaveCollections(); SaveCommerce(); SaveServices(); PlayerPrefs.Save(); }
        void OnApplicationPause(bool paused) { if(initialized&&paused)Save(); }
        void OnApplicationQuit() { if(initialized)Save(); }
        void ConsumeGesture() { consumeThroughFrame=Time.frameCount+2; releaseLatch=Pointer.current!=null&&Pointer.current.press.isPressed; game.CancelUiPointer(); }
        void ClearOverlays() { foreach(var go in overlayStack) if(go){go.SetActive(false);Destroy(go);} overlayStack.Clear(); }
        static void ClearChildren(Transform parent) { if(!parent)return; foreach(Transform child in parent) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
        void LateUpdate()
        {
            if(!initialized)return; if(releaseLatch&&(Pointer.current==null||!Pointer.current.press.isPressed))releaseLatch=false;
            Relayout(); TickServices();
            if(game.Kills<lastKills)lastKills=game.Kills;
            if(game.Kills>lastKills) { int earned=Mathf.Max(1,Mathf.RoundToInt((game.Kills-lastKills)*10*GoldGainMultiplier*GoldBuffMultiplier)); Gold+=earned; RecordServiceProgress("gold",earned); lastKills=game.Kills; }
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(HasOverlay)CloseDetail();else if(ActivePage!=null)ClosePage();}
            RefreshHud(); if(toast&&Time.unscaledTime>toastUntil)toast.text="";
        }
        public void RefreshHud()
        {
            if(!initialized)return; profile.text=PlayerName+"\n전투력 "+Power.ToString("N0"); walletGold.text=Gold.ToString("N0"); walletDiamond.text=Diamonds.ToString("N0");
            buffGold.text=Duration(GoldBuffSeconds); buffAttack.text=Duration(AttackBuffSeconds); missionText.text=ActiveDungeonIndex>=0?DungeonMission:"미션 5.\n공격력 15단계 달성\n("+AttackStatLevel+"/15)";
            var skills=EquippedSkills; for(int i=0;i<8;i++) { bool found=i<skills.Count; hudIcons[i].sprite=UiKit.Art(found?skills[i].icon:"Banana"); hudIcons[i].color=found?Color.white:new Color(1,1,1,.15f); hudMasks[i].fillAmount=found?game.UiCooldown(skills[i].ability):0; }
        }
        static string Duration(double seconds) { return seconds>0?TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss"):"00:00"; }
        public float UiDamageMultiplier => CombatDamageMultiplier*AttackBuffMultiplier;
        public float UiSpeedMultiplier => CombatAttackSpeedMultiplier;
    }
    public sealed class DoodleUiWindow : MonoBehaviour
    {
        public bool full;
        public RectTransform content,inner;
        public void Reflow(RectTransform safe)
        {
            var panel=(RectTransform)transform;
            float width=Mathf.Min(610,safe.rect.width-44);
            if(full) { UiKit.Stretch(panel); inner.anchorMin=safe.anchorMin;inner.anchorMax=safe.anchorMax;inner.offsetMin=inner.offsetMax=Vector2.zero; width=Mathf.Min(802,safe.rect.width); }
            else { panel.anchorMin=panel.anchorMax=(safe.anchorMin+safe.anchorMax)*.5f; panel.sizeDelta=new Vector2(width,Mathf.Min(940,Mathf.Max(200,safe.rect.height-335))); }
            content.sizeDelta=new Vector2(width-42,content.sizeDelta.y);
        }
    }
    public sealed class DoodleUiSquareRow : MonoBehaviour
    {
        void LateUpdate() { var r=(RectTransform)transform; var row=GetComponent<HorizontalLayoutGroup>(); float size=Mathf.Min(65,(r.rect.width-row.spacing*7)/8); foreach(Transform c in transform) { var e=c.GetComponent<LayoutElement>(); if(e){e.minWidth=e.preferredWidth=size;e.flexibleWidth=0;e.minHeight=e.preferredHeight=size;} } }
    }
    public sealed class DoodleUiRewardLayout : MonoBehaviour
    {
        public RectTransform safe;
        void LateUpdate() { if(!safe)return; var rect=(RectTransform)transform; rect.sizeDelta=new Vector2(Mathf.Min(670,safe.rect.width-42),rect.sizeDelta.y); rect.anchorMin=rect.anchorMax=(safe.anchorMin+safe.anchorMax)*.5f; }
    }
    public sealed class DoodleRewardRays : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var rect=rectTransform.rect; Vector2 center=rect.center; float radius=Mathf.Min(rect.width,rect.height)*.5f;
            for(int i=0;i<16;i++) {
                float a=i*Mathf.PI/8; Vector2 dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),side=new Vector2(-dir.y,dir.x); float inner=radius*.62f,outer=radius*(i%3==0?1:.9f); int start=vh.currentVertCount;
                Add(vh,center+dir*inner-side*2,color); Add(vh,center+dir*outer-side*4,color); Add(vh,center+dir*outer+side*4,color); Add(vh,center+dir*inner+side*2,color); vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
            }
            // Soft yellow radial backdrop is independent of the item frames.
            for(int i=0;i<32;i++) { int start=vh.currentVertCount; Add(vh,center,new Color(1,.8f,.12f,.5f)); float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16; Add(vh,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,new Color(1,.8f,.12f,0)); Add(vh,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,new Color(1,.8f,.12f,0)); vh.AddTriangle(start,start+1,start+2); }
        }
        static void Add(VertexHelper vh,Vector2 pos,Color c) { var v=UIVertex.simpleVert; v.position=pos; v.color=c; vh.AddVert(v); }
    }
}
