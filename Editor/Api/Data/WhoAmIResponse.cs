using Newtonsoft.Json;

namespace FVPR
{
	public class WhoAmIResponse
	{
		[JsonProperty("discord")]	public ulong DiscordId;
		[JsonProperty("name")]		public string Username;
		[JsonProperty("roles")]		public string[] Roles;
		[JsonProperty("domains")]	public string[] Domains;
		[JsonProperty("scopes")]	public string[] Scopes;
	}
}