using System;
using System.Text;
using EchoSound.Companion;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EchoSound.Editor.Tests
{
    public static class ConversationChecks
    {
        public static void Validate()
        {
            var dialogue = new CompanionDialogue();
            string mood = Say(dialogue, "Ну как твои дела?");
            string reason = Say(dialogue, "А почему?");
            Require(mood != reason && reason.Contains("Вдвоём"), "A follow-up explains the previous topic instead of repeating it.");
            string joke = Say(dialogue, "Можешь придумать новую шутку пожалуйста?");
            string explanation = Say(dialogue, "Объясни прикол.");
            Require(joke.Contains("Призрак") && explanation.Contains("настроение"), "Explain the actual joke that was told.");
            Require(Say(dialogue, "Ещё одну!") != joke, "Another joke must be different.");
            dialogue.NoteCommand("стой");
            Require(Say(dialogue, "Почему?").Contains("команду"), "A command replaces the old conversational reason.");
            Require(!dialogue.TryReply("В чём шутка?", out _), "An old joke cannot explain a newer action.");
            foreach (string text in new[] { "Расскажи шутку и открой дверь", "Он сказал: расскажи шутку", "«Расскажи шутку»", "Как зовут твою собаку?" })
                Require(!dialogue.TryReply(text, out _), "Do not swallow unrelated speech or compound orders: " + text);
            Say(dialogue, "Меня зовут Марк, а кота зовут Плюш.");
            Require(Say(dialogue, "Как зовут моего кота?") == "Твоего кота зовут Плюш.", "A combined introduction retains both names.");
            dialogue.Remember("Случайная реплика.", "Твоего кота зовут Боб.");
            Require(Say(dialogue, "Как зовут моего кота?").Contains("Плюш"), "Generated prose cannot change player facts.");
            for (int i = 0; i < 8; i++) dialogue.Remember("Earlier user " + i, "Earlier reply " + i);
            Require(dialogue.RecentMessages().Count <= 8, "Keep a bounded conversation window.");
            dialogue.Reset();
            Require(!dialogue.TryReply("Почему?", out _), "Reset clears follow-up context.");
            Require(Say(dialogue, "How do you feel?").Contains("Alert"), "English social dialogue.");
            Require(Say(dialogue, "Tell me another joke.").Contains("Ghosts"), "English authored joke.");
            Require(Say(dialogue, "Explain that joke.").Contains("Spirits"), "English contextual explanation.");
            dialogue.Reset();
            Require(Say(dialogue, "Opowiedz mi krótki żart.").Contains("duch"), "Polish authored joke.");
            Require(Say(dialogue, "Wyjaśnij dowcip.").Contains("Przejrzysty"), "Polish contextual explanation.");

            JObject request = CompanionConversationClient.CreateRequest("Посоветуй тему для разговора.", null, null);
            Require(request["response_format"] == null && request["tools"] == null && (bool)request["stream"], "Conversation produces streamed text without action schema.");
            Require(!((string)request["messages"][0]["content"]).Contains("Classify"), "Planner instructions never enter a conversation.");
            Require((double)request["temperature"] > 0 && (int)request["max_tokens"] == 48, "Chat has its own sampling and output budget.");
            Require(FastIntentClient.CreateRequest("Держись рядом со мной")["response_format"] != null, "Command planning remains constrained.");
            foreach (string invalid in new[] { "", "[\"stop\"]", "{\"actions\":[\"open\"]}", "<think>Hidden</think>" })
            {
                bool rejected = false;
                try { CompanionConversationClient.ReadReply(invalid); } catch (JsonException) { rejected = true; }
                Require(rejected, "Chat cannot turn an action plan or hidden reasoning into a reply.");
            }

            using (var stream = new CompanionConversationClient.ReplyStream(1))
            {
                byte[] data = Encoding.UTF8.GetBytes(Chunk("Привет. Второе предложение не нужно.") + "\n");
                foreach (byte value in data) stream.Feed(new[] { value }, 1);
                Require(stream.Complete && stream.Text == "Привет.", "UTF-8/SSE boundaries and sentence completion survive one-byte chunks.");
            }
            using (var stream = new CompanionConversationClient.ReplyStream(2))
            {
                Feed(stream, "data: {\"choices\":[{\"delta\":{\"reasoning_content\":\"secret\"}}]}\n\n");
                Feed(stream, "data:{\"choices\":[]}\n\n");
                Feed(stream, Chunk("Правда?! Да. Ещё слова.") + "\n");
                Require(stream.Complete && stream.Text == "Правда?! Да.", "Ignore reasoning and count consecutive punctuation as one sentence.");
            }
            using (var stream = new CompanionConversationClient.ReplyStream(1))
            {
                Feed(stream, Chunk("Он сказал: «Привет.") + "\n");
                Feed(stream, Chunk("» Другой текст.") + "\n");
                Require(stream.Complete && stream.Text == "Он сказал: «Привет.»", "Keep a closing quote even when it arrives in the next token.");
            }
            using (var stream = new CompanionConversationClient.ReplyStream(1))
            {
                Feed(stream, Chunk("Незавершённая фраза") + "\n");
                Feed(stream, "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"length\"}]}\n\n");
                Require(!stream.Complete && !stream.Stopped && stream.CompletedSentences == "", "A truncated reply is not a completed answer.");
                Feed(stream, "data: invalid json\n\n");
                Require(stream.Error != null, "Malformed SSE fails cleanly.");
            }
            Debug.Log("Conversation checks: all passed (context, authored replies, reply-only protocol and incremental UTF-8/SSE).");
        }

        private static string Say(CompanionDialogue dialogue, string text)
        {
            Require(dialogue.TryReply(text, out string reply), "Expected an authored response to " + text);
            dialogue.Remember(text, reply);
            return reply;
        }
        private static string Chunk(string text) => "data: " + new JObject { ["choices"] = new JArray(new JObject { ["delta"] = new JObject { ["content"] = text } }) }.ToString(Formatting.None) + "\n";
        private static void Feed(CompanionConversationClient.ReplyStream stream, string value)
        {
            byte[] data = Encoding.UTF8.GetBytes(value);
            stream.Feed(data, data.Length);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Conversation: " + message);
        }
    }
}
