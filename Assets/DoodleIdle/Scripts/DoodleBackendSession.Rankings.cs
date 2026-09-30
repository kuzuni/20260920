using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BACKND.Database;

namespace DoodleIdle
{
    [Table("rank_profile",TableType.UserTable,ClientAccess=true,
        ReadPermissions=new[]{TablePermission.SELF,TablePermission.OTHERS},WritePermissions=new[]{TablePermission.SELF})]
    public sealed class DoodleRankProfile : BaseModel
    {
        [PrimaryKey,Column("account",NotNull=true)] public string Account {get;set;}
        [Column("metadata")] public string Metadata {get;set;}
    }
    public sealed partial class DoodleBackendSession
    {
        async Task PublishRankLook(string metadata)
        {
            var db=await ConnectPvp();string account=AccountId;
            var row=new DoodleRankProfile{Account=account,Metadata=metadata};
            var existing=await db.From<DoodleRankProfile>().OfCurrentUser().FirstOrDefault();
            if(existing==null)await db.From<DoodleRankProfile>().Insert(row);
            else await db.From<DoodleRankProfile>().OfCurrentUser().Where(x=>x.Account==account).Update(row);
        }
        public async Task<Dictionary<string,DoodlePlayerLook>> RankLooks(List<string> accounts)
        {
            if(accounts.Count==0)return new Dictionary<string,DoodlePlayerLook>();
            var db=await ConnectPvp();
            var rows=await db.From<DoodleRankProfile>().Where(x=>accounts.Contains(x.Account)).Take(100).ToList();
            return rows.ToDictionary(x=>x.Account,x=>DoodleRankMetadata.Decode(x.Metadata,out _));
        }
    }
}
