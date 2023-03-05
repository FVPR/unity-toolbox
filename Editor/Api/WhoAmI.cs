using FVPR.Toolbox;
using Newtonsoft.Json;

namespace FVPR
{
	public static partial class FvprApi
	{
		public static class WhoAmI
		{
			public static bool GET(string token, out WhoAmIResponse response, out ErrorResponse error)
			{
				var client = MakeBearerClient(token);
				var getResponse = client.GetAsync(
					$"{Strings.ApiBase}/whoami"
				).Result;
				
				if (!getResponse.IsSuccessStatusCode)
				{
					// Parse the error
					error = JsonConvert.DeserializeObject<ErrorResponse>(getResponse.Content.ReadAsStringAsync().Result);
					response = null;
					return false;
				}
				
				// Parse the response
				response = JsonConvert.DeserializeObject<WhoAmIResponse>(getResponse.Content.ReadAsStringAsync().Result);
				error = null;
				return true;
			}
		}
	}
}