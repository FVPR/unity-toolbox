using System.Net.Http;
using FVPR.Toolbox;
using Newtonsoft.Json;

namespace FVPR
{
	public static partial class FvprApi
	{
		public static class Publish
		{
			public static bool POST(string ticketUid, byte[] payload, out ErrorResponse error)
			{
				var client = MakeTicketClient(ticketUid);
				var postResponse = client.PostAsync(
					$"{Strings.ApiBase}/publish",
					new ByteArrayContent(payload)
				).Result;
				
				if (!postResponse.IsSuccessStatusCode)
				{
					// Parse the error
					error = JsonConvert.DeserializeObject<ErrorResponse>(postResponse.Content.ReadAsStringAsync().Result);
					return false;
				}
				
				// Parse the response
				error = null;
				return true;
			}
		}
	}
}