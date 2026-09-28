#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using BackEnd;
using BackndChat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public sealed class DoodleChatTests
    {
        [TestCase(" hello ", true)]
        [TestCase("안녕하세요", true)]
        [TestCase("", false)]
        [TestCase("   ", false)]
        [TestCase("a\nb", false)]
        [TestCase("a\tb", false)]
        public void ValidateChatInput(string text, bool valid) => Assert.That(DoodleChatService.ValidateMessage(text, out _), Is.EqualTo(valid));

        [Test]
        public void HistoriesAreIsolatedDeduplicatedCappedAndModerated()
        {
            var go = new GameObject("Chat test");
            var chat = go.AddComponent<DoodleChatService>();
            try {
                var joined = (Dictionary<string, ChannelInfo>)typeof(DoodleChatService).GetField("joined", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(chat);
                foreach (string group in new[] { "global", "korea" }) joined[group] = new ChannelInfo { ChannelGroup = group, ChannelName = "server-1", ChannelNumber = 1 };
                var global = new MessageInfo { ChannelGroup = "global", ChannelName = "server-1", ChannelNumber = 1, Index = 1, Tag = "tag", GamerName = "<b>name</b>", Message = "<b>hello</b>" };
                chat.OnChatMessage(global); chat.OnChatMessage(global);
                Assert.That(chat.Messages.Count, Is.EqualTo(1));
                chat.OnChatMessage(new MessageInfo { ChannelGroup = "korea", ChannelName = "server-1", ChannelNumber = 1, Index = 1, Tag = "tag", GamerName = "이름", Message = "한국" });
                Assert.That(chat.Messages.Count, Is.EqualTo(1), "Korea messages must not appear in Global.");
                typeof(DoodleChatService).GetProperty("Selected").SetValue(chat, "korea");
                Assert.That(chat.Messages[0].Content, Is.EqualTo("한국"));
                typeof(DoodleChatService).GetProperty("Selected").SetValue(chat, "global");
                chat.OnHideMessage(global); Assert.That(chat.Messages.Count, Is.Zero);
                for (ulong i = 2; i < 112; i++) chat.OnChatMessage(new MessageInfo { ChannelGroup = "global", ChannelName = "server-1", ChannelNumber = 1, Index = i, GamerName = "test", Message = "message" });
                Assert.That(chat.Messages.Count, Is.EqualTo(DoodleChatService.HistoryLimit));
                chat.OnChatMessage(new MessageInfo { ChannelGroup = "global", ChannelName = "server-1", ChannelNumber = 99, Index = 112, Message = "Wrong shard" });
                Assert.That(chat.Messages[99].Index, Is.EqualTo(111));
                chat.Disconnect(); Assert.That(chat.Messages.Count, Is.Zero);
                Assert.That(DoodleChatService.ValidateMessage(new string('한', 140), out _), Is.True);
                Assert.That(DoodleChatService.ValidateMessage(new string('a', 141), out _), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [UnityTest]
        public IEnumerator LiveBackndCanJoinBothLanguageChannels()
        {
            if (Environment.GetEnvironmentVariable("DOODLE_BACKND_SMOKE") != "1") Assert.Ignore("Live BACKND test is opt-in.");
            var task = JoinBoth();
            double deadline = Time.realtimeSinceStartupAsDouble + 100;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Chat integration timed out.");
            if (task.IsFaulted) throw task.Exception.InnerException;
        }
        static async Task JoinBoth()
        {
            var settings = JsonUtility.FromJson<DoodleBackendSession.Settings>(Resources.Load<TextAsset>("DoodleIdle/BackendSettings").text);
            var init = await DoodleBackendSession.Request(cb => Backend.InitializeAsync(new BackendCustomSetting { clientAppID = settings.clientAppId, signatureKey = settings.signatureKey, useAsyncPoll = false, isSendLogReport = false, timeOutSec = 20 }, cb));
            Assert.That(init.IsSuccess(), Is.True, "initialize");
            string id = "qa_chat_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            var signup = await DoodleBackendSession.Request(cb => Backend.BMember.CustomSignUp(id, Guid.NewGuid().ToString("N") + "aA1!", cb));
            Assert.That(signup.IsSuccess(), Is.True, "QA signup");
            string account = Backend.UserInDate;
            var go = new GameObject("Live chat test");
            var chat = go.AddComponent<DoodleChatService>();
            try {
                var nick = await DoodleBackendSession.Request(cb => Backend.BMember.CreateNickname("ChatTest" + Guid.NewGuid().ToString("N").Substring(0, 8), cb));
                Assert.That(nick.IsSuccess(), Is.True, "nickname");
                typeof(DoodleChatService).GetMethod("Connect", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(chat, new object[] { settings.chatUuid });
                var joined = (Dictionary<string, ChannelInfo>)typeof(DoodleChatService).GetField("joined", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(chat);
                double deadline = Time.realtimeSinceStartupAsDouble + 40;
                while (joined.Count < 2 && Time.realtimeSinceStartupAsDouble < deadline) await Task.Delay(100);
                Assert.That(joined.Keys, Is.EquivalentTo(new[] { "global", "korea" }), chat.Status);
                Assert.That(joined["global"].ChannelNumber, Is.GreaterThan(0));
                Assert.That(joined["korea"].ChannelNumber, Is.GreaterThan(0));
                Assert.That(chat.CanSend, Is.True);
                // Do not broadcast QA messages into public user channels.
                chat.Disconnect(); Assert.That(chat.CanSend, Is.False);
            }
            finally {
                chat.Disconnect(); UnityEngine.Object.Destroy(go);
                if (Backend.IsLogin && Backend.UserInDate == account) {
                    var deleted = await DoodleBackendSession.Request(cb => Backend.BMember.WithdrawAccount(0, cb));
                    Assert.That(deleted.IsSuccess(), Is.True, "delete temporary QA account");
                }
            }
        }
    }
}
#endif
