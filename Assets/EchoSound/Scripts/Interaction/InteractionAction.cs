namespace EchoSound.Interaction
{
    /// <summary>
    /// Everything a player or the companion can do to an object. Add a verb here, for example Pull,
    /// and the companion gets a matching function (pull_lever) for every object that supports it.
    /// </summary>
    public enum InteractionAction
    {
        Open,
        Close
    }

    public static class InteractionActions
    {
        /// <summary>Lower-case verb used in function names and messages, for example "open".</summary>
        public static string Verb(this InteractionAction action) => action.ToString().ToLowerInvariant();
    }
}
