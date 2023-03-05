using FVPR.Toolbox;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FVPR
{
	public static partial class FvprApi
	{
		public static partial class Auth
		{
			public class Device
			{
				/// <summary>
				/// Initiates a device authentication request.
				/// </summary>
				/// <param name="response">The json response from the server.</param>
				/// <param name="error">The error response from the server.</param>
				/// <returns>True if the request was successful, false otherwise.</returns>
				public static bool POST(out AuthDeviceResponse response, out ErrorResponse error)
				{
					var postResponse = PublicClient.PostAsync(
						$"{Strings.ApiBase}/auth/device",
						AuthDeviceBody.SerializeContent("identify", "ticket.publish")
					).Result;

					var raw = postResponse.Content.ReadAsStringAsync().Result;

					if (!postResponse.IsSuccessStatusCode)
					{
						// Parse the error
						error = JsonConvert.DeserializeObject<ErrorResponse>(raw);
						response = null;
						return false;
					}

					// Parse the response
					response = JsonConvert.DeserializeObject<AuthDeviceResponse>(raw);
					error = null;
					return true;
				}

				public static class Resolve
				{
					/// <summary>
					/// Attempts to resolve the given device code to an access token.
					/// </summary>
					/// <param name="deviceCode">The device code to resolve.</param>
					/// <param name="token">The access token.</param>
					/// <param name="error">The error response from the server.</param>
					/// <returns>True if the request was successful, false otherwise.</returns>
					public static bool GET(string deviceCode, out AuthDeviceResolveResponse response, out ErrorResponse error)
					{
						var postResponse = PublicClient.GetAsync(
							$"{Strings.ApiBase}/auth/device/resolve?code={deviceCode}"
						).Result;

						var raw = postResponse.Content.ReadAsStringAsync().Result;

						if (!postResponse.IsSuccessStatusCode)
						{
							// Parse the error
							error = JsonConvert.DeserializeObject<ErrorResponse>(raw);
							response = null;
							return false;
						}

						// Parse the response
						response = JsonConvert.DeserializeObject<AuthDeviceResolveResponse>(raw);
						error = null;
						return true;
					}
				}
			}
		}
	}
}