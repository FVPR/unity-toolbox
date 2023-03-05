using System.Net.Http;
using FVPR.Toolbox;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FVPR
{
	public static partial class FvprApi
	{
		public static partial class Auth
		{
			public static class Revoke
			{
				/// <summary>
				/// Attempts to revoke the given token.
				/// </summary>
				/// <param name="token">The token to revoke.</param>
				/// <returns>True if the request was successful, false otherwise.</returns>
				public static bool POST(string token, out ErrorResponse error)
				{
					var client = MakeBearerClient(token);
					var postResponse = client.PostAsync(
						$"{Strings.ApiBase}/auth/revoke",
						null
					).Result;
					
					if (!postResponse.IsSuccessStatusCode)
					{
						// Parse the error
						error = JsonConvert.DeserializeObject<ErrorResponse>(postResponse.Content.ReadAsStringAsync().Result);
						return false;
					}
					
					error = null;
					return true;
				}
			}

			public static class RevokeAll
			{
				public static bool POST(string token, out string response, out ErrorResponse error)
				{
					var client = MakeBearerClient(token);
					var postResponse = client.PostAsync(
						$"{Strings.ApiBase}/auth/revoke-all",
						null
					).Result;
					
					if (!postResponse.IsSuccessStatusCode)
					{
						// Parse the error
						error = JsonConvert.DeserializeObject<ErrorResponse>(postResponse.Content.ReadAsStringAsync().Result);
						response = null;
						return false;
					}
					
					var raw = postResponse.Content.ReadAsStringAsync().Result;
					var json = JObject.Parse(raw);
					response = json["message"].ToString();
					error = null;
					return true;
				}
			}
		}
	}
}