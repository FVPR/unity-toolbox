namespace FVPR.Toolbox
{
	public static class Strings
	{
#if FVPR_DEV 
		public const string Domain = "dev.vpm.foxscore.de";
		public const string TokenPref = "fvpr::dev::token";
#else
		public const string Domain = "vpm.foxscore.de";
		public const string TokenPref = "fvpr::token";
#endif
		public const string ApiBase = "https://" + Domain + "/api/v1";
		public const string AuthenticatorUrl = "https://authenticator.foxscore.de";
	}
}