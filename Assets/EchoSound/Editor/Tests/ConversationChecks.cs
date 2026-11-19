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
            string mood = Say(dialogue, "Jak się czujesz?");
            string reason = Say(dialogue, "Dlaczego?");
            Require(mood != reason && reason.Contains("Razem"), "A follow-up explains the previous topic instead of repeating it.");
            string joke = Say(dialogue, "Możesz wymyślić nowy żart?");
            string explanation = Say(dialogue, "Wyjaśnij dowcip.");
            Require(joke.Contains("duch") && explanation.Contains("Przejrzysty"), "Explain the actual joke that was told.");
            Require(Say(dialogue, "Jeszcze jeden!") != joke, "Another joke must be different.");
            dialogue.NoteCommand("stój");
            Require(Say(dialogue, "Czemu?").Contains("polecenie"), "A command replaces the old conversational reason.");
            Require(!dialogue.TryReply("O co chodzi w żarcie?", out _), "An old joke cannot explain a newer action.");
            foreach (string text in new[] { "Opowiedz żart i otwórz drzwi", "Powiedział: opowiedz żart", "„Opowiedz żart”", "Jak się nazywa twój pies?",
                "Tell me a joke and open the door", "\"Tell me a joke\"", "What's your dog's name?" })
                Require(!dialogue.TryReply(text, out _), "Do not swallow unrelated speech or compound orders: " + text);
            Say(dialogue, "Mam na imię Marek, a mój kot to Plusz.");
            Require(Say(dialogue, "Jak ma na imię mój kot?") == "Twój kot ma na imię Plusz.", "A combined introduction retains both names.");
            dialogue.Remember("Przypadkowa uwaga.", "Twój kot ma na imię Bob.");
            Require(Say(dialogue, "Jak ma na imię mój kot?").Contains("Plusz"), "Generated prose cannot change player facts.");
            for (int i = 0; i < 8; i++) dialogue.Remember("Earlier user " + i, "Earlier reply " + i);
            Require(dialogue.RecentMessages().Count <= 8, "Keep a bounded conversation window.");
            dialogue.Reset();
            Require(!dialogue.TryReply("Dlaczego?", out _), "Reset clears follow-up context.");
            Require(Say(dialogue, "How do you feel?").Contains("Alert"), "English social dialogue.");
            Require(Say(dialogue, "Tell me another joke.").Contains("Ghosts"), "English authored joke.");
            Require(Say(dialogue, "Explain that joke.").Contains("Spirits"), "English contextual explanation.");
            dialogue.Reset();
            Require(Say(dialogue, "Opowiedz mi krotki zart.").Contains("duch"), "Polish typed without diacritics.");
            Require(Say(dialogue, "Wyjasnij dowcip.").Contains("Przejrzysty"), "Polish contextual explanation without diacritics.");
            dialogue.Reset();
            Say(dialogue, "Mam na imię Łucja.");
            Require(Say(dialogue, "What's my name?") == "Your name is Łucja.", "A name keeps its Polish letters.");
            Say(dialogue, "My name is Sam.");
            Say(dialogue, "Actually, my name is Mark.");
            Require(Say(dialogue, "What should you call me now?") == "Your name is Mark.", "A correction replaces the player's name.");
            Say(dialogue, "I have a cat called Biscuit.");
            Require(Say(dialogue, "What's my cat's name?") == "Your cat is called Biscuit.", "English cat name.");
            Require(Say(dialogue, "Jak masz na imię?") == "Mów mi Echo." && Say(dialogue, "What's your name?") == "Call me Echo.",
                "The reply follows the language of the question.");

            JObject polish = CompanionConversationClient.CreateRequest("Zaproponuj temat do rozmowy.", null, null);
            Require(polish["response_format"] == null && polish["tools"] == null && (bool)polish["stream"], "Conversation produces streamed text without action schema.");
            Require(((string)polish["messages"][0]["content"]).Contains("Reply in Polish"), "Polish conversation is answered in Polish.");
            Require(((string)CompanionConversationClient.CreateRequest("Dzieki, jestes super", null, null)["messages"][0]["content"]).Contains("Reply in Polish"),
                "Polish typed without diacritics is answered in Polish.");
            Require(((string)CompanionConversationClient.CreateRequest("Suggest a topic for us to talk about.", null, null)["messages"][0]["content"]).Contains("Reply in English"),
                "English conversation is answered in English.");
            Require(!((string)polish["messages"][0]["content"]).Contains("Classify"), "Planner instructions never enter a conversation.");
            Require((double)polish["temperature"] > 0 && (int)polish["max_tokens"] == 48, "Chat has its own sampling and output budget.");
            Require(FastIntentClient.CreateRequest("Trzymaj się blisko mnie")["response_format"] != null, "Command planning remains constrained.");
            foreach (string invalid in new[] { "", "[\"stop\"]", "{\"actions\":[\"open\"]}", "<think>Hidden</think>" })
            {
                bool rejected = false;
                try { CompanionConversationClient.ReadReply(invalid); } catch (JsonException) { rejected = true; }
                Require(rejected, "Chat cannot turn an action plan or hidden reasoning into a reply.");
            }

            using (var stream = new CompanionConversationClient.ReplyStream(1))
            {
                byte[] data = Encoding.UTF8.GetBytes(Chunk("Cześć. Drugie zdanie nie jest potrzebne.") + "\n");
                foreach (byte value in data) stream.Feed(new[] { value }, 1);
                Require(stream.Complete && stream.Text == "Cześć.", "UTF-8/SSE boundaries and sentence completion survive one-byte chunks.");
            }
            using (var stream = new CompanionConversationClient.ReplyStream(2))
            {
                Feed(stream, "data: {\"choices\":[{\"delta\":{\"reasoning_content\":\"secret\"}}]}\n\n");
                Feed(stream, "data:{\"choices\":[]}\n\n");
                Feed(stream, Chunk("Naprawdę?! Tak. Więcej słów.") + "\n");
                Require(stream.Complete && stream.Text == "Naprawdę?! Tak.", "Ignore reasoning and count consecutive punctuation as one sentence.");
            }
            using (var stream = new CompanionConversationClient.ReplyStream(1))
            {
                Feed(stream, Chunk("Powiedział: „Cześć.") + "\n");
                Feed(stream, Chunk("” Inny tekst.") + "\n");
                Require(stream.Complete && stream.Text == "Powiedział: „Cześć.”", "Keep a closing quote even when it arrives in the next token.");
            }
            using (var stream = new CompanionConversationClient.ReplyStream(1))
            {
                Feed(stream, Chunk("Niedokończone zdanie") + "\n");
                Feed(stream, "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"length\"}]}\n\n");
                Require(!stream.Complete && !stream.Stopped && stream.CompletedSentences == "", "A truncated reply is not a completed answer.");
                Feed(stream, "data: invalid json\n\n");
                Require(stream.Error != null, "Malformed SSE fails cleanly.");
            }
            Debug.Log("Conversation checks: all passed (context, authored replies, reply language, reply-only protocol and incremental UTF-8/SSE).");
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
