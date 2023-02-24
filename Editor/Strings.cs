namespace FVPR.Toolbox
{
	public static class Strings
	{
#if FVPR_DEV 
		public const string Url = "https://dev.vpm.foxscore.de";
		public const string TokenPref = "fvpr::dev::token";
#else
		public const string Url = "https://vpm.foxscore.de";
		public const string TokenPref = "fvpr::token";
#endif
	}
}