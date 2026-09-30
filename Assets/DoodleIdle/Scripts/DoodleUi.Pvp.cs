using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        bool pvpBusy;
        int pvpMyRank;
        double pvpRankTime=-100;
        List<LocalRank> pvpRanks;
        void BuildPvp(RectTransform body)
        {
            var session=DoodleBackendSession.Instance;
            if(!session || !session.PvpConfigured){
                UiKit.Text(body,"로그인 후 PVP 서버에 연결해 주세요.",24,TextAnchor.MiddleCenter,90);
                return;
            }
            if(session.PendingPvp()!=null && !game.PvpSessionActive){
                UiKit.Text(body,"이전 경기 결과를 서버에 저장해야 합니다.",24,TextAnchor.MiddleCenter,80);
                UiKit.Button(body,"결과 저장 다시 시도",()=>RetryPvpSave(session.PendingPvp()),UiKit.Yellow,65).interactable=!pvpBusy;
                return;
            }
            if(pvpRanks!=null && Time.unscaledTimeAsDouble-pvpRankTime<30){DrawPvpRanking(body,pvpRanks);return;}
            var status=UiKit.Text(body,"PVP 순위를 불러오는 중…",23,TextAnchor.MiddleCenter,70);
            _=LoadPvpPage(body,status,session);
        }
        async Task LoadPvpPage(RectTransform body,Text status,DoodleBackendSession session)
        {
            try {
                var mine=await session.ReadMyPvp();
                var ranking=await session.PvpRanking();
                int ownRank=mine!=null?await session.PvpOwnRank():0;
                if(!body || ActivePage!="Pvp")return;
                ResetServicePeriods();services.pvpPoints=mine?.Score??0;
                if(mine?.Summary?.day==DateTime.UtcNow.ToString("yyyy-MM-dd"))services.pvpUsed=Math.Max(services.pvpUsed,mine.Summary.used);
                pvpRanks=new List<LocalRank>();pvpMyRank=ownRank;
                foreach(var rank in ranking){
                    bool self=rank.account==session.AccountId;if(self)pvpMyRank=rank.rank;
                    GameNumber.TryParse(rank.summary?.power,out var power);
                    pvpRanks.Add(new LocalRank{rank=rank.rank,name=rank.summary?.name??rank.name,art="Player",look=rank.summary?.Look??new DoodlePlayerLook(),points=rank.points,power=power,self=self});
                }
                pvpRankTime=Time.unscaledTimeAsDouble;
                RefreshPage();
            }catch(Exception){
                if(status)status.text="PVP 서버 연결에 실패했어요. 다시 시도해 주세요.";
                if(body)UiKit.Button(body,"다시 시도",RefreshPage,UiKit.Blue,62);
            }
        }
        public async void OpenPvpChallenge()
        {
            if(pvpBusy || game.PvpSessionActive)return;
            var session=DoodleBackendSession.Instance;
            if(!session || !session.PvpConfigured){Toast("PVP 서버에 연결해 주세요.");return;}
            if(session.PendingPvp()!=null){RetryPvpSave(session.PendingPvp());return;}
            ResetServicePeriods();if(services.pvpUsed>=serviceTuning.pvpAttempts){Toast("오늘의 도전 횟수를 모두 사용했어요.");return;}
            if(services.activeDungeon>=0){Toast("던전을 마친 뒤 도전해 주세요.");return;}
            pvpBusy=true;
            RectTransform panel=null;Text status=null;
            ShowDetail("도전 상대",body=>{panel=body;status=UiKit.Text(body,"상대를 찾는 중…",23,TextAnchor.MiddleCenter,70);});
            try {
                var own=await session.ReadMyPvp();
                var candidates=await session.PvpCandidates(own?.Score??0);
                bool bootstrap=candidates.Count==0 && own==null && await session.PvpServerEmpty();
                if(!panel)return;
                status.text=bootstrap?"첫 PVP · 연습 상대를 선택해 주세요.":"상대 한 명을 선택해 도전하세요.";
                if(bootstrap){
                    for(int i=0;i<5;i++){
                        var dummy=CreatePvpDummy(i);
                        PvpCandidateButton(panel,dummy.playerName,own?.Score??0,0,PowerAmount.ToString(),()=>BeginPvpChallenge(null,dummy,own?.Score??0));
                    }
                }else {
                    foreach(var candidate in candidates){
                        var row=candidate;
                        var button=PvpCandidateButton(panel,row.Summary?.name??"플레이어",own?.Score??0,row.Score,row.Summary?.power,()=>BeginPvpChallenge(row,null,own?.Score??0));
                        button.name="PvpOpponent:"+row.Account;
                    }
                    for(int i=candidates.Count;i<5;i++)UiKit.Button(panel,"대전 상대 등록 대기 중",()=>{},UiKit.Paper,126).interactable=false;
                }
                Relayout(true);
            }catch(Exception){if(status)status.text="상대 조회에 실패했어요. 다시 도전해 주세요.";}
            finally{pvpBusy=false;}
        }
        Button PvpCandidateButton(Transform panel,string name,int ownPoints,int points,string power,Action challenge)
        {
            string shownPower=GameNumber.TryParse(power,out var value)?UiNumber.Format(value):"—";
            int win=DoodlePvpRules.Delta(ownPoints,points,true),loss=DoodlePvpRules.Delta(ownPoints,points,false);
            var button=UiKit.Button(panel,name+"\n승점 "+points+" · 전투력 "+shownPower+"\n승리 +"+win+"점 · 패배 "+loss+"점",challenge,UiKit.Blue,126);
            var label=button.GetComponentInChildren<Text>();label.fontSize=23;label.supportRichText=false;
            return button;
        }
        DoodlePvpLoadout CreatePvpDummy(int index)
        {
            var snapshot=CapturePvpLoadout();
            snapshot.playerName="연습 상대 "+(index+1);snapshot.attackBuff=1;
            // A genuine combat loadout, not an invented leaderboard entry.
            return snapshot;
        }
        async void BeginPvpChallenge(DoodlePvpListing candidate,DoodlePvpLoadout dummy,int quotedOwnPoints)
        {
            if(pvpBusy||game.PvpSessionActive)return;pvpBusy=true;
            var session=DoodleBackendSession.Instance;
            try {
                if(session.PendingPvp()!=null)throw new InvalidOperationException("이전 결과 저장이 필요합니다.");
                var own=await session.ReadMyPvp();
                var opponentRow=dummy==null?await session.ReadPvpOpponent(candidate.Account):null;
                if((own?.Score??0)!=quotedOwnPoints || candidate!=null && opponentRow!=null && opponentRow.Score!=candidate.Score)
                    throw new InvalidOperationException("승점이 변경됐어요. 상대 목록을 다시 열어 확인해 주세요.");
                var opponent=dummy??opponentRow?.Payload?.Unpack();
                if(opponent==null)throw new InvalidOperationException("상대 전투 정보가 없습니다.");
                ResetServicePeriods();string day=DateTime.UtcNow.ToString("yyyy-MM-dd");
                int used=Math.Max(services.pvpUsed,own?.Summary?.day==day?own.Summary.used:0);
                if(used>=serviceTuning.pvpAttempts)throw new InvalidOperationException("오늘 도전 횟수를 모두 사용했습니다.");
                var snapshot=CapturePvpLoadout();
                var pending=new DoodlePvpPending{account=session.AccountId,matchId=Guid.NewGuid().ToString("N"),previousMatch=own?.Summary?.lastMatch,
                    startScore=own?.Score??0,opponentScore=opponentRow?.Score??0,opponentName=opponent.playerName,payload=DoodlePvpPayload.Pack(snapshot),
                    summary=new DoodlePvpSummary{name=PlayerName,power=PowerAmount.ToString(),lookData=DoodlePvpSummary.EncodeLook(DoodlePlayerLook.From(this)),day=day,used=used+1}};
                session.JournalPvp(pending);services.pvpUsed=used+1;Save();
                CloseDetail();ClosePage();
                StartCoroutine(PlayPvpAndSave(snapshot,opponent,pending));
            }catch(Exception error){pvpBusy=false;Toast(error is InvalidOperationException?error.Message:"PVP 준비에 실패했어요. 다시 시도해 주세요.");}
        }
        IEnumerator PlayPvpAndSave(DoodlePvpLoadout own,DoodlePvpLoadout opponent,DoodlePvpPending pending)
        {
            bool completed=false;
            var battle=game.RunPvpBattle(own,opponent,outcome=>{pending.won=outcome==DoodlePvpOutcome.Win;pending.draw=outcome==DoodlePvpOutcome.Draw;completed=true;});
            // Catch engine initialization failures without stranding the main field in a paused state.
            while(true){
                bool next;try{next=battle.MoveNext();}catch(Exception error){Debug.LogException(error);break;}
                if(!next)break;yield return battle.Current;
            }
            (battle as IDisposable)?.Dispose();
            pending.finished=true;pending.won=completed && pending.won;pending.draw=completed && pending.draw;
            pending.delta=pending.draw?0:DoodlePvpRules.Delta(pending.startScore,pending.opponentScore,pending.won);
            DoodleBackendSession.Instance.JournalPvp(pending);
            pvpBusy=false;RetryPvpSave(pending);
        }
        async void RetryPvpSave(DoodlePvpPending pending)
        {
            if(pvpBusy||pending==null)return;pvpBusy=true;
            Text status=null;Button retry=null;
            ShowDetail(pending.finished&&pending.draw?"PVP 무승부":pending.finished&&pending.won?"PVP 승리":"PVP 패배",body=>{
                int delta=pending.FinalScore-pending.startScore;
                UiKit.Text(body,pending.opponentName+"\n승점 "+(delta>0?"+":"")+delta,29,TextAnchor.MiddleCenter,100);
                status=UiKit.Text(body,"결과와 게임 정보를 서버에 저장 중…",21,TextAnchor.MiddleCenter,75);
                retry=UiKit.Button(body,"저장 다시 시도",()=>{CloseDetail();RetryPvpSave(pending);},UiKit.Yellow,60);retry.gameObject.SetActive(false);
            });
            try {
                await DoodleBackendSession.Instance.FinishPvp(pending,this);
                pvpRanks=null;pvpRankTime=-100;
                if(status)status.text="서버 저장 완료 · 현재 승점 "+pending.FinalScore;
            }catch(Exception){if(status)status.text="저장이 지연됐어요. 결과는 보관되어 있으며\n재시도해도 승점이 중복 지급되지 않습니다.";if(retry)retry.gameObject.SetActive(true);}
            finally{pvpBusy=false;}
        }
        public void ApplyPvpSaved(DoodlePvpPending pending)
        {
            InitServices();services.pvpPoints=pending.FinalScore;
            if(services.day==pending.summary.day)services.pvpUsed=Math.Max(services.pvpUsed,pending.summary.used);
            if(services.lastAppliedPvpMatch!=pending.matchId){services.lastAppliedPvpMatch=pending.matchId;RecordServiceProgress("pvp",1);}
            Save();
        }
    }
}
