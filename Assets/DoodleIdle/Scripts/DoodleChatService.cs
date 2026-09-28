using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BackEnd;
using BackndChat;
using UnityEngine;

namespace DoodleIdle
{
    /// <summary>One authenticated connection, with isolated Global and Korea histories.</summary>
    public sealed class DoodleChatService : MonoBehaviour, IChatClientListener
    {
        public const string Global = "global", Korea = "korea", ChannelName = "server-1";
        public const int MaxCharacters = 140, MaxBytes = 420, HistoryLimit = 100;
        public sealed class Entry
        {
            public string Key, Author, Content, Tag;
            public ulong Index;
            public bool Self;
        }
        readonly Dictionary<string, ChannelInfo> joined = new Dictionary<string, ChannelInfo>();
        readonly Dictionary<string, List<Entry>> history = new Dictionary<string, List<Entry>> {
            { Global, new List<Entry>() }, { Korea, new List<Entry>() }
        };
        readonly HashSet<string> blocked = new HashSet<string>(StringComparer.Ordinal);
        ChatClient client;
        string account, nickname, notice;
        bool stopped, koreaRequested, disposePending;
        double nextSend, banUntil;
        public string Selected { get; private set; } = Global;
        public int Revision { get; private set; }
        public bool CanSend => client != null && !stopped && !DoodleSecurity.Compromised &&
            Backend.IsLogin && Backend.UserInDate == account && joined.ContainsKey(Selected) && Time.realtimeSinceStartupAsDouble >= banUntil;
        public IReadOnlyList<Entry> Messages => history[Selected];
        public string Status => !string.IsNullOrEmpty(notice) ? notice : CanSend
            ? DoodleLanguage.Text("연결됨", "Connected")
            : DoodleLanguage.Text("채팅 서버 연결 중…", "Connecting to chat…");
        public static DoodleChatService Get()
        {
            var session = DoodleBackendSession.Get();
            return session.GetComponent<DoodleChatService>() ?? session.gameObject.AddComponent<DoodleChatService>();
        }
        static string T(string ko, string en) => DoodleLanguage.Text(ko, en);
        public void Select(string group)
        {
            if (!history.ContainsKey(group)) throw new ArgumentException("Unknown chat group", nameof(group));
            Selected = group; notice = null; Revision++;
            EnsureConnected();
        }
        public void EnsureConnected()
        {
            var session = DoodleBackendSession.Instance;
            if (!session || !session.Ready || !Backend.IsLogin) { notice = T("로그인 후 채팅을 이용할 수 있어요.", "Sign in to use chat."); return; }
            if (client != null || stopped || DoodleSecurity.Compromised) return;
            Connect(session.Config.chatUuid);
        }
        void Connect(string uuid)
        {
            if (!Backend.IsLogin || string.IsNullOrEmpty(uuid) || string.IsNullOrEmpty(Backend.UserNickName))
            { notice = T("채팅 설정과 닉네임을 확인해 주세요.", "Check chat settings and your nickname."); return; }
            account = Backend.UserInDate; nickname = Backend.UserNickName; notice = null;
            try { client = new ChatClient(this, new ChatClientArguments { UUID = uuid, Avatar = "Player", Metadata = new Dictionary<string, string>() }); }
            catch (Exception) { notice = T("채팅에 연결하지 못했어요. 다시 시도해 주세요.", "Could not connect. Please retry."); }
        }
        void Update()
        {
            if (client == null) return;
            if (disposePending || DoodleSecurity.Compromised || !Backend.IsLogin || Backend.UserInDate != account) { Disconnect(); return; }
            try { client.Update(); }
            catch (Exception) { Disconnect(); notice = T("채팅 연결을 다시 시도해 주세요.", "Please reconnect to chat."); }
        }
        public void Retry()
        {
            if (stopped) return;
            Disconnect(); EnsureConnected();
        }
        public static bool ValidateMessage(string input, out string text)
        {
            text = (input ?? "").Trim();
            if (text.Length == 0 || text.Length > MaxCharacters || Encoding.UTF8.GetByteCount(text) > MaxBytes) return false;
            return !text.Any(char.IsControl);
        }
        public bool Send(string input)
        {
            if (!CanSend) { notice = T("채널에 연결된 뒤 보낼 수 있어요.", "Wait until the channel is connected."); return false; }
            if (!ValidateMessage(input, out string text)) { notice = T("1~140자로 입력해 주세요. 줄바꿈은 사용할 수 없어요.", "Use 1–140 characters without line breaks."); return false; }
            if (Time.realtimeSinceStartupAsDouble < nextSend) { notice = T("잠시 후 보내 주세요.", "Please wait before sending again."); return false; }
            var channel = joined[Selected];
            try {
                client.SendChatMessage(channel.ChannelGroup, channel.ChannelName, channel.ChannelNumber, text);
                nextSend = Time.realtimeSinceStartupAsDouble + 2; notice = null;
                // Only the server echo becomes a visible message.
                return true;
            }
            catch (Exception) { notice = T("전송하지 못했어요. 다시 시도해 주세요.", "Message could not be sent. Please retry."); return false; }
        }
        public void Disconnect()
        {
            var previous = client; client = null; joined.Clear(); koreaRequested = false; disposePending = false;
            try { previous?.Dispose(); } catch (Exception) { /* Shutdown must not prevent logout. */ }
            foreach (var list in history.Values) list.Clear();
            blocked.Clear(); account = nickname = null; Revision++;
        }
        void OnDestroy() => Disconnect();
        void OnApplicationQuit() => Disconnect();
        public void OnJoinChannel(ChannelInfo channel)
        {
            if (client == null || !history.ContainsKey(channel.ChannelGroup) || channel.ChannelName != ChannelName) return;
            joined[channel.ChannelGroup] = channel;
            foreach (var player in client.GetBlockGamers()) blocked.Add(player.GamerName);
            if (channel.Messages != null) foreach (var message in channel.Messages) OnChatMessage(message);
            if (channel.ChannelGroup == Global && !koreaRequested)
            {
                koreaRequested = true;
                client.UpdateLanguage(DoodleLanguage.Korean ? "ko" : "en");
                client.SendJoinOpenChannel(Korea, ChannelName);
            }
            notice = null; Revision++;
        }
        public void OnLeaveChannel(ChannelInfo channel)
        {
            if (joined.TryGetValue(channel.ChannelGroup, out var current) && current.ChannelNumber == channel.ChannelNumber)
                joined.Remove(channel.ChannelGroup);
            if (channel.ChannelGroup == Korea || channel.ChannelGroup == Global) koreaRequested = false;
            Revision++;
        }
        static string Key(MessageInfo message) => message.ChannelGroup + ":" + message.ChannelName + ":" + message.ChannelNumber + ":" + message.Index + ":" + message.Tag;
        public void OnChatMessage(MessageInfo message)
        {
            if (!history.TryGetValue(message.ChannelGroup, out var list) || message.ChannelName != ChannelName || blocked.Contains(message.GamerName)) return;
            if (!joined.TryGetValue(message.ChannelGroup, out var channel) || channel.ChannelNumber != message.ChannelNumber) return;
            string key = Key(message);
            if (list.Any(entry => entry.Key == key)) return;
            list.Add(new Entry { Key = key, Author = message.GamerName, Content = message.Message, Self = message.GamerName == nickname, Index = message.Index, Tag = message.Tag });
            if (list.Count > HistoryLimit) list.RemoveRange(0, list.Count - HistoryLimit);
            Revision++;
        }
        void Remove(MessageInfo message)
        {
            if (history.TryGetValue(message.ChannelGroup, out var list) && list.RemoveAll(entry => entry.Key == Key(message)) > 0) Revision++;
        }
        public void OnHideMessage(MessageInfo message) => Remove(message);
        public void OnDeleteMessage(MessageInfo message) => Remove(message);
        public string[] BlockedPlayers => blocked.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        public void Block(string name) { if (client != null && name != nickname) client.SendAddBlockGamer(name); }
        public void Unblock(string name) { client?.SendRemoveBlockGamer(name); }
        public Dictionary<string, string> ReportReasons => client?.GetReportReasons() ?? new Dictionary<string, string>();
        public void Report(Entry entry, string reason)
        {
            if (client == null || entry.Self || !ReportReasons.ContainsKey(reason)) return;
            client.SendReportChatMessage(entry.Index, entry.Tag, reason, ReportReasons[reason]);
        }
        public void OnSuccess(SUCCESS_MESSAGE success, object param)
        {
            if (success == SUCCESS_MESSAGE.ADD_BLOCK_PLAYER || success == SUCCESS_MESSAGE.REMOVE_BLOCK_PLAYER)
            {
                blocked.Clear(); foreach (var player in client.GetBlockGamers()) blocked.Add(player.GamerName);
                foreach (var list in history.Values) list.RemoveAll(entry => blocked.Contains(entry.Author));
                Revision++;
            }
            if (success == SUCCESS_MESSAGE.REPORT) notice = T("신고가 접수됐어요.", "Report submitted.");
        }
        public void OnError(ERROR_MESSAGE error, object param)
        {
            switch (error)
            {
                case ERROR_MESSAGE.MESSAGE_SPAM: notice = T("메시지를 너무 자주 보냈어요. 잠시 기다려 주세요.", "Too many messages. Please wait."); break;
                case ERROR_MESSAGE.MESSAGE_FILTERED: notice = T("보낼 수 없는 표현이 포함돼 있어요.", "This message was filtered."); break;
                case ERROR_MESSAGE.CHAT_BAN:
                    var ban = param as ErrorMessageChatBanParam;
                    banUntil = Time.realtimeSinceStartupAsDouble + (ban != null ? ban.RemainSeconds : 60);
                    notice = T("채팅 이용이 제한된 계정이에요.", "Chat is restricted for this account."); break;
                case ERROR_MESSAGE.DUPLICATE_CONNECTION:
                    stopped = true; disposePending = true;
                    notice = T("다른 기기에서 접속했어요. 다시 로그인해 주세요.", "Connected on another device. Sign in again."); break;
                case ERROR_MESSAGE.NOT_AUTHENTICATION:
                    stopped = true; disposePending = true; notice = T("다시 로그인해 주세요.", "Please sign in again."); break;
                default: notice = T("채팅 요청을 완료하지 못했어요. 잠시 후 다시 시도해 주세요.", "Chat request failed. Please try again shortly."); break;
            }
            Revision++;
        }
        public void OnJoinChannelPlayer(string group, string name, ulong number, PlayerInfo player) { }
        public void OnLeaveChannelPlayer(string group, string name, ulong number, PlayerInfo player) { }
        public void OnUpdatePlayerInfo(string group, string name, ulong number, PlayerInfo player) { }
        public void OnChangeGamerName(string oldName, string newName)
        {
            if (nickname == oldName) nickname = newName;
            foreach (var list in history.Values) foreach (var entry in list) if (entry.Author == oldName) entry.Author = newName;
            Revision++;
        }
        public void OnWhisperMessage(WhisperMessageInfo message) { }
        public void OnTranslateMessage(List<MessageInfo> messages) { }
    }
}
