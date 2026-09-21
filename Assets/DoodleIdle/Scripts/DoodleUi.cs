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
        public long Gold;
        public int Diamonds;
        public string PlayerName = "먼지고양이";
        public string ActivePage { get; private set; }
        public bool HasOverlay => overlayStack.Count>0;
        public bool BlocksGameplay => !string.IsNullOrEmpty(ActivePage) || HasOverlay || Time.frameCount<=consumeThroughFrame || releaseLatch;
        public Canvas Canvas { get; private set; }
        public RectTransform SafeRoot => safe;
        RectTransform root,safe,pageLayer,overlayLayer,nav,header,skillDock,shortcuts,mission;
        Text walletGold,walletDiamond,profile,missionText,buffGold,buffAttack,toast;
        Text powerToast,cameraLabel,stageLabel;
        Button missionClaim, breakthroughButton;
        RectTransform cameraControl,stageInfo;
        public int CameraMode { get; private set; } = 1;
        float powerToastUntil;
        bool rebuildingPage;
        Image goldBuffSurface,attackBuffSurface;
        readonly List<GameObject> overlayStack=new List<GameObject>();
        readonly List<Image> hudMasks=new List<Image>();
        readonly List<Image> hudIcons=new List<Image>();
        readonly List<DoodleUiPadlock> hudLocks=new List<DoodleUiPadlock>();
        readonly List<Image> navIcons=new List<Image>();
        readonly List<Text> navLabels=new List<Text>();
        readonly string[] pages={"Stats","Equipment","Skills","Companions","Relics","Skins","Dungeons","Pvp","Shop"};
        readonly string[] titles={"스탯","장비","스킬","동료","유물","스킨","던전","PVP","상점"};
        readonly string[] navArt={"Stats","ArmorMetal","SkillMeteor","CompanionMon_4","NavPottery","ClothArmor","Dungeon","NavColosseum","Shop"};
        int consumeThroughFrame,lastKills;
        bool releaseLatch,initialized,suppressSaving;
        float toastUntil;
        float nextWalletSave=15;
        Vector2 previousSize;
        Rect previousSafe;
        Action<RectTransform> fullscreenBuilder;
        string fullscreenTitle;

        public void Initialize(DoodleIdleGame owner,Transform canvasRoot)
        {
            if(initialized)return; initialized=true; game=owner; root=(RectTransform)canvasRoot; Canvas=root.GetComponent<Canvas>();
            font=Resources.Load<Font>("DoodleIdle/UI/DisplayFont"); UiKit.Font=font;
            long.TryParse(PlayerPrefs.GetString("DoodleUi.Gold","0"),out Gold); Diamonds=PlayerPrefs.GetInt("DoodleUi.Diamonds",0);
            InitCollections(); InitSkins(); InitCommerce(); InitServices();
            CameraMode=Mathf.Clamp(PlayerPrefs.GetInt("DoodleUi.CameraMode",1),1,3); ApplyCameraMode();
            safe=UiKit.Rect(root,"Safe area"); UiKit.Stretch(safe);
            BuildMain();
            pageLayer=UiKit.Rect(root,"Primary modal layer"); UiKit.Stretch(pageLayer);
            BuildNavigation();
            overlayLayer=UiKit.Rect(root,"Detail and fullscreen layer"); UiKit.Stretch(overlayLayer);
            toast=BuildToast("Toast",24,-115,70);
            powerToast=BuildToast("Power change toast",27,-195,76);
            Relayout(true); RefreshHud();
        }
        void BuildMain()
        {
            header=UiKit.Row(safe,"Profile and currencies",92,9); Anchor(header,new Vector2(.5f,1),new Vector2(0,-58),new Vector2(696,92));
            var p=UiKit.Box(header,"Profile",UiKit.Paper,84); var row=UiKit.Row(p,"Profile contents",80,7); UiKit.Stretch(row,9,2,9,2);
            UiKit.Icon(row,"Player",72); profile=UiKit.Text(row,"",28,TextAnchor.MiddleLeft,76); UiKit.Flexible(p,1.75f);
            var gold=UiKit.Box(header,"Gold wallet",UiKit.Paper,66); var g=UiKit.Row(gold,"Gold",62,2); UiKit.Stretch(g,7,2,7,2); UiKit.Icon(g,"Gold",38); walletGold=UiKit.Text(g,"",27,TextAnchor.MiddleCenter,52); UiKit.Flexible(gold,1.05f);
            var diamond=UiKit.Box(header,"Diamond wallet",UiKit.Paper,66); var d=UiKit.Row(diamond,"Diamonds",62,2); UiKit.Stretch(d,7,2,7,2); UiKit.Icon(d,"Diamond",39); walletDiamond=UiKit.Text(d,"",27,TextAnchor.MiddleCenter,52); UiKit.Flexible(diamond,.9f);
            var settings=IconButton(header,"Settings","","Settings",()=>ShowPage("Settings"),68); FixedWidth(settings.transform,64); settings.GetComponent<Image>().color=Color.clear; settings.GetComponent<Outline>().enabled=false; UiKit.Stretch(settings.transform.Find("Icon: Settings") as RectTransform,0,0,0,0);
            var buffs=UiKit.Row(safe,"Timed buffs",86,12); Anchor(buffs,new Vector2(0,1),new Vector2(88,-160),new Vector2(142,86));
            buffGold=BuildBuff(buffs,"Gold buff","Gold"); buffAttack=BuildBuff(buffs,"Attack buff","Club");
            shortcuts=UiKit.Column(safe,"Activities",12,0); Anchor(shortcuts,new Vector2(0,1),new Vector2(16,-215),new Vector2(84,528)); shortcuts.pivot=new Vector2(0,1);
            string[] ids={"Attendance","Roulette","Buffs","Quests","Chat"},names={"출석","룰렛","버프","퀘스트","채팅"};
            for(int i=0;i<ids.Length;i++) { string id=ids[i]; IconButton(shortcuts,id,names[i],id,()=>ShowPage(id),96); }
            mission=UiKit.Box(safe,"Mission",UiKit.Paper,144); Anchor(mission,new Vector2(1,0),new Vector2(-160,396),new Vector2(300,144));
            var m=UiKit.Row(mission,"Mission row",100,8); UiKit.Stretch(m,13,50,13,8); UiKit.Icon(m,"Quests",44); missionText=UiKit.Text(m,"",24,TextAnchor.MiddleLeft,96);
            missionClaim=UiKit.Button(mission,"",()=>ClaimMainMission(),UiKit.Yellow,34);missionClaim.name="Claim main mission";
            var claimRect=(RectTransform)missionClaim.transform;claimRect.anchorMin=new Vector2(1f/6,0);claimRect.anchorMax=new Vector2(5f/6,0);claimRect.offsetMin=new Vector2(0,8);claimRect.offsetMax=new Vector2(0,42);
            missionClaim.GetComponentInChildren<Text>().gameObject.SetActive(false);
            var rewardRow=UiKit.Row(missionClaim.transform,"Mission reward",30,4);UiKit.Stretch(rewardRow,8,2,8,2);
            UiKit.Text(rewardRow,"500",22,TextAnchor.MiddleRight,30);
            UiKit.Icon(rewardRow,"Diamond",24);
            UiKit.Text(rewardRow,"받기",22,TextAnchor.MiddleLeft,30);
            var cameraButton=UiKit.Button(safe,"카메라  1",CycleCameraMode,UiKit.Paper,40);cameraButton.name="Camera mode";
            cameraControl=(RectTransform)cameraButton.transform;cameraLabel=cameraButton.GetComponentInChildren<Text>();cameraLabel.resizeTextMaxSize=23;
            stageInfo=UiKit.Rect(safe,"Stage progress");stageLabel=UiKit.Text(stageInfo,"",28,TextAnchor.MiddleCenter,68);UiKit.Stretch(stageLabel.rectTransform,0,44,0,0);
            stageLabel.supportRichText=false;stageLabel.color=UiKit.Ink;stageLabel.gameObject.AddComponent<Outline>().effectColor=UiKit.Paper;
            breakthroughButton=UiKit.Button(stageInfo,"돌파 모드",ToggleBreakthroughMode,UiKit.Green,36);
            var modeRect=(RectTransform)breakthroughButton.transform;modeRect.anchorMin=new Vector2(.12f,0);modeRect.anchorMax=new Vector2(.88f,0);modeRect.offsetMin=Vector2.zero;modeRect.offsetMax=new Vector2(0,36);
            breakthroughButton.GetComponentInChildren<Text>().resizeTextMaxSize=23;
            skillDock=UiKit.Row(safe,"Eight equipped cooldowns",84,8); Anchor(skillDock,new Vector2(.5f,0),new Vector2(0,249),new Vector2(696,84));
            skillDock.gameObject.AddComponent<DoodleUiSquareRow>();
            for(int i=0;i<8;i++) {
                var r=UiKit.Box(skillDock,"Skill status "+i,UiKit.Paper,65); r.GetComponent<Image>().sprite=UiKit.Circle;
                var icon=UiKit.Icon(r,"Banana",48); UiKit.Stretch(icon.rectTransform,8,8,8,8); hudIcons.Add(icon);
                var mask=UiKit.Rect(r,"Clockwise cooldown mask"); UiKit.Stretch(mask,3,3,3,3); var im=mask.gameObject.AddComponent<Image>(); im.sprite=UiKit.Circle; im.type=Image.Type.Filled; im.fillMethod=Image.FillMethod.Radial360; im.fillClockwise=true; im.fillOrigin=(int)Image.Origin360.Top; im.color=new Color(.06f,.07f,.1f,.58f); im.raycastTarget=false; hudMasks.Add(im);
                var lockRect=UiKit.Rect(r,"HUD skill lock"); lockRect.anchorMin=lockRect.anchorMax=Vector2.one*.5f; lockRect.sizeDelta=new Vector2(22,28);
                var padlock=lockRect.gameObject.AddComponent<DoodleUiPadlock>(); padlock.raycastTarget=false; hudLocks.Add(padlock);
            }
        }
        Text BuildBuff(Transform parent,string name,string art)
        {
            var host=UiKit.Rect(parent,name); FixedWidth(host,64); UiKit.Height(host,86);
            var hit=host.gameObject.AddComponent<Image>();hit.color=Color.clear;
            var button=host.gameObject.AddComponent<Button>();button.targetGraphic=hit;button.transition=Selectable.Transition.None;button.onClick.AddListener(()=>ShowPage("Buffs"));
            var circle=UiKit.Box(host,"Buff circle",UiKit.Paper); circle.GetComponent<Image>().sprite=UiKit.Circle; Anchor(circle,new Vector2(.5f,1),new Vector2(0,-30),new Vector2(60,60));
            var icon=UiKit.Icon(circle,art,44); UiKit.Stretch(icon.rectTransform,8,8,8,8);
            if(art=="Gold")goldBuffSurface=circle.GetComponent<Image>();else attackBuffSurface=circle.GetComponent<Image>();
            var timer=UiKit.Text(host,"",21,TextAnchor.MiddleCenter,26); UiKit.Stretch(timer.rectTransform,0,0,0,60); timer.gameObject.AddComponent<Outline>().effectColor=Color.white; return timer;
        }
        void BuildNavigation()
        {
            nav=UiKit.Box(root,"Bottom navigation",UiKit.Paper,146); var row=UiKit.Row(nav,"Navigation buttons",136,2); UiKit.Stretch(row,5,6,5,6);
            for(int i=0;i<pages.Length;i++) {
                string id=pages[i]; var b=IconButton(row,id,titles[i],navArt[i],()=>ShowPage(id),132); b.GetComponent<Image>().color=Color.clear;b.GetComponent<Outline>().enabled=false;
                navIcons.Add(b.transform.Find("Icon: "+navArt[i]).GetComponent<Image>()); navLabels.Add(b.GetComponentInChildren<Text>());
                if(i>0) { var separator=UiKit.Rect(b.transform,"Menu divider"); separator.anchorMin=new Vector2(0,0);separator.anchorMax=new Vector2(0,1);separator.offsetMin=new Vector2(-2,8);separator.offsetMax=new Vector2(1,-8);separator.gameObject.AddComponent<Image>().color=new Color(.82f,.79f,.72f); }
            }
        }
        Button IconButton(Transform parent,string id,string label,string icon,Action click,float height)
        {
            var b=UiKit.Button(parent,"",click,UiKit.Paper,height); b.name=id; var im=UiKit.Icon(b.transform,icon,40); UiKit.Stretch(im.rectTransform,7,25,7,5);
            var text=b.GetComponentInChildren<Text>(); text.text=label; text.resizeTextMaxSize=27; UiKit.Stretch(text.rectTransform,1,4,1,height-32); return b;
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
            // CanvasScaler matches the 9:19 reference height for every UI element.
            // Keep the reference composition centered instead of stretching HUD rows across a wide monitor.
            float centerX=(safe.anchorMin.x+safe.anchorMax.x)*.5f;
            float halfWidth=Mathf.Min(720,safe.rect.width)*.5f/Mathf.Max(1,size.x);
            safe.anchorMin=new Vector2(centerX-halfWidth,safe.anchorMin.y);
            safe.anchorMax=new Vector2(centerX+halfWidth,safe.anchorMax.y);
            float w=Mathf.Min(700,safe.rect.width-24);
            bool tall=safe.rect.height>=1100; float navHeight=tall?146:92,navBottom=tall?20:10;
            header.sizeDelta=new Vector2(w,92); skillDock.sizeDelta=new Vector2(w,tall?84:65);skillDock.anchoredPosition=new Vector2(0,tall?249:148);
            nav.anchorMin=new Vector2(safe.anchorMin.x,safe.anchorMin.y); nav.anchorMax=new Vector2(safe.anchorMax.x,safe.anchorMin.y); nav.pivot=new Vector2(.5f,0); nav.offsetMin=new Vector2(12,navBottom); nav.offsetMax=new Vector2(-12,navBottom+navHeight);
            foreach(var label in navLabels) { UiKit.Height(label.transform.parent,navHeight-14);label.resizeTextMaxSize=tall?27:21;UiKit.Stretch(label.rectTransform,1,5,1,navHeight-48);var icon=label.transform.parent.GetComponentsInChildren<Image>()[1]; UiKit.Stretch(icon.rectTransform,7,tall?43:30,7,tall?18:6); }
            bool compactActivities=safe.rect.height<1160;
            shortcuts.anchoredPosition=new Vector2(16,compactActivities?-210:-215);shortcuts.sizeDelta=new Vector2(compactActivities?110:84,shortcuts.sizeDelta.y);shortcuts.GetComponent<VerticalLayoutGroup>().spacing=compactActivities?4:12;
            foreach(Transform button in shortcuts) {
                float height=compactActivities?44:96;UiKit.Height(button,height);var label=button.GetComponentInChildren<Text>();label.resizeTextMaxSize=compactActivities?19:27;
                var icon=button.GetComponentsInChildren<Image>()[1].rectTransform;
                if(compactActivities) { UiKit.Stretch(label.rectTransform,42,3,2,3);Anchor(icon,new Vector2(0,.5f),new Vector2(22,0),new Vector2(32,32)); }
                else { UiKit.Stretch(label.rectTransform,1,2,1,height-32);UiKit.Stretch(icon,7,33,7,5); }
            }
            mission.sizeDelta=new Vector2(tall?300:270,tall?184:152);mission.anchoredPosition=new Vector2(tall?-160:-145,tall?408:278);missionText.resizeTextMaxSize=tall?24:19;UiKit.Height(missionText.transform,tall?96:68);
            Anchor(cameraControl,new Vector2(0,0),new Vector2(96,tall?329:208),new Vector2(164,tall?42:34));
            Anchor(stageInfo,new Vector2(.5f,1),new Vector2(0,-191),new Vector2(230,140));
            foreach(var window in root.GetComponentsInChildren<DoodleUiWindow>()) if(!window.GetComponent<DoodlePopupMotion>() || !window.GetComponent<DoodlePopupMotion>().IsClosing) window.Reflow(safe);
            foreach(var rewards in root.GetComponentsInChildren<DoodleUiRewardLayout>()) rewards.Reflow();
            foreach(var squares in root.GetComponentsInChildren<DoodleUiSquareRow>()) squares.Reflow();
            UnityEngine.Canvas.ForceUpdateCanvases();
            foreach(var collection in root.GetComponentsInChildren<DoodleCollectionReferenceLayout>()) collection.Reflow();
            UnityEngine.Canvas.ForceUpdateCanvases();
            ReflowServiceLayouts();
            foreach(var inventory in root.GetComponentsInChildren<DoodleCollectionInventoryViewport>()) inventory.Reflow();
            foreach(var commerce in root.GetComponentsInChildren<DoodleCommerceLayout>()) commerce.Reflow();
            foreach(var grid in root.GetComponentsInChildren<DoodleUiGrid>()) grid.Reflow();
            UnityEngine.Canvas.ForceUpdateCanvases();
            foreach(var slot in root.GetComponentsInChildren<DoodleUiSlotLayout>()) slot.Reflow();
            foreach(var button in root.GetComponentsInChildren<Button>())
            {
                var element=button.GetComponent<LayoutElement>();
                if(!element || element.ignoreLayout || button.GetComponent<DoodleButtonMotion>())continue;
                var motion=button.gameObject.AddComponent<DoodleButtonMotion>();var action=button.onClick;
                button.onClick=new Button.ButtonClickedEvent();button.onClick.AddListener(()=>{motion.Pulse();action.Invoke();});
            }
        }
        public void ShowPage(string id)
        {
            StopRepeating();
            if(ActivePage==id){ClosePage();return;}
            ConsumeGesture(); ClearOverlays(); pendingEquip=null;
            ActivePage=id; RenderPage(); RefreshHud();
        }
        public void ClosePage() { pendingEquip=null; StopRepeating(); ConsumeGesture(); ClearOverlays(); if(pageLayer&&pageLayer.childCount>0)DoodlePopupMotion.Close(pageLayer.GetChild(0).gameObject,root); ActivePage=null; RenderPage(); }
        public void RefreshPage()
        {
            var previous=pageLayer?pageLayer.GetComponentInChildren<ScrollRect>():null;
            float position=previous&&previous.content.rect.height>previous.viewport.rect.height+1?previous.verticalNormalizedPosition:1;
            if(!string.IsNullOrEmpty(ActivePage)) { rebuildingPage=true;try{RenderPage();}finally{rebuildingPage=false;} Relayout(true); var next=pageLayer.GetComponentInChildren<ScrollRect>(); if(next)next.verticalNormalizedPosition=position; }
            RefreshHud();
        }
        void RenderPage()
        {
            ClearChildren(pageLayer);
            nav.gameObject.SetActive(true);
            for(int i=0;i<navIcons.Count;i++) { navIcons[i].sprite=UiKit.Art(ActivePage==pages[i]?"Close":navArt[i]);navLabels[i].text=ActivePage==pages[i]?"":titles[i]; }
            if(string.IsNullOrEmpty(ActivePage))return;
            bool full=false;
            var dim=Dim(pageLayer,"Dim: "+ActivePage,()=>ClosePage(),full?1:.30f);
            var body=Window(dim,Title(ActivePage),full,()=>ClosePage(),Array.IndexOf(pages,ActivePage)>=0 || ActivePage=="Chat"); BuildPage(body); Relayout(true);
        }
        static string Title(string id)
        {
            string[] ids={"Stats","Equipment","Skills","Companions","Relics","Skins","Dungeons","Pvp","Shop","Attendance","Roulette","Buffs","Quests","Chat","Settings"};
            string[] names={"스탯","장비","스킬","동료","유물","스킨","던전","PVP","상점","출석 보상","룰렛","버프","퀘스트","채팅","설정"}; int i=Array.IndexOf(ids,id); return i<0?id:names[i];
        }
        void BuildPage(RectTransform body)
        {
            switch(ActivePage) {
                case "Stats": BuildStats(body); break; case "Equipment": BuildEquipment(body); break; case "Skills": BuildSkills(body); break; case "Companions": BuildCompanions(body); break; case "Relics": BuildRelics(body); break; case "Skins": BuildSkins(body); break;
                case "Dungeons": BuildDungeons(body); break; case "Pvp": BuildPvp(body); break; case "Shop": BuildShop(body); break; case "Attendance": BuildAttendance(body); break; case "Roulette": BuildRoulette(body); break; case "Buffs": BuildBuffs(body); break; case "Quests": BuildQuests(body); break; case "Chat": BuildChat(body); break; case "Settings": BuildSettings(body); break;
            }
        }
        RectTransform Dim(Transform parent,string name,Action close,float alpha)
        {
            var dim=UiKit.Rect(parent,name); UiKit.Stretch(dim); var im=dim.gameObject.AddComponent<Image>(); im.color=new Color(.03f,.025f,.015f,alpha); dim.gameObject.AddComponent<Button>().onClick.AddListener(()=>close()); return dim;
        }
        RectTransform Window(RectTransform dim,string title,bool full,Action close,bool bottomNavigation=false)
        {
            var panel=UiKit.Box(dim,"Panel: "+title,UiKit.Paper); panel.GetComponent<LayoutElement>().ignoreLayout=true;
            float w=Mathf.Min(610,safe.rect.width-44),h=Mathf.Min(940,Mathf.Max(200,safe.rect.height-215));
            if(full) UiKit.Stretch(panel); else Anchor(panel,new Vector2(.5f,.5f),new Vector2(0,-5),new Vector2(w,h));
            var block=panel.gameObject.AddComponent<Button>(); block.transition=Selectable.Transition.None; // Stop the dim handler receiving panel clicks.
            var inner=UiKit.Rect(panel,"Safe panel contents");
            if(full) { inner.anchorMin=safe.anchorMin; inner.anchorMax=safe.anchorMax; inner.offsetMin=inner.offsetMax=Vector2.zero; }
            else UiKit.Stretch(inner);
            if(full&&title=="뽑기 결과") { var ribbon=UiKit.Box(inner,"Golden result banner",UiKit.Yellow); Anchor(ribbon,new Vector2(.5f,1),new Vector2(0,-43),new Vector2(350,62)); ribbon.GetComponent<Image>().raycastTarget=false; }
            var titleText=UiKit.Text(inner,title,42,TextAnchor.MiddleCenter,62); titleText.rectTransform.anchorMin=new Vector2(0,1); titleText.rectTransform.anchorMax=Vector2.one; titleText.rectTransform.pivot=new Vector2(.5f,1); titleText.rectTransform.offsetMin=new Vector2(56,-76); titleText.rectTransform.offsetMax=new Vector2(-56,-8);
            var x=UiKit.Button(inner,"",close,Color.clear,46); x.name="Close "+title; x.GetComponent<Outline>().enabled=false; Anchor((RectTransform)x.transform,new Vector2(1,1),new Vector2(-35,-34),new Vector2(46,46)); var icon=UiKit.Icon(x.transform,"Close",40); UiKit.Stretch(icon.rectTransform,3,3,3,3);
            var viewport=UiKit.Rect(inner,"Viewport"); UiKit.Stretch(viewport,17,18,25,82); viewport.gameObject.AddComponent<RectMask2D>(); var bg=viewport.gameObject.AddComponent<Image>(); bg.color=Color.clear;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=38; scroll.viewport=viewport;
            var content=UiKit.Column(viewport,"Scrollable content",10,4); content.anchorMin=new Vector2(.5f,1); content.anchorMax=new Vector2(.5f,1); content.pivot=new Vector2(.5f,1); content.anchoredPosition=Vector2.zero; content.sizeDelta=new Vector2(full?Mathf.Min(760,safe.rect.width-42):w-42,0);
            scroll.content=content;
            var rail=UiKit.Rect(inner,"Scroll position"); rail.anchorMin=new Vector2(1,0);rail.anchorMax=Vector2.one;rail.offsetMin=new Vector2(-13,20);rail.offsetMax=new Vector2(-6,-84);
            var railImage=rail.gameObject.AddComponent<Image>();railImage.sprite=UiKit.Frame;railImage.type=Image.Type.Sliced;railImage.color=new Color(.83f,.83f,.8f);
            var handle=UiKit.Rect(rail,"Scroll thumb");UiKit.Stretch(handle);var handleImage=handle.gameObject.AddComponent<Image>();handleImage.sprite=UiKit.Frame;handleImage.type=Image.Type.Sliced;handleImage.color=UiKit.Green;
            var scrollbar=rail.gameObject.AddComponent<Scrollbar>();scrollbar.direction=Scrollbar.Direction.BottomToTop;scrollbar.handleRect=handle;scrollbar.targetGraphic=handleImage;scroll.verticalScrollbar=scrollbar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            var responsive=panel.gameObject.AddComponent<DoodleUiWindow>(); responsive.full=full;responsive.bottomNavigation=bottomNavigation; responsive.content=content; responsive.inner=inner; responsive.viewport=viewport;responsive.rail=rail;responsive.titleText=titleText;responsive.closeButton=(RectTransform)x.transform; SetWindowProfile(responsive,title);responsive.Reflow(safe);
            var motion=panel.gameObject.AddComponent<DoodlePopupMotion>();if(!rebuildingPage)motion.Open();
            return content;
        }
        static void SetWindowProfile(DoodleUiWindow window,string title)
        {
            // Bottom navigation panels share the user's final RectTransform specification.
            // Secondary/service windows retain their individual reference measurements.
            if(window.bottomNavigation) {
                window.maxWidth=679.9417f;window.maxHeight=1232.817f;
                if(title=="스탯")window.headerHeight=90;
                return;
            }
            switch(title) {
                case "뽑기 확률": case "뽑기 확률 안내": window.maxWidth=570;window.maxHeight=1110;window.centerFromTop=.52f;break;
                case "출석 보상": case "룰렛": window.maxWidth=576;window.maxHeight=865;window.centerFromTop=.49f;break;
                case "버프": window.maxWidth=558;window.maxHeight=860;window.centerFromTop=.49f;break;
                case "퀘스트": window.maxWidth=574;window.maxHeight=835;window.centerFromTop=.49f;break;
                case "설정": window.maxWidth=510;window.maxHeight=750;window.centerFromTop=.493f;break;
            }
        }
        public void ShowDetail(string title,Action<RectTransform> build)
        {
            StopRepeating();
            ConsumeGesture(); var dim=Dim(overlayLayer,"Detail dim: "+title,CloseDetail,.56f); overlayStack.Add(dim.gameObject); var body=Window(dim,title,false,CloseDetail); build(body); Relayout(true);
        }
        public void CloseDetail() { StopRepeating(); ConsumeGesture(); if(overlayStack.Count==0)return; var last=overlayStack[overlayStack.Count-1]; overlayStack.RemoveAt(overlayStack.Count-1); if(rewardCloseEffects.TryGetValue(last,out var effect)){rewardCloseEffects.Remove(last);effect();} DoodlePopupMotion.Close(last,root); RefreshHud(); }
        public void ShowFullscreen(string title,Action<RectTransform> build,bool animate=true)
        {
            StopRepeating();
            if(fullscreenBuilder!=null && overlayStack.Count>0) { var old=overlayStack[overlayStack.Count-1];overlayStack.RemoveAt(overlayStack.Count-1);old.SetActive(false);Destroy(old); }
            ConsumeGesture(); fullscreenBuilder=build; fullscreenTitle=title; var dim=Dim(overlayLayer,"Fullscreen: "+title,()=>{},1); overlayStack.Add(dim.gameObject); bool previous=rebuildingPage; rebuildingPage=!animate;try{build(Window(dim,title,true,CloseFullscreen));}finally{rebuildingPage=previous;} Relayout(true);
        }
        public void CloseFullscreen() { CloseDetail(); fullscreenBuilder=null; fullscreenTitle=null; RefreshPage(); }
        public void ShowRewards(string title,List<UiReward> rewards)
        {
            StopRepeating();
            ConsumeGesture(); var dim=Dim(overlayLayer,"Reward dim",CloseDetail,.77f); overlayStack.Add(dim.gameObject);
            var area=UiKit.Column(dim,"Floating rewards",30,4); Anchor(area,new Vector2(.5f,.5f),new Vector2(0,40),new Vector2(670,400));
            var reflow=area.gameObject.AddComponent<DoodleUiRewardLayout>(); reflow.safe=safe;reflow.Reflow();
            var text=UiKit.Text(area,title,64,TextAnchor.MiddleCenter,86); text.color=Color.white; text.gameObject.AddComponent<Outline>().effectColor=UiKit.Ink;
            var row=UiKit.Row(area,"Individual rewards",206,16);
            System.Action onClose = null;
            foreach(var reward in rewards) {
                var host=UiKit.Rect(row,"Reward "+reward.name); FixedWidth(host,Mathf.Min(158,630f/Mathf.Max(1,rewards.Count))); UiKit.Height(host,200);
                var halo=UiKit.Rect(host,"Golden hand drawn rays"); UiKit.Stretch(halo,-24,-24,-24,-24); var rays=halo.gameObject.AddComponent<DoodleRewardRays>(); rays.color=new Color(1,.84f,.21f,.92f); rays.raycastTarget=false;
                var card=UiKit.Box(host,"Reward frame",UiKit.Rarity(reward.rarity)); UiKit.Stretch(card,3,3,3,3); card.GetComponent<Image>().raycastTarget=false;
                var icon=UiKit.Icon(card,reward.icon,100); UiKit.Stretch(icon.rectTransform,13,57,13,18);
                string currency = reward.icon;
                if (reward.amount > 0) onClose += () => FlyReward(icon, currency);
                var count=UiKit.Text(card,UiNumber.Format(reward.amount),34,TextAnchor.MiddleCenter,44); UiKit.Stretch(count.rectTransform,5,9,5,144);
            }
            var hint=UiKit.Text(area,"화면을 터치하면 닫힙니다",28,TextAnchor.MiddleCenter,50); hint.color=Color.white;
            area.gameObject.AddComponent<DoodlePopupMotion>().Open();
            if (onClose != null) rewardCloseEffects[dim.gameObject] = onClose;
        }
        Text BuildToast(string name,int size,float offset,float height)
        {
            var panel=UiKit.Box(root,name+" frame",new Color(.24f,.24f,.24f,.9f));
            Anchor(panel,new Vector2(.5f,1),new Vector2(0,offset),new Vector2(620,height));
            panel.GetComponent<Image>().raycastTarget=false;
            var label=UiKit.Text(panel,"",size,TextAnchor.MiddleCenter,height-12);label.name=name;label.color=Color.white;
            UiKit.Stretch(label.rectTransform,14,6,14,6);
            panel.gameObject.AddComponent<DoodleToastMotion>().Hide();
            return label;
        }
        public void Toast(string message) {
            if(!toast)return;toast.text=message;toastUntil=Time.unscaledTime+3.5f;
            var motion=toast.GetComponentInParent<DoodleToastMotion>();
            if(string.IsNullOrEmpty(message))motion.Hide();else motion.Show(3.5f);
        }
        internal void StopSavingForReset() { suppressSaving=true; }
        public void Save() { if(suppressSaving)return; PlayerPrefs.SetString("DoodleUi.Gold",Gold.ToString()); PlayerPrefs.SetInt("DoodleUi.Diamonds",Diamonds); PlayerPrefs.SetInt("DoodleUi.CameraMode",CameraMode); SaveCollections(); SaveCommerce(); SaveServices(); SaveSkins(); PlayerPrefs.Save(); }
        public void NotifyPowerChanged(long before,string reason=null)
        {
            if(!powerToast)return;long after=Power,change=after-before;
            powerToast.text="전투력 "+UiNumber.Format(before)+" → "+UiNumber.Format(after)+"  ("+(change>0?"+":"")+UiNumber.Format(change)+")";
            if(!string.IsNullOrEmpty(reason))powerToast.text=reason+"\n"+powerToast.text;
            powerToast.color=change<0?UiKit.Red:change>0?UiKit.Green:UiKit.Paper;powerToastUntil=Time.unscaledTime+2.5f;powerToast.GetComponentInParent<DoodleToastMotion>().Show(2.5f);
        }
        void StopRepeating() { if(Canvas){var driver=Canvas.GetComponent<DoodleUiRepeatDriver>();if(driver)driver.Stop();} }
        void ApplyCameraMode() { if(game)game.SetUiCameraSize(new[]{8.5f,11f,14.5f}[CameraMode-1]);if(cameraLabel)cameraLabel.text="카메라  "+CameraMode; }
        public void CycleCameraMode() { CameraMode=CameraMode%3+1;ApplyCameraMode();Save(); }
        void OnApplicationPause(bool paused) { if(initialized&&paused)Save(); }
        void OnApplicationQuit() { if(initialized)Save(); }
        void ConsumeGesture() { consumeThroughFrame=Time.frameCount+2; releaseLatch=Pointer.current!=null&&Pointer.current.press.isPressed; game.CancelUiPointer(); }
        void ClearOverlays() { foreach(var go in overlayStack) if(go){rewardCloseEffects.Remove(go);go.SetActive(false);Destroy(go);} overlayStack.Clear(); }
        static void ClearChildren(Transform parent) { if(!parent)return; foreach(Transform child in parent) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
        void LateUpdate()
        {
            if(!initialized)return; if(releaseLatch&&(Pointer.current==null||!Pointer.current.press.isPressed))releaseLatch=false;
            Relayout(); TickServices();
            if(game.Kills<lastKills)lastKills=game.Kills;
            if(game.Kills>lastKills) { int earned=Mathf.Max(1,Mathf.RoundToInt((game.Kills-lastKills)*10*GoldGainMultiplier*GoldBuffMultiplier)); Gold=SaturatingAdd(Gold,earned); RecordServiceProgress("gold",earned); lastKills=game.Kills; }
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(HasOverlay)CloseDetail();else if(ActivePage!=null)ClosePage();}
            RefreshHud(); if(toast&&Time.unscaledTime>toastUntil)toast.text="";if(powerToast&&Time.unscaledTime>powerToastUntil)powerToast.text="";
            if(Time.unscaledTime>=nextWalletSave) { nextWalletSave=Time.unscaledTime+15; Save(); }
        }
        public void RefreshHud()
        {
            if(!initialized)return; profile.text=PlayerName+"\n전투력 "+UiNumber.Format(Power); walletGold.text=UiNumber.Format(Gold); walletDiamond.text=UiNumber.Format(Diamonds);
            buffGold.text=GoldBuffSeconds>0?Duration(GoldBuffSeconds):"비활성"; buffAttack.text=AttackBuffSeconds>0?Duration(AttackBuffSeconds):"비활성"; missionText.text=MainMissionText;
            missionClaim.interactable=CanClaimMainMission;cameraLabel.text="카메라  "+CameraMode;
            stageLabel.text=ActiveDungeonIndex>=0?DungeonMission:"스테이지 "+(MainStage+1).ToString()+"\n<"+game.CurrentThemeName+">\n"+(game.BossActive?"보스 1/1":UiNumber.Format(MainStageKillProgress)+"/"+UiNumber.Format(MainStageKillGoal));
            breakthroughButton.interactable=ActiveDungeonIndex<0;breakthroughButton.GetComponentInChildren<Text>().text=BreakthroughMode?"돌파 모드 ON":"돌파 모드 OFF";breakthroughButton.GetComponent<Image>().color=BreakthroughMode?UiKit.Green:Color.gray;
            goldBuffSurface.color=GoldBuffSeconds>0?UiKit.Green:Color.gray;attackBuffSurface.color=AttackBuffSeconds>0?UiKit.Green:Color.gray;
            var skills=EquippedSkills; for(int i=0;i<8;i++) { bool locked=i>=UnlockedSkillSlots; bool found=i<skills.Count; hudIcons[i].enabled=!locked; hudLocks[i].gameObject.SetActive(locked); hudIcons[i].sprite=UiKit.Art(found?skills[i].icon:"AddSlot"); hudIcons[i].color=found?Color.white:new Color(1,1,1,.65f); hudMasks[i].fillAmount=found?game.UiCooldown(skills[i].ability):0; }
        }
        static string Duration(double seconds) => ServiceClock((int)Math.Max(0,Math.Min(int.MaxValue,Math.Ceiling(seconds))));
        public float UiDamageMultiplier => CombatDamageMultiplier*AttackBuffMultiplier;
        public float UiSpeedMultiplier => CombatAttackSpeedMultiplier;
    }
    public sealed class DoodleUiWindow : MonoBehaviour
    {
        public bool full,bottomNavigation;
        public float maxWidth=570,maxHeight=880,centerFromTop=.5f,headerHeight=80;
        public float detailScale=1;
        public int titleSize=48;
        public RectTransform content,inner,footer,viewport,rail,closeButton;
        public Text titleText;
        public void Reflow(RectTransform safe)
        {
            var panel=(RectTransform)transform;
            float width=maxWidth;
            if(full) { UiKit.Stretch(panel); inner.anchorMin=safe.anchorMin;inner.anchorMax=safe.anchorMax;inner.offsetMin=inner.offsetMax=Vector2.zero; width=Mathf.Min(802,safe.rect.width); }
            else if(bottomNavigation) {
                panel.anchorMin=panel.anchorMax=panel.pivot=Vector2.one*.5f;
                panel.sizeDelta=new Vector2(width,maxHeight);
                panel.anchoredPosition=new Vector2(0,33.8761f);
            }
            else {
                float height=safe.rect.height;
                float scale=detailScale>1?Mathf.Min(detailScale,(safe.rect.width-32)/width,(height-48)/maxHeight):1;
                panel.anchorMin=panel.anchorMax=(safe.anchorMin+safe.anchorMax)*.5f;panel.pivot=Vector2.one*.5f;panel.sizeDelta=new Vector2(width,maxHeight)*scale;
                float bottom=detailScale>1?24:180,top=detailScale>1?24:108;
                panel.anchoredPosition=new Vector2(0,Mathf.Clamp(height*(.5f-centerFromTop),-height*.5f+bottom+maxHeight*scale*.5f,height*.5f-top-maxHeight*scale*.5f));
                if(detailScale>1) {
                    // Scale the entire reference layout together; popup motion owns the outer transform.
                    inner.anchorMin=inner.anchorMax=inner.pivot=Vector2.one*.5f;
                    inner.anchoredPosition=Vector2.zero;inner.sizeDelta=new Vector2(width,maxHeight);
                    inner.localScale=Vector3.one*scale;
                }
            }
            float heading=full?82:headerHeight;
            if(titleText) { titleText.resizeTextMaxSize=full?42:titleSize;titleText.rectTransform.offsetMin=new Vector2(50,-heading+4);titleText.rectTransform.offsetMax=new Vector2(-50,-8); }
            if(closeButton)closeButton.anchoredPosition=new Vector2(-33,-heading*.5f);
            if(viewport)viewport.offsetMax=new Vector2(viewport.offsetMax.x,-heading);
            if(rail)rail.offsetMax=new Vector2(rail.offsetMax.x,-heading-2);
            content.sizeDelta=new Vector2(width-42,content.sizeDelta.y);
            if(footer) footer.sizeDelta=new Vector2(width-34,footer.sizeDelta.y);
        }
    }
    public sealed class DoodleUiSquareRow : MonoBehaviour
    {
        void LateUpdate()=>Reflow();
        public void Reflow() { var r=(RectTransform)transform; var row=GetComponent<HorizontalLayoutGroup>(); float size=Mathf.Min(r.rect.height,(r.rect.width-row.spacing*7)/8); foreach(Transform c in transform) { var e=c.GetComponent<LayoutElement>(); if(e){e.minWidth=e.preferredWidth=size;e.flexibleWidth=0;e.minHeight=e.preferredHeight=size;} } }
    }
    public sealed class DoodleUiRewardLayout : MonoBehaviour
    {
        public RectTransform safe;
        void LateUpdate()=>Reflow();
        public void Reflow() { if(safe) { var rect=(RectTransform)transform;rect.anchorMin=rect.anchorMax=(safe.anchorMin+safe.anchorMax)*.5f; } }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DoodleRewardRays : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var rect=rectTransform.rect; Vector2 center=rect.center; float radius=Mathf.Min(rect.width,rect.height)*.5f;
            for(int i=0;i<16;i++) {
                float a=i*Mathf.PI/8; Vector2 dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),side=new Vector2(-dir.y,dir.x); float inner=radius*.62f,outer=radius*(i%3==0?1:.9f); int start=vh.currentVertCount;
                Add(vh,center+dir*inner-side*2,color); Add(vh,center+dir*outer-side*4,color); Add(vh,center+dir*outer+side*4,color); Add(vh,center+dir*inner+side*2,color); vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
            }
            for(int i=0;i<4;i++) {
                float a=(i+.35f)*Mathf.PI/2; Vector2 p=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*.93f; int start=vh.currentVertCount;
                Add(vh,p+Vector2.up*12,color); Add(vh,p+Vector2.right*5,color); Add(vh,p+Vector2.down*12,color); Add(vh,p+Vector2.left*5,color); vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
            }
            // Soft yellow radial backdrop is independent of the item frames.
            for(int i=0;i<32;i++) { int start=vh.currentVertCount; Add(vh,center,new Color(1,.8f,.12f,.5f)); float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16; Add(vh,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,new Color(1,.8f,.12f,0)); Add(vh,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,new Color(1,.8f,.12f,0)); vh.AddTriangle(start,start+1,start+2); }
        }
        static void Add(VertexHelper vh,Vector2 pos,Color c) { var v=UIVertex.simpleVert; v.position=pos; v.color=c; vh.AddVert(v); }
    }
}
