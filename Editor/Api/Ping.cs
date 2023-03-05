using System.Net.Http;
using FVPR.Toolbox;
using Newtonsoft.Json;

namespace FVPR
{
	public static partial class FvprApi
	{
		public static class Ping
		{
			public static bool GET(out ErrorResponse error)
			{
				var getResponse = PublicClient.GetAsync($"{Strings.ApiBase}/ping").Result;
				
				if (!getResponse.IsSuccessStatusCode)
				{
					// Parse the error
					error = JsonConvert.DeserializeObject<ErrorResponse>(getResponse.Content.ReadAsStringAsync().Result);
					return false;
				}
				
				error = null;
				return true;
			}
			
			public static bool HEAD() => PublicClient
				.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"{Strings.ApiBase}/ping"))
				.Result
				.IsSuccessStatusCode;
		}
	}
}