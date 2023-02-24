using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace FVPR.Toolbox
{
	public class PublishPackageUtils : EditorWindow
	{
		private static bool IsLoggedIn()
		{
			var token = EditorPrefs.GetString(Strings.TokenPref);
			
			// Check if token is set
			if (string.IsNullOrEmpty(token))
			{
				EditorUtility.DisplayDialog(
					"Login required",
					"You need to login to publish a package.",
					"Close"
				);
				SettingsWindow.ShowWindow();
				LoginWindow.ShowWindow();
				return false;
			}
			
			return true;
		}
		
		[MenuItem("FVPR/Publish Package", false, 610)]
		public static void SelectPackage()
		{
			if (IsLoggedIn())
				PackageSelectorPopup.ShowWindow("Select a package to publish", Publish);
		}

		public static void Publish(string name)
		{
			if (!IsLoggedIn()) return;
			
			// Make sure the package exists
			var path = Path.Combine("Packages", name);
			if (!Directory.Exists(path))
			{
				EditorUtility.DisplayDialog(
					"Package not found",
					$"The package '{name}' does not exist.",
					"Close"
				);
				return;
			}
			
			// Make sure the package has a valid package.json
			var jsonPath = Path.Combine("Packages", name, "package.json");
			if (!File.Exists(jsonPath))
			{
				EditorUtility.DisplayDialog(
					"Invalid package",
					$"The package '{name}' does not have a valid package.json.",
					"Close"
				);
				return;
			}
			// Load the package.json
			JObject packageJson;
			try
			{
				packageJson = JObject.Parse(File.ReadAllText(jsonPath));
			}
			catch
			{
				EditorUtility.DisplayDialog(
					"Invalid package",
					$"The package '{name}' does not have a valid package.json.",
					"Close"
				);
				return;
			}
			// Validate
			if (!ValidationUtils.IsPackageJsonValid(packageJson, out var warnings, out var errors))
			{
				foreach (var error in errors)
					Debug.LogError(FvprToolbox.MakeLogString("Publish Package", error, "red"));
				foreach (var warning in warnings)
					Debug.LogWarning(FvprToolbox.MakeLogString("Publish Package", warning, "yellow"));
				EditorUtility.DisplayDialog(
					"Invalid package",
					$"The package '{name}' does not have a valid package.json. See the console for more details.",
					"Close"
				);
				return;
			}
			
			// Get user info
			var token = EditorPrefs.GetString(Strings.TokenPref);
			var client = new HttpClient();
			client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
			var response = client.GetAsync($"{Strings.Url}/api/v1/whoami").Result;
			// 503
			if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
			{
				EditorUtility.DisplayDialog(
					"Service Unavailable",
					"The FVPR API is currently unavailable. Please try again later.",
					"Close"
				);
				return;
			}
			// 401
			if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
			{
				EditorUtility.DisplayDialog(
					"Unauthorized",
					"The token is invalid. Please login again.",
					"Close"
				);
				EditorPrefs.DeleteKey(Strings.TokenPref);
				SettingsWindow.ShowWindow();
				LoginWindow.ShowWindow();
				return;
			}
			// 403
			if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
			{
				var r = JObject.Parse(response.Content.ReadAsStringAsync().Result); 
				if (!EditorUtility.DisplayDialog(
					"Forbidden",
					$"Your account has been locked: {r["reason"].Value<string>()}\n\n" +
					$"Contact a moderator on our Discord server for more information.",
					"Close",
					"Open Discord"
				))
					Application.OpenURL($"{Strings.Url}/discord");
				return;
			}
			// Not 200
			if (response.StatusCode != System.Net.HttpStatusCode.OK)
			{
				EditorUtility.DisplayDialog(
					"Error",
					$"Failed to get user data: ({response.StatusCode}) {response.ReasonPhrase}",
					"Close"
				);
				SettingsWindow.ShowWindow();
				return;
			}
			// 200
			var userJson = JObject.Parse(response.Content.ReadAsStringAsync().Result);
			var scopes = userJson["scopes"].Value<JArray>().Children().Select(x => x.Value<string>()).ToArray();
			var domains = userJson["domains"].Value<JArray>().Children().Select(x => x.Value<string>()).ToArray();
			
			// Make sure the "publish" scope is set
			if (!scopes.Contains("publish"))
			{
				if (EditorUtility.DisplayDialog(
					"Unauthorized",
					$"This token does not have permission to publish packages.",
					"Fix",
					"Close"
				))
				{
					EditorPrefs.DeleteKey(Strings.TokenPref);
					SettingsWindow.ShowWindow();
					LoginWindow.ShowWindow();
				}
				return;
			}
			
			// Check if the package name starts with one of the user's domains
			if (!domains.Any(name.StartsWith))
			{
				EditorUtility.DisplayDialog(
					"Unauthorized",
					$"You are not allowed to publish packages with the name '{name}'.\n\n" +
					$"Your scopes are: {string.Join(", ", domains)}",
					"Close"
				);
				return;
			}

			// Put all the files in a zip
			// ReSharper disable AssignNullToNotNullAttribute
			var tmpPath = Path.Combine("Temp", $"{packageJson["name"].Value<string>()}_{packageJson["version"].Value<string>()}.zip");
			// ReSharper restore AssignNullToNotNullAttribute
			if (File.Exists(tmpPath))
				File.Delete(tmpPath);

			try
			{
				EditorUtility.DisplayProgressBar("Publishing package", "Creating zip file...", 0f);
				using (var zipFileStream = File.Create(tmpPath))
				using (var zip = new ZipArchive(zipFileStream, ZipArchiveMode.Create))
				{
					foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
					{
						var entry = zip.CreateEntry(file.Substring(path.Length + 1));
						using (var entryStream = entry.Open())
						using (var fileStream = File.OpenRead(file))
							fileStream.CopyTo(entryStream);
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				EditorUtility.DisplayDialog(
					"Error",
					$"An error occurred while creating the zip file. See the console for more details.",
					"Close"
				);
				if (File.Exists(tmpPath))
					File.Delete(tmpPath);
				return;
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
			
			// Publish
			EditorUtility.DisplayProgressBar("Publishing package", "Uploading package...", 0.5f);
			{
				// Upload
				try
				{
					// using (var httpClient = new HttpClient())
					// {
					// 	var bytes = File.ReadAllBytes(tmpPath);
					// 	httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
					// 	httpClient.DefaultRequestHeaders.Add("Content-Type", "application/zip");
					// 	httpClient.DefaultRequestHeaders.Add("Content-Length", bytes.Length.ToString());
					// 	response = httpClient.PostAsync(
					// 		$"{Strings.Url}/api/v1/publish?name={name}&version={packageJson["version"].Value<string>()}",
					// 		new ByteArrayContent(bytes)
					// 	).Result;
					// }

					using (var httpClientHandler = new HttpClientHandler())
					{
						var bytes = File.ReadAllBytes(tmpPath);
						httpClientHandler.MaxRequestContentBufferSize = (1024 * 1024) + bytes.Length;
						
						using (var httpClient = new HttpClient(httpClientHandler))
						{
							httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
							response = httpClient.PostAsync(
								$"{Strings.Url}/api/v1/publish?name={name}&version={packageJson["version"].Value<string>()}",
								new ByteArrayContent(bytes)
							).Result;
						}
					}
				}
				catch (Exception e)
				{
					Debug.LogException(e);
					EditorUtility.DisplayDialog(
						"Error",
						"An error occurred while uploading the package. See the console for more details.",
						"Close"
					);
					return;
				}
				finally
				{
					EditorUtility.ClearProgressBar();
					try
					{
						// if (File.Exists(tmpPath))
						// 	File.Delete(tmpPath);
					}
					catch
					{
						Debug.LogWarning($"Failed to delete temporary file '{tmpPath}'");
					}
				}
				
				// Check response
				if (response.StatusCode == System.Net.HttpStatusCode.OK)
				{
					EditorUtility.DisplayDialog(
						"Success",
						$"The package '{name}' has been published successfully.",
						"Close"
					);
					AssetDatabase.Refresh();
				}
				else
				{
					try
					{
						var error = JObject.Parse(response.Content.ReadAsStringAsync().Result);
						var message = error["message"]?.Value<string>() ?? response.ReasonPhrase;
						EditorUtility.DisplayDialog(
							$"Failed to publish package ({response.StatusCode})",
							message,
							"Close"
						);
					}
					catch
					{
						EditorUtility.DisplayDialog(
							$"Failed to publish package ({response.StatusCode})",
							response.ReasonPhrase,
							"Close"
						);
					}
				}
			}
		}
	}
}