using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BackEnd;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        sealed class RankingRow
        {
            public string account,name,rank,value;
            public DoodlePlayerLook look;
        }
        sealed class RankingPage
        {
            public string account,status;
            public double time;
            public readonly List<RankingRow> rows=new List<RankingRow>();
            public RankingRow mine;
        }
        readonly RankingPage[] rankingPages=new RankingPage[3];
        public void OpenRankings(int initialTab=0)
        {
            ShowDetail(L("랭킹","Rankings"),body=>{
                var window=body.GetComponentInParent<DoodleUiWindow>();
                window.maxWidth=640;window.maxHeight=1080;
                var tabs=UiKit.Row(window.inner,"Ranking tabs",54,8);
                window.fixedHeader=tabs;window.fixedHeaderHeight=54;
                var status=UiKit.Text(body,"",21,TextAnchor.MiddleCenter,42);status.name="Ranking status";
                var rows=UiKit.Column(body,"Ranking rows",7,0);
                var footer=UiKit.Footer(body,"Ranking footer",156);
                var mine=UiKit.Column(footer,"My ranking row",0,0);
                Button refresh=null;int selected=-1,request=0;
                var buttons=new Button[3];
                async void Load(bool force){
                    int token=++request,tab=selected;refresh.interactable=false;
                    ClearRankingRows(rows);ClearRankingRows(mine);status.text=L("불러오는 중…","Loading…");
                    try{
                        var session=DoodleBackendSession.Instance;
                        if(!session || !session.Ready)throw new InvalidOperationException(L("로그인 후 랭킹을 확인해 주세요.","Sign in to view rankings."));
                        var page=rankingPages[tab];
                        if(force || page==null || page.account!=session.AccountId || Time.unscaledTimeAsDouble-page.time>=30)
                            page=await ReadRankingPage(session,tab);
                        if(!body || !body.gameObject.activeInHierarchy || token!=request)return;
                        rankingPages[tab]=page;status.text=page.status;
                        if(page.rows.Count==0)UiKit.Text(rows,L("아직 등록된 기록이 없어요.","No scores yet."),22,TextAnchor.MiddleCenter,70);
                        foreach(var entry in page.rows)DrawRankingRow(rows,entry,entry.account==session.AccountId);
                        DrawRankingRow(mine,page.mine,true);
                        Relayout(true);body.GetComponentInParent<ScrollRect>().verticalNormalizedPosition=1;
                    }catch(Exception error){if(status && token==request)status.text=error is InvalidOperationException?error.Message:L("랭킹 연결에 실패했어요. 새로고침해 주세요.","Could not load rankings. Please refresh.");}
                    finally{if(refresh && token==request)refresh.interactable=true;}
                }
                void Select(int tab){
                    if(selected==tab)return;selected=tab;
                    for(int i=0;i<buttons.Length;i++)buttons[i].GetComponent<Image>().color=i==tab?UiKit.Yellow:UiKit.Paper;
                    Load(false);
                }
                string[] labels={L("스테이지","Stage"),"PVP",L("전투력","Power")};
                for(int i=0;i<3;i++){int tab=i;buttons[i]=UiKit.Button(tabs,labels[i],()=>Select(tab),UiKit.Paper,54);buttons[i].name="Ranking tab "+i;}
                refresh=UiKit.Button(footer,L("새로고침","Refresh"),()=>Load(true),UiKit.Blue,54);
                Select(Mathf.Clamp(initialTab,0,2));
            });
        }
        static void ClearRankingRows(Transform parent){foreach(Transform child in parent){child.gameObject.SetActive(false);Destroy(child.gameObject);}}
        void DrawRankingRow(Transform parent,RankingRow entry,bool self)
        {
            var card=UiKit.Box(parent,"Ranking entry "+entry.rank,self?UiKit.Yellow:UiKit.Paper,88);
            var row=UiKit.Row(card,"Rank contents",80,8);UiKit.Stretch(row,8,4,8,4);
            var portrait=UiKit.Icon(row,"Player",76);FixedWidth(portrait.transform,76);
            portrait.gameObject.AddComponent<DoodleRankingPortrait>().Configure(this,entry.look);
            var label=UiKit.Text(row,(self?L("나 · ","Me · "):"")+entry.rank+L("위"," place")+"\n"+entry.name,23,TextAnchor.MiddleLeft,76);label.supportRichText=false;
            var score=UiKit.Text(row,entry.value,22,TextAnchor.MiddleRight,76);score.supportRichText=false;UiKit.Flexible(score.transform,.8f);
        }
        async Task<RankingPage> ReadRankingPage(DoodleBackendSession session,int tab)
        {
            var page=new RankingPage{account=session.AccountId,mine=new RankingRow{account=session.AccountId,name=PlayerName,rank="—",look=DoodlePlayerLook.From(this)}};
            if(tab==1){
                if(!session.PvpConfigured)throw new InvalidOperationException("PVP 서버 설정을 확인해 주세요.");
                var ranks=await session.PvpRanking();var own=await session.ReadMyPvp();int myRank=own!=null?await session.PvpOwnRank():0;
                foreach(var rank in ranks)page.rows.Add(new RankingRow{account=rank.account,name=rank.summary?.name??rank.name,rank=rank.rank.ToString(),value=rank.points+L("점"," pts"),look=rank.summary?.Look??new DoodlePlayerLook()});
                page.mine.rank=myRank>0?myRank.ToString():"—";page.mine.value=(own?.Score??0)+L("점"," pts");page.mine.look=own?.Summary?.Look??page.mine.look;
                page.status=L("누적 승점 · 상위 100명","PVP points · Top 100");
            }else{
                string uuid=tab==0?session.Config.stageLeaderboardUuid:session.Config.powerLeaderboardUuid;
                if(string.IsNullOrEmpty(uuid))throw new InvalidOperationException(L("랭킹 서버 설정을 확인해 주세요.","Leaderboard configuration is missing."));
                session.SetStage(HighestMainStage);bool published=tab==0?await session.PublishStage():await session.PublishPower();
                var response=await RequestRanking(uuid,false);
                foreach(var rank in response.GetUserLeaderboardList()){
                    var look=DoodleRankMetadata.Decode(rank.extraData,out var power);
                    page.rows.Add(new RankingRow{account=rank.gamerInDate,name=rank.nickname,rank=rank.rank.ToString(),look=look,value=tab==0?rank.score+L(" 단계"," stage"):UiNumber.Format(power)});
                }
                if(tab==0){
                    var looks=await session.RankLooks(page.rows.Select(x=>x.account).ToList());
                    foreach(var row in page.rows)if(looks.TryGetValue(row.account,out var look))row.look=look;
                }
                var own=await RequestRanking(uuid,true);var ownRank=own?.GetUserLeaderboardList().FirstOrDefault(x=>x.gamerInDate==session.AccountId);
                if(ownRank!=null){page.mine.rank=ownRank.rank.ToString();var look=DoodleRankMetadata.Decode(ownRank.extraData,out var power);if(tab==2)page.mine.look=look;page.mine.value=tab==0?ownRank.score+L(" 단계"," stage"):UiNumber.Format(power);}
                else page.mine.value=tab==0?HighestMainStage+L(" 단계"," stage"):UiNumber.Format(PowerAmount);
                page.status=tab==0?L("최고 클리어 스테이지 · 상위 100명","Highest stage cleared · Top 100"):L("전투력 · 상위 100명","Combat power · Top 100");
                if(!published)page.status=L("기록 업로드 지연 · ","Upload pending · ")+page.status;
            }
            page.time=Time.unscaledTimeAsDouble;return page;
        }
        static async Task<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject> RequestRanking(string uuid,bool mine)
        {
            var completion=new TaskCompletionSource<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>();
            if(mine)Backend.Leaderboard.User.GetMyLeaderboard(uuid,r=>completion.TrySetResult(r));
            else Backend.Leaderboard.User.GetLeaderboard(uuid,100,0,r=>completion.TrySetResult(r));
            if(await Task.WhenAny(completion.Task,Task.Delay(30000))!=completion.Task)throw new TimeoutException();
            var result=await completion.Task;if(mine && result.GetStatusCode()=="404")return null;
            if(!result.IsSuccess())throw new Exception("Leaderboard request failed");return result;
        }
    }
}
