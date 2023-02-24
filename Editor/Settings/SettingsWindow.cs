using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace FVPR.Toolbox
{
	public class SettingsWindow : EditorWindow
	{
		private bool _isLoggedOut;
		private bool _didError;
		private bool _tokenIsInvalid;
		private bool _missingScope;
		private string _username;
		private bool _firstFrame = true;

		private Texture _menuIcon;
		
		[MenuItem("FVPR/Settings", false, 650)]
		public static void ShowWindow()
		{
			var window = GetWindow<SettingsWindow>(true);
			window.titleContent = new GUIContent("FVPR Settings");
			// 147
			window.minSize = new Vector2(400, 130);
			window.maxSize = new Vector2(400, 130);
			window.Show();
		}

		private void OnEnable()
		{
			Check();
			_menuIcon = EditorGUIUtility.IconContent("SettingsIcon").image;
		}

		private void OnGUI()
		{
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
			
			EditorGUILayout.Space();
			DrawAccountInfo();
			EditorGUILayout.Space();
			DrawAbout();
		}

		private bool _showLoginWindow;
		private void DrawAccountInfo()
		{
			EditorGUILayout.BeginVertical("helpbox");
			{
				GUILayout.Label("Account Info", EditorStyles.boldLabel);
				
				if (_isLoggedOut)
					LabelWithLoginButton("You are not logged in");
				
				if (_didError) LabelWithLogoutButton("An error occurred while checking your account info", false);

				if (_tokenIsInvalid)
					LabelWithLoginButton("Your token is invalid, maybe it expired?");
				
				if (_missingScope) LabelWithLoginButton("The token is missing the 'identify' scope! Please re-login.");

				if (_username != "")
				{
					LabelWithLogoutButton($"You are logged in as {_username}", true);
				}
			}
			EditorGUILayout.EndVertical();

			if (_showLoginWindow)
			{
				_showLoginWindow = false;
				LoginWindow.ShowWindow();
			}
		}

		private void LabelWithLoginButton(string label)
		{
			GUILayout.Label(label);
			if (GUILayout.Button("Login"))
				_showLoginWindow = true;
		}
		
		private void LabelWithLogoutButton(string label, bool withLogoutAllButton)
		{
			GUILayout.Label(label);
			EditorGUILayout.BeginHorizontal();
			{
				if (GUILayout.Button("Logout"))
				{
					var token = EditorPrefs.GetString(Strings.TokenPref, null);
					EditorPrefs.DeleteKey(Strings.TokenPref);
					var client = new HttpClient();
					client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
					_ = client.PostAsync($"{Strings.Url}/api/v1/auth/revoke", null).Result;
					Check();
				}
				
				if (withLogoutAllButton)
				{
					var rect = EditorGUILayout.GetControlRect(false, 16, GUILayout.Width(16));
					rect.y += 2f;
					
					// var texture = new Texture2D(1, 1);
					// texture.SetPixel(0, 0, Color.white);
					// texture.Apply();
					// GUI.DrawTexture(rect, texture);
					GUI.DrawTexture(rect, _menuIcon);

					if (
						GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition) &&
						Event.current.type == EventType.MouseDown)
					{
						var menu = new GenericMenu();
						menu.AddItem(new GUIContent("Logout from all devices"), false, () =>
						{
							var token = EditorPrefs.GetString(Strings.TokenPref, null);
							EditorPrefs.DeleteKey(Strings.TokenPref);
							var client = new HttpClient();
							client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
							var response = client.PostAsync($"{Strings.Url}/api/v1/auth/revoke-all", null).Result;
							// Parse response
							var json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
							var message = json["message"].Value<string>();
							Debug.Log(FvprToolbox.MakeLogString("FVPR API", message, "cyan"));
							Check();
						});
						menu.ShowAsContext();
					}
				}
			}
			EditorGUILayout.EndHorizontal();
		}

		private void DrawAbout()
		{
			EditorGUILayout.BeginVertical("helpbox");
			{
				// GUILayout.Label("About", EditorStyles.boldLabel);
				
				FvprToolbox.Centered(() =>
				{
					if (GUILayout.Button("Discord", EditorStyles.linkLabel))
						Application.OpenURL($"{Strings.Url}/discord");
					
					GUILayout.Label("|");
					
					if (GUILayout.Button("API Documentation", EditorStyles.linkLabel))
						Application.OpenURL($"{Strings.Url}/docs/api");
					
					GUILayout.Label("|");
					
					if (GUILayout.Button("Terms of Service", EditorStyles.linkLabel))
						Application.OpenURL($"{Strings.Url}/tos");
				});
#if FVPR_DEV
				FvprToolbox.Centered(() => GUILayout.Label("DEVELOPER MODE ENABLED"));
#else
				FvprToolbox.Centered(() => GUILayout.Label("Made with ❤️ by Fox_score"));
#endif
			}
			EditorGUILayout.EndVertical();
		}

		public void Check()
		{
			// Reset
			_isLoggedOut = false;
			_didError = false;
			_tokenIsInvalid = false;
			_missingScope = false;
			_username = "";

			// Check if the user is logged in
			var token = EditorPrefs.GetString(Strings.TokenPref, null);
			if (string.IsNullOrEmpty(token))
			{
				_isLoggedOut = true;
				return;
			}
			
			// Attempt to get user data
			// {Strings.Url}/api/v1/whoami
			// Bearer {token}
			var client = new HttpClient();
			client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
			var response = client.GetAsync($"{Strings.Url}/api/v1/whoami").Result;
			if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
			{
				_tokenIsInvalid = true;
				EditorPrefs.DeleteKey(Strings.TokenPref);
				return;
			}
			if (response.StatusCode != System.Net.HttpStatusCode.OK)
			{
				_didError = true;
				Debug.LogError($"Failed to get user data: ({response.StatusCode}) {response.ReasonPhrase}");
				return;
			}
			
			// Parse user data
			try
			{
				var raw = response.Content.ReadAsStringAsync().Result;
				var json = JObject.Parse(raw);
				_username = json["name"].Value<string>();
				// Check the scopes object, and see if it contains "publish"
				var scopes = json["scopes"].ToObject<string[]>();
				if (scopes == null || scopes.All(s => s != "publish"))
				{
					_missingScope = true;
					EditorPrefs.DeleteKey(Strings.TokenPref);
				}
				return;
			}
			catch (Exception e)
			{
				_didError = true;
				Debug.LogError($"Failed to parse user data: {e.Message}");
				return;
			}
		}
	}
}