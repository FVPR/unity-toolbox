using System;
using System.Net.Http;
using FVPR.Toolbox;
using Newtonsoft.Json;

namespace FVPR
{
	public static partial class FvprApi
	{
		public static partial class Auth
		{
			public static class Ticket
			{
				public static class Open
				{
					public static bool POST(
						string token,
						TicketType type,
						out TicketResponse response,
						out ErrorResponse error,
						params (string, string)[] parameters
					)
					{
						var paramsString = "";
						foreach (var param in parameters)
							paramsString += $"&{Uri.EscapeUriString(param.Item1)}={Uri.EscapeUriString(param.Item2)}";
						var client = MakeBearerClient(token);
						var postResponse = client.PostAsync(
							$"{Strings.ApiBase}/auth/ticket/open?op={type.ToApiString()}" + paramsString,
							null
						).Result;
						
						if (!postResponse.IsSuccessStatusCode)
						{
							// Parse the error
							error = JsonConvert.DeserializeObject<ErrorResponse>(postResponse.Content.ReadAsStringAsync().Result);
							response = null;
							return false;
						}
						
						// Parse the response
						response = JsonConvert.DeserializeObject<TicketResponse>(postResponse.Content.ReadAsStringAsync().Result);
						error = null;
						return true;
					}
				}
				
				public static class Status
				{
					public static bool GET(string ticketUid, out TicketResponse response, out ErrorResponse error)
					{
						var client = MakeTicketClient(ticketUid);
						var getResponse = client.GetAsync(
							$"{Strings.ApiBase}/auth/ticket/status"
						).Result;
						
						if (!getResponse.IsSuccessStatusCode)
						{
							// Parse the error
							error = JsonConvert.DeserializeObject<ErrorResponse>(getResponse.Content.ReadAsStringAsync().Result);
							response = null;
							return false;
						}
						
						// Parse the response
						response = JsonConvert.DeserializeObject<TicketResponse>(getResponse.Content.ReadAsStringAsync().Result);
						error = null;
						return true;
					}
				}
			}
		}
	}
}