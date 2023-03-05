using System;
using Newtonsoft.Json;

namespace FVPR
{
	public class AuthDeviceResolveResponse
	{
		// {
		// 	"token": "42LUwo@2S4DK1Wm@+zSVDSr68JPsdO.R/he0L4in?PuRcM*se4jmcJ*ISkGrUs@V-sHyk9ZFgu1OO?FbSukDN8VZkA0/WjlYb224rpszca84XOP9oH0R+Y5NBXPupAd3",
		// 	"scopes": [
		// 		"identify"
		// 	],
		// 	"expiresAt": "2019-08-24T14:15:22Z"
		// }
		[JsonProperty("token")]			public string Token;
		[JsonProperty("scopes")]		public string[] Scopes;
		[JsonProperty("expiresAt")]		public string ExpiresAt;
		
		[JsonIgnore]					public DateTime ExpiresAtDateTime => DateTime.Parse(ExpiresAt);
	}
}