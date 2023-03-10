namespace FVPR.Toolbox
{
	public static class Strings
	{
#if FVPR_DEV 
		public const string Domain = "dev.vpm.foxscore.de";
		public const string TokenPref = "fvpr::dev::token";
		public const string ApiBase = "https://" + Domain + "/api/v1";
		public const string AuthenticatorUrl = "https://authenticator.foxscore.de";
#else
		public const string Domain = "fvpr.dev";
		public const string TokenPref = "fvpr::token";
		public const string ApiBase = "https://api." + Domain;
		public const string AuthenticatorUrl = "https://auth + Domain";
#endif
	}
}