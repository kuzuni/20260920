using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BACKND.Database;
using BackEnd;

namespace DoodleIdle
{
    public sealed partial class DoodleBackendSession
    {
        Client pvpDatabase;
        string pvpAccount;
        Task<Client> pvpConnecting;
        const string PvpPendingKey="Doodle.Pvp.Pending.v1";
        public bool PvpConfigured => Ready && !string.IsNullOrEmpty(Config.pvpDatabaseUuid) && !string.IsNullOrEmpty(Config.pvpLeaderboardUuid);
        async Task<Client> ConnectPvp()
        {
            if(!PvpConfigured || DoodleSecurity.Compromised || !Backend.IsLogin || Backend.UserInDate!=AccountId)
                throw new InvalidOperationException("PVP 서버 연결을 확인해 주세요.");
            if(pvpDatabase!=null && pvpAccount==AccountId)return pvpDatabase;
            if(pvpConnecting!=null && !pvpConnecting.IsCompleted)return await pvpConnecting;
            pvpConnecting=ConnectPvpNow();return await pvpConnecting;
        }
        async Task<Client> ConnectPvpNow()
        {
            pvpDatabase?.Dispose();pvpDatabase=null;
            string account=AccountId;
            var client=new Client(Config.pvpDatabaseUuid);
            await client.Initialize();
            if(!Ready||AccountId!=account||string.IsNullOrEmpty(client.UserUUID)){client.Dispose();throw new InvalidOperationException("PVP 로그인이 만료되었습니다.");}
            pvpAccount=account;pvpDatabase=client;return client;
        }
        public async Task<DoodlePvpProfile> ReadMyPvp()
        {
            var db=await ConnectPvp();return await db.From<DoodlePvpProfile>().OfCurrentUser().FirstOrDefault();
        }
        public async Task<DoodlePvpProfile> ReadPvpOpponent(string account)
        {
            var db=await ConnectPvp();return await db.From<DoodlePvpProfile>().Where(x=>x.Account==account).FirstOrDefault();
        }
        public async Task<List<DoodlePvpListing>> PvpCandidates(int points)
        {
            var db=await ConnectPvp();string self=AccountId;
            var above=await db.From<DoodlePvpListing>().Where(x=>x.Score>=points && x.Account!=self).OrderBy(x=>x.Score).Take(5).ToList();
            var below=await db.From<DoodlePvpListing>().Where(x=>x.Score<points && x.Account!=self).OrderByDescending(x=>x.Score).Take(5).ToList();
            return above.Concat(below).OrderBy(x=>Math.Abs((long)x.Score-points)).ThenBy(x=>x.Account,StringComparer.Ordinal).Take(5).ToList();
        }
        public async Task<bool> PvpServerEmpty()
        {
            var db=await ConnectPvp();return await db.From<DoodlePvpListing>().FirstOrDefault()==null;
        }
        public sealed class PvpRank
        {
            public string account,name;
            public int rank,points;
            public DoodlePvpSummary summary;
        }
        public async Task<List<PvpRank>> PvpRanking()
        {
            var db=await ConnectPvp();
            var response=new TaskCompletionSource<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>();
            Backend.Leaderboard.User.GetLeaderboard(Config.pvpLeaderboardUuid,100,0,r=>response.TrySetResult(r));
            if(await Task.WhenAny(response.Task,Task.Delay(30000))!=response.Task)throw new TimeoutException();
            var result=await response.Task;
            if(!result.IsSuccess())throw new InvalidOperationException("PVP 순위를 불러오지 못했습니다.");
            var ranks=new List<PvpRank>();
            foreach(var entry in result.GetUserLeaderboardList()){
                int.TryParse(entry.rank.ToString(),out int rank);int.TryParse(entry.score.ToString(),out int points);
                ranks.Add(new PvpRank{account=entry.gamerInDate,name=entry.nickname,rank=rank,points=points});
            }
            // Fetch summaries for the exact ranked accounts in one request, including tied scores.
            if(ranks.Count>0){
                var accounts=ranks.Select(x=>x.account).ToList();
                var summaries=await db.From<DoodlePvpListing>().Where(x=>accounts.Contains(x.Account)).Take(100).ToList();
                var byAccount=summaries.ToDictionary(x=>x.Account);
                foreach(var rank in ranks)if(byAccount.TryGetValue(rank.account,out var row))rank.summary=row.Summary;
            }
            return ranks;
        }
        public async Task<int> PvpOwnRank()
        {
            await ConnectPvp();
            var response=new TaskCompletionSource<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>();
            Backend.Leaderboard.User.GetMyLeaderboard(Config.pvpLeaderboardUuid,r=>response.TrySetResult(r));
            if(await Task.WhenAny(response.Task,Task.Delay(30000))!=response.Task)throw new TimeoutException();
            var result=await response.Task;
            if(!result.IsSuccess()){
                if(result.GetStatusCode()=="404")return 0;
                throw new InvalidOperationException("내 순위를 불러오지 못했습니다.");
            }
            var row=result.GetUserLeaderboardList().FirstOrDefault(x=>x.gamerInDate==AccountId);
            return row!=null && int.TryParse(row.rank,out var rank)?rank:0;
        }
        public DoodlePvpPending PendingPvp()
        {
            string text=DoodlePrefs.GetString(PvpPendingKey,"");
            if(string.IsNullOrEmpty(text))return null;
            var value=DoodleJson.FromJson<DoodlePvpPending>(text);
            return value!=null && value.account==AccountId ? value : null;
        }
        public void JournalPvp(DoodlePvpPending pending)
        {
            if(pending==null||pending.account!=AccountId)throw new InvalidOperationException("PVP 계정이 변경되었습니다.");
            DoodlePrefs.SetString(PvpPendingKey,DoodleJson.ToJson(pending));DoodlePrefs.Flush();
        }
        public async Task FinishPvp(DoodlePvpPending pending,DoodleUi ui)
        {
            var db=await ConnectPvp();string account=AccountId;
            if(pending==null||pending.account!=account)throw new InvalidOperationException("PVP 계정이 변경되었습니다.");
            var existing=await db.From<DoodlePvpProfile>().OfCurrentUser().FirstOrDefault();
            if(existing?.Summary?.lastMatch!=pending.matchId){
                if((existing?.Summary?.lastMatch??"")!=(pending.previousMatch??""))
                    throw new InvalidOperationException("다른 기기의 PVP 기록이 변경되었습니다.");
                pending.summary.lastMatch=pending.matchId;
                pending.summary.lastOutcome=pending.finished&&pending.draw?"Draw":pending.finished&&pending.won?"Win":"Loss";
                pending.summary.lastDelta=pending.FinalScore-pending.startScore;
                var row=new DoodlePvpProfile{Account=account,Score=pending.FinalScore,Summary=pending.summary,Payload=pending.payload};
                if(existing==null)await db.From<DoodlePvpProfile>().Insert(row);
                else {
                    var update=await db.From<DoodlePvpProfile>().OfCurrentUser().Where(x=>x.Account==account && x.Score==pending.startScore).Update(row);
                    if(update.AffectedRows!=1)throw new InvalidOperationException("PVP 저장이 충돌했습니다. 다시 시도해 주세요.");
                }
            }
            // Repeating an acknowledged result writes the same score, never adds the delta again.
            var param=new Param();param.Add("score",pending.FinalScore);
            var published=await Request(cb=>Backend.Leaderboard.User.UpdateMyDataAndRefreshLeaderboard(Config.pvpLeaderboardUuid,"pvp_profile",account,param,r=>cb(r)));
            if(!published.IsSuccess())throw new InvalidOperationException("승점 순위 반영을 다시 시도해 주세요.");
            if(!Ready||AccountId!=account)throw new InvalidOperationException("PVP 계정이 변경되었습니다.");
            ui.ApplyPvpSaved(pending);
            if(!await SaveCloud())throw new InvalidOperationException("게임 정보 서버 저장을 다시 시도해 주세요.");
            DoodlePrefs.SetString(PvpPendingKey,"");DoodlePrefs.Flush();
            // Persist journal acknowledgement too; a restored older journal is still idempotent.
            await SaveCloud();
        }
    }
}
