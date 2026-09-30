using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using BACKND.Database;

namespace DoodleIdle
{
    [Serializable] public sealed class DoodlePvpSummary
    {
        public string name, power, day, lastMatch, lastOutcome;
        public int used,lastDelta;
        public string lookData;
        public static string EncodeLook(DoodlePlayerLook value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(DoodleJson.ToJson(value)));
        [Newtonsoft.Json.JsonIgnore] public DoodlePlayerLook Look => string.IsNullOrEmpty(lookData)?new DoodlePlayerLook():DoodleJson.FromJson<DoodlePlayerLook>(Encoding.UTF8.GetString(Convert.FromBase64String(lookData)));
    }

    [Serializable] public sealed class DoodlePvpPayload
    {
        public string compressed;
        public static DoodlePvpPayload Pack(DoodlePvpLoadout value)
        {
            var bytes=Encoding.UTF8.GetBytes(DoodleJson.ToJson(value));
            if(bytes.Length>256000)throw new InvalidDataException("PVP 전투 정보가 너무 큽니다.");
            using var output=new MemoryStream();
            using(var gzip=new GZipStream(output,CompressionLevel.Optimal,true))gzip.Write(bytes,0,bytes.Length);
            var encoded=Convert.ToBase64String(output.ToArray());
            if(encoded.Length>11000)throw new InvalidDataException("PVP 전투 정보 저장 한도를 초과했습니다.");
            return new DoodlePvpPayload{compressed=encoded};
        }
        public DoodlePvpLoadout Unpack()
        {
            if(string.IsNullOrEmpty(compressed)||compressed.Length>11000)throw new InvalidDataException("PVP 전투 정보가 없습니다.");
            using var input=new MemoryStream(Convert.FromBase64String(compressed));
            using var gzip=new GZipStream(input,CompressionMode.Decompress);
            using var output=new MemoryStream();
            var buffer=new byte[4096];int count;
            while((count=gzip.Read(buffer,0,buffer.Length))>0){
                if(output.Length+count>256000)throw new InvalidDataException("PVP 전투 정보가 너무 큽니다.");
                output.Write(buffer,0,count);
            }
            var value=DoodleJson.FromJson<DoodlePvpLoadout>(Encoding.UTF8.GetString(output.ToArray()));
            if(value==null||value.version!=1||string.IsNullOrEmpty(value.collections)||float.IsNaN(value.attackBuff)||float.IsInfinity(value.attackBuff))
                throw new InvalidDataException("지원하지 않는 PVP 전투 정보입니다.");
            return value;
        }
    }

    [Table("pvp_profile",TableType.UserTable,ClientAccess=true,
        ReadPermissions=new[]{TablePermission.SELF,TablePermission.OTHERS},WritePermissions=new[]{TablePermission.SELF})]
    public sealed class DoodlePvpProfile : BaseModel
    {
        [PrimaryKey,Column("account",NotNull=true)] public string Account {get;set;}
        [Column("score",NotNull=true)] public int Score {get;set;}
        [Column("summary")] public DoodlePvpSummary Summary {get;set;}
        [Column("payload")] public DoodlePvpPayload Payload {get;set;}
    }

    // A projection of the same table: candidate lists never download combat payloads.
    [Table("pvp_profile",TableType.UserTable,ClientAccess=true,
        ReadPermissions=new[]{TablePermission.SELF,TablePermission.OTHERS},WritePermissions=new[]{TablePermission.SELF})]
    public sealed class DoodlePvpListing : BaseModel
    {
        [PrimaryKey,Column("account",NotNull=true)] public string Account {get;set;}
        [Column("score",NotNull=true)] public int Score {get;set;}
        [Column("summary")] public DoodlePvpSummary Summary {get;set;}
    }

    [Serializable] public sealed class DoodlePvpPending
    {
        public string account,matchId,previousMatch,opponentName;
        public int startScore,opponentScore,delta;
        public bool finished,won,draw;
        public DoodlePvpSummary summary;
        public DoodlePvpPayload payload;
        public int FinalScore => checked(startScore+(finished?(draw?0:delta):DoodlePvpRules.Delta(startScore,opponentScore,false)));
    }

    public enum DoodlePvpOutcome { Loss, Win, Draw }

    public static class DoodlePvpRules
    {
        public const float TimeLimitSeconds=30;
        public static DoodlePvpOutcome Outcome(GameNumber health,GameNumber maxHealth,GameNumber opponentHealth,GameNumber opponentMaxHealth)
        {
            if(health<=0 || opponentHealth<=0)
                return health<=0 && opponentHealth<=0 ? DoodlePvpOutcome.Draw : health>0 ? DoodlePvpOutcome.Win : DoodlePvpOutcome.Loss;
            // Compare fractions by cross multiplication, including different maximum HP.
            var own=health*GameNumber.Max(1,opponentMaxHealth);
            var other=opponentHealth*GameNumber.Max(1,maxHealth);
            return own==other ? DoodlePvpOutcome.Draw : own>other ? DoodlePvpOutcome.Win : DoodlePvpOutcome.Loss;
        }
        public static int Delta(int own,int opponent,bool won)
        {
            double expected=1/(1+Math.Pow(10,Math.Clamp(((double)opponent-own)/400,-10,10)));
            int magnitude=Math.Clamp((int)Math.Round(6*(won?1-expected:expected)),1,5);
            return won?magnitude:-magnitude;
        }
    }
}
