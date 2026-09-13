namespace ProxyPlayer
{
    public static class Constants
    {
        public const string PluginName = "ProxyPlayer";
        public const int MaxTitleLengthDtr = 14;
        public const string UnsupportedWineBuild = """
            The current Wine/Proton build you are running is not supported by ProxyPlayer.

            Linux Media Controls require a modern Wine/Proton runner with AF_UNIX support. Update your Wine/Proton version to a more recent build to use ProxyPlayer."
            """;
    }
}
