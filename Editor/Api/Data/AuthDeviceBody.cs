using System;
using System.Net.Http;
using Newtonsoft.Json;

namespace FVPR
{
	[Serializable]
	public class AuthDeviceBody
	{
		[JsonProperty("scopes")] string[] Scopes { get; set; }
			
		public static string Serialize(params string[] scopes) => JsonConvert.SerializeObject(new AuthDeviceBody { Scopes = scopes });
		public static HttpContent SerializeContent(params string[] scopes) => new StringContent(Serialize(scopes));
	}
}