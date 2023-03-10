using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace FVPR.Toolbox
{
	public class PublishPackageUtils : EditorWindow
	{
		private static bool IsLoggedIn()
		{
			// Check if the API is reachable
			if (!FvprApi.Ping.HEAD())
			{
				EditorUtility.DisplayDialog(
					"API unreachable",
					"Cannot connect to the FVPR API. Please try again later.",
					"Close"
				);
				return false;
			}
			
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
			
			// Check if the API is reachable
			if (!FvprApi.Ping.HEAD())
			{
				EditorUtility.DisplayDialog(
					"API unreachable",
					"The FVPR API is currently unavailable. Please try again later.",
					"Close"
				);
				return;
			}
			
			// Get user info
			var token = EditorPrefs.GetString(Strings.TokenPref);
			if (!FvprApi.WhoAmI.GET(token, out var response, out var errorResponse))
			{
				// 401
				if (errorResponse.Is(HttpStatusCode.Unauthorized))
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
				if (errorResponse.Is(HttpStatusCode.Forbidden))
				{
					if (!EditorUtility.DisplayDialog(
						"Forbidden",
						"Your account has been locked!\n\n" +
						"Contact a moderator on our Discord server for more information.",
						"Close",
						"Open Discord"
					))
						Application.OpenURL($"https://{Strings.Domain}/discord");
					return;
				}
				// Not 200
				EditorUtility.DisplayDialog(
					"Error",
					$"Failed to get user data: ({errorResponse.Code}) {errorResponse.Message}",
					"Close"
				);
				return;
			}
			
			// 200
			
			// Make sure the "ticket.publish" scope is set
			if (!response.Scopes.Contains("ticket.publish"))
			{
				if (EditorUtility.DisplayDialog(
					"Unauthorized",
					"This token does not have permission to publish packages.",
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
			if (!response.Domains.Any(name.StartsWith))
			{
				EditorUtility.DisplayDialog(
					"Unauthorized",
					$"You are not allowed to publish packages with the name '{name}'.\n\n" +
					$"Your scopes are: {string.Join(", ", response.Domains)}",
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
					var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
						.Where(file => !file.Replace('\\', '/').Contains("/.git/"))
						.ToArray();
					// ToDo: Apply .gitignore rules (if any)
					foreach (var file in files)
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
			
			// Open a ticket
			if (!FvprApi.Auth.Ticket.Open.POST(
				token,
				TicketType.PublishPackage,
				out var ticket,
				out errorResponse,
				("name", name),
				("version", packageJson["version"].Value<string>())
			))
			{
				// Delete the zip file
				if (File.Exists(tmpPath))
					File.Delete(tmpPath);
				
				// Show error
				EditorUtility.DisplayDialog(
					"Error",
					$"Failed to open a ticket: ({errorResponse.Code}) {errorResponse.Message}",
					"Close"
				);
				return;
			}
			
			// Await confirmation
			// EditorUtility.DisplayProgressBar(
			// 	"Publishing package",
			// 	"Awaiting approval...\n\n" +
			// 	"Open your FVPR Authenticator app, or visit the url below to confirm the publish request.\n\n" +
			// 	$"URL: https://{Strings.Domain}/authenticator",
			// 	0.3f
			// );
			EditorUtility.DisplayProgressBar(
				"Publishing package",
				"Awaiting approval from the FVPR Authenticator...",
				0.3f
			);
			{
				// Show a notice, if needed
				// %localappdata%/FVPR/unity_has_read_app_notice
				var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FVPR");
				var file = Path.Combine(dir, "unity_has_read_app_notice");
				if (!File.Exists(file))
				{
					EditorUtility.DisplayDialog(
						"Notice",
						"You may need to open the FVPR Authenticator to confirm publishing requests.\n\n" +
						"We recommend you open the url below on your phone, and add it to your home screen.\n\n" +
						Strings.AuthenticatorUrl,
						"Close"
					);
					FvprToolbox.Mkdir(dir);
					File.Create(file).Close();
				}
				
				// Wait for confirmation
				while (true)
				{
					// Get the current status of the ticket
					if (!FvprApi.Auth.Ticket.Status.GET(ticket.Uid, out var ticketResponse, out errorResponse))
					{
						// Delete the zip file
						if (File.Exists(tmpPath))
							File.Delete(tmpPath);
						
						// Show error
						EditorUtility.ClearProgressBar();
						EditorUtility.DisplayDialog(
							"Error",
							$"Failed to get ticket status. It may have expired.\n\n" +
							$"({errorResponse.Code}) {errorResponse.Message}",
							"Close"
						);
						return;
					}

					var status = ticketResponse.GetStatus();
					
					// Check if the ticket has been confirmed
					if (status == TicketStatus.Approved)
						break;
					
					// Check if the ticket has been denied
					if (status == TicketStatus.Rejected)
					{
						// Delete the zip file
						if (File.Exists(tmpPath))
							File.Delete(tmpPath);
						
						// Show error
						EditorUtility.ClearProgressBar();
						EditorUtility.DisplayDialog(
							"Rejected",
							"You have rejected the publish request.",
							"Close"
						);
						return;
					}
					
					// Check if the ticket has expired
					if (status == TicketStatus.Expired)
					{
						// Delete the zip file
						if (File.Exists(tmpPath))
							File.Delete(tmpPath);
						
						// Show error
						EditorUtility.ClearProgressBar();
						EditorUtility.DisplayDialog(
							"Expired",
							"The publish request has expired.",
							"Close"
						);
						return;
					}
					
					// If it is anything other than AwaitingApproval, something went wrong
					if (status != TicketStatus.AwaitingApproval)
					{
						// Delete the zip file
						if (File.Exists(tmpPath))
							File.Delete(tmpPath);
						
						// Show error
						EditorUtility.ClearProgressBar();
						EditorUtility.DisplayDialog(
							"Error",
							$"An error occurred while awaiting confirmation. The ticket status was '{ticket.Status}'.",
							"Close"
						);
						return;
					}
					
					// Wait a bit
					Thread.Sleep(1000);
				}
			}
			
			// Publish
			EditorUtility.DisplayProgressBar("Publishing package", "Uploading package...", 0.6f);
			{
				string errorMessage = null;
				// Upload
				try
				{
					// using (var httpClientHandler = new HttpClientHandler())
					// {
					// 	var bytes = File.ReadAllBytes(tmpPath);
					// 	httpClientHandler.MaxRequestContentBufferSize = (1024 * 1024) + bytes.Length;
					// 	
					// 	using (var httpClient = new HttpClient(httpClientHandler))
					// 	{
					// 		httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
					// 		response = httpClient.PostAsync(
					// 			$"{Strings.Url}/api/v1/publish?name={name}&version={packageJson["version"].Value<string>()}",
					// 			new ByteArrayContent(bytes)
					// 		).Result;
					// 	}
					// }
					var payload = File.ReadAllBytes(tmpPath);
					if (!FvprApi.Publish.POST(ticket.Uid, payload, out var error))
						errorMessage = $"Failed to publish the package: ({error.Code}) {error.Message}";
				}
				catch (Exception e)
				{
					Debug.LogException(e);
					errorMessage = "An error occurred while uploading the package. See the console for more details.";
				}
				finally
				{
					EditorUtility.ClearProgressBar();
					try
					{
						if (File.Exists(tmpPath))
							File.Delete(tmpPath);
					}
					catch
					{
						// Debug.LogWarning($"Failed to delete temporary file '{tmpPath}'");
					}
				}
				
				// Check response
				if (errorMessage is null)
				{
					EditorUtility.DisplayDialog(
						"Success",
						$"The package '{name}' has been published successfully.",
						"Close"
					);
					AssetDatabase.Refresh();
				}
				else
					EditorUtility.DisplayDialog("Error", errorMessage, "Close");
			}
		}
	}
}