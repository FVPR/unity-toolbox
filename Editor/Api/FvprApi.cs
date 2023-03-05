using System.Net.Http;

namespace FVPR
{
	public static partial class FvprApi
	{
		private static readonly HttpClient PublicClient = new HttpClient();
		
		private static HttpClient MakeBearerClient(string token) => new HttpClient
		{
			DefaultRequestHeaders =
			{
				Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token)
			}
		};
		private static HttpClient MakeTicketClient(string ticket) => new HttpClient
		{
			DefaultRequestHeaders =
			{
				Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Ticket", ticket)
			}
		};

		public static partial class Auth { }
	}
}