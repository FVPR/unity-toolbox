using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace FVPR.Toolbox
{
	public class LoginWindow : EditorWindow
	{
		public static void ShowWindow()
		{
			var window = GetWindow<LoginWindow>(true);
			window.titleContent = new GUIContent("FVPR Login");
			window.minSize = new Vector2(400, 400);
			window.maxSize = new Vector2(400, 400);
			window.ShowModal();
		}

		[Serializable]
		private struct PostData
		{
			[JsonProperty("scopes")] string[] Scopes { get; set; }
			
			public static string Serialize(params string[] scopes) => JsonConvert.SerializeObject(new PostData { Scopes = scopes });
			public static HttpContent SerializeContent(params string[] scopes) => new StringContent(Serialize(scopes));
		}

		private AuthDeviceResponse _response;
		private DateTime _lastPollTime = DateTime.MinValue;
		private DateTime _expiresAt = DateTime.MinValue;
		private GUIStyle _userCodeStyle;
		
		private bool _firstFrame = true;

		private void OnEnable()
		{
			// Call the API to get the user code and device code
			if (!FvprApi.Auth.Device.POST(out var response, out var error))
			{
				Debug.LogError(error.ToString("Failed to start authentication process"));
				Close();
				return;
			}
			
			// Parse the response
			_response = response;
			_expiresAt = DateTime.Now.AddSeconds(response.ExpiresIn);
		}

		private void Poll()
		{
			if (DateTime.Now > _expiresAt)
			{
				EditorUtility.DisplayDialog("FVPR Login", "The login code has expired.", "OK");
				EditorApplication.update -= Poll;
				Close();
				return;
			}
			
			if (DateTime.Now - _lastPollTime < TimeSpan.FromSeconds(_response.Interval)) return;
			_lastPollTime = DateTime.Now;

			// GET /api/v1/device/resolve
			// var client = new HttpClient();
			// var response = client.GetAsync($"{Strings.Url}/api/v1/auth/device/resolve?code={_deviceCode}").Result;
			// if (response.StatusCode != HttpStatusCode.OK) return;
			if (!FvprApi.Auth.Device.Resolve.GET(_response.DeviceCode, out var response, out var error)) return;
			
			// Parse the response
			EditorPrefs.SetString(Strings.TokenPref, response.Token);
			EditorApplication.update -= Poll;
			GetWindow<SettingsWindow>().Check();
			Close();
		}

		private void OnGUI()
		{
			Poll();

			if (_firstFrame)
			{
				_firstFrame = false;
				position = new Rect(
					(Screen.currentResolution.width - position.width) / 2,
					(Screen.currentResolution.height - position.height) / 2,
					position.width,
					position.height
				);
			}

			if (_userCodeStyle == null)
			{
				_userCodeStyle = new GUIStyle("label")
				{
					fontSize = 64
				};
			}

			GUILayout.FlexibleSpace();

			// CenteredText("Visit the link below (preferably on your phone), select \"add new device\", and enter the following code");
			CenteredText("Visit the link below (preferably on your phone)");
			GUILayout.Space(10);
			CenteredText("Log in, select \"add new device\", and enter the following code");

			GUILayout.FlexibleSpace();

			CenteredText(_response.UserCode, _userCodeStyle);

			GUILayout.FlexibleSpace();

			FvprToolbox.Centered(() =>
			{
				if (GUILayout.Button(_response.VerificationUri, EditorStyles.linkLabel))
					Application.OpenURL(_response.VerificationUri);
			});

			GUILayout.FlexibleSpace();
			
			Repaint();
		}

		private void CenteredText(string text, GUIStyle style, params GUILayoutOption[] options)
		{
			FvprToolbox.Centered(() =>
			{
				GUILayout.Label(text, style, options);
			});
		}
		private void CenteredText(string text, params GUILayoutOption[] options) => CenteredText(text, "label", options);
	}
}