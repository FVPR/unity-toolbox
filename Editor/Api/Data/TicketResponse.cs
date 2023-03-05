using Newtonsoft.Json;

namespace FVPR
{
	public class TicketResponse
	{
		[JsonProperty("type")]			public string Type;
		[JsonProperty("uid")]			public string Uid;
		[JsonProperty("status")]		public string Status;
		[JsonProperty("expiresAt")]		public string ExpiresAt;
		[JsonProperty("title")]			public string Title;
		[JsonProperty("description")]	public string Description;
	}
}