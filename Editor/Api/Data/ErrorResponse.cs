using Newtonsoft.Json;

namespace FVPR
{
	public class ErrorResponse
	{
		[JsonProperty("error")]								public bool IsError;
		[JsonProperty("message")]							public string Message;
		[JsonProperty("code")]								public int Code;

		public override string ToString() => $"Error: {Message} ({Code})";
		public string ToString(string prefix) => $"{prefix}: {Message} ({Code})";
	}
}