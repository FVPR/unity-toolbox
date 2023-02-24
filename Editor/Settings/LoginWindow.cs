using System;
using System.Net;
using System.Net.Http;
using Net.Codecrete.QrCodeGenerator;
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

		private string _userCode;
		private string _deviceCode;
		private int _interval;
		private DateTime _lastPollTime = DateTime.MinValue;
		private DateTime _expiresAt = DateTime.MinValue;
		private string _verificationUrl;

		private string _directUrl;
		private Texture2D _qrTexture;
		
		private bool _firstFrame = true;

		private void OnEnable()
		{
			// Call the API to get the user code and device code
			var client = new HttpClient();
			var response = client.PostAsync(
				$"{Strings.Url}/api/v1/auth/device",
				PostData.SerializeContent("identify", "publish")
			).Result;
			
			// Parse the response
			var raw = response.Content.ReadAsStringAsync().Result;
			var json = JObject.Parse(raw);
			_userCode = json["user_code"].Value<string>();
			_deviceCode = json["device_code"].Value<string>();
			_interval = json["interval"].Value<int>();
			_expiresAt = DateTime.Now.AddSeconds(json["expires_in"].Value<int>());
			_verificationUrl = json["verification_uri"].Value<string>();
			
			// Generate the data
			_directUrl = $"{_verificationUrl}?code={_userCode}";
			var qr = QrCode.EncodeText(_directUrl, QrCode.Ecc.Quartile);
			var scale = 8;
			var texture = new Texture2D((qr.Size + 2) * scale, (qr.Size + 2) * scale, TextureFormat.RGBA32, false);
			
			// Fill the texture with white
			for (var y = 0; y < texture.height; y++)
				for (var x = 0; x < texture.width; x++)
					texture.SetPixel(x, y, Color.white);
			
			// texture.SetPixel(x, y, qr.GetModule(x, y) ? Color.black : Color.white);
			for (var y = 0; y < qr.Size; y++)
			{
				for (var x = 0; x < qr.Size; x++)
				{
					var color = qr.GetModule(x, y) ? Color.black : Color.white;
					
					for (var i = 0; i < scale; i++)
						for (var j = 0; j < scale; j++)
							texture.SetPixel((x + 1) * scale + i, (y + 1) * scale + j, color);
				}
			}
			texture.Apply();
			texture.FlipVertically();
			_qrTexture = texture;
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
			
			if (DateTime.Now - _lastPollTime < TimeSpan.FromSeconds(_interval)) return;
			_lastPollTime = DateTime.Now;

			// GET /api/v1/device/resolve
			var client = new HttpClient();
			var response = client.GetAsync($"{Strings.Url}/api/v1/auth/device/resolve?code={_deviceCode}").Result;
			if (response.StatusCode != HttpStatusCode.OK) return;
			
			// Parse the response
			var raw = response.Content.ReadAsStringAsync().Result;
			var json = JObject.Parse(raw);
			var token = json["token"].Value<string>();
			EditorPrefs.SetString(Strings.TokenPref, token);
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
			
			GUILayout.FlexibleSpace();
			EditorGUILayout.Space();
			
			var rect = EditorGUILayout.GetControlRect(false, _qrTexture.width);
			rect.x += (rect.width - _qrTexture.width) / 2;
			rect.width = _qrTexture.width;
			GUI.DrawTexture(rect, _qrTexture);
			
			EditorGUILayout.Space();

			GUILayout.Label("— OR —", EditorStyles.centeredGreyMiniLabel);
			
			FvprToolbox.Centered(() =>
			{
				if (GUILayout.Button($"{_directUrl}", EditorStyles.linkLabel))
					Application.OpenURL(_directUrl);
			});
			
			GUILayout.FlexibleSpace();
			
			Repaint();
		}
	}
}