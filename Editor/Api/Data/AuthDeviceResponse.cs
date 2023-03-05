using Newtonsoft.Json;

namespace FVPR
{
	public class AuthDeviceResponse
	{
		[JsonProperty("user_code")]			public string UserCode;
		[JsonProperty("device_code")]		public string DeviceCode;
		[JsonProperty("interval")]			public int Interval;
		[JsonProperty("expires_in")]		public int ExpiresIn;
		[JsonProperty("verification_uri")]	public string VerificationUri;
	}
}