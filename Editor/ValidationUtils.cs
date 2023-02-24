using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Version = SemanticVersioning.Version;
using Range = SemanticVersioning.Range;

namespace FVPR.Toolbox
{
	public static class ValidationUtils
	{
		public static bool IsPackageNameValid(string name, out string[] errors)
		{
			var errorsList = new List<string>();

			if (name == null)
			{
				errorsList.Add("name cannot be null");
				goto Return;
			}

			if (name == "")
			{
				errorsList.Add("name length must be greater than zero");
				goto Return;
			}

			if (name.StartsWith("."))
				errorsList.Add("name cannot start with a period");
		
			if (name.StartsWith("_"))
				errorsList.Add("name cannot start with an underscore");
		
			if (name.Trim() != name)
				errorsList.Add("name cannot contain leading or trailing spaces");
		
			if (name.Length > 214)
				errorsList.Add("name can no longer contain more than 214 characters");
		
			if (name.ToLower() != name)
				errorsList.Add("name can no longer contain capital letters");
		
			if (Regex.IsMatch(name.Split('/').Last(), @"[\~\'\!\(\)\*]"))
				errorsList.Add("name can no longer contain special characters ('~'!'()*')");
		
			if (Uri.EscapeDataString(name) != name)
				errorsList.Add("name can only contain URL-friendly characters");

			Return:
			errors = errorsList.ToArray();
			return errors.Length == 0;
		}

		public static bool IsPackageVersionValid(string version) => Version.TryParse(version, out _);

		public static bool IsPackageJsonValid(JObject json, out string[] warnings, out string[] errors)
		{
			if (json == null)
			{
				warnings = Array.Empty<string>();
				errors = new[] { "INTERNAL ERROR: json is null" };
				return false;
			}

			var warningsList = new List<string>();
			var errorsList = new List<string>();
			
			// Validate the name
			var name = json["name"]?.Value<string>();
			if (name == null)
				errorsList.Add("name is missing");
			if (!IsPackageNameValid(name, out var nameErrors))
				errorsList.AddRange(nameErrors);
		
			// Validate the version
			var version = json["version"]?.Value<string>();
			if (version == null)
				errorsList.Add("version is missing");
			if (!IsPackageVersionValid(version))
				errorsList.Add("version is invalid");
		
			// Make sure a display name is present
			if (string.IsNullOrWhiteSpace(json["displayName"]?.Value<string>()))
				errorsList.Add("displayName is missing");
		
			// Make sure a description is present
			if (string.IsNullOrWhiteSpace(json["description"]?.Value<string>()))
				errorsList.Add("description is missing");
		
			// Make sure an author object is present
			if (json["author"] == null)
				errorsList.Add("author is missing");
		
			// Make sure the author object has at least a name
			else if (string.IsNullOrWhiteSpace(json["author"]["name"]?.Value<string>()))
				errorsList.Add("author.name is missing");
		
			// If "keywords" is present, make sure it's an array of strings
			if (json["keywords"] != null)
			{
				if (json["keywords"].Type != JTokenType.Array)
					errorsList.Add("keywords must be an array");
		
				foreach (var keyword in json["keywords"])
				{
					if (keyword.Type != JTokenType.String)
						errorsList.Add("keywords must be an array of strings");
				}
			}
		
			// If "dependencies", "gitDependencies", or "vpmDependencies" are present, make sure they're valid
			var error = CheckDependencies(json, "dependencies");
			if (error != null)
				errorsList.AddRange(error);
			error = CheckDependencies(json, "gitDependencies");
			if (error != null)
				errorsList.AddRange(error);
			error = CheckDependencies(json, "vpmDependencies");
			if (error != null)
				errorsList.AddRange(error);
		
			// If "legacyFolders" or "legacyFiles" are present, make sure they're valid
			error = CheckLegacy(json, "legacyFolders");
			if (error != null)
				errorsList.AddRange(error);
			error = CheckLegacy(json, "legacyFiles");
			if (error != null)
				errorsList.AddRange(error);
		
			// Don't allow deprecated packages to be published
			if (json["isDeprecated"]?.Value<bool>() == true)
				errorsList.Add("Unable to publish deprecated packages");
			if (json["deprecationMessage"] != null)
				errorsList.Add("Unable to publish packages with a deprecation message");
		
			// Make sure hideInEditor is a boolean
			if (json["hideInEditor"] != null && json["hideInEditor"].Type != JTokenType.Boolean)
				errorsList.Add("hideInEditor must be a boolean");
		
			// Make sure any known fields that we haven't checked are strings
			var uncheckedKnownFields = new[]
				{ "license", "licensesUrl", "category", "type", "documentationUrl", "changelogUrl" };
			foreach (var field in uncheckedKnownFields)
				if (json[field] != null && json[field].Type != JTokenType.String)
					errorsList.Add($"{field} must be a string");
			
			var checkedKnownFields = new[]
				{ "name", "version", "displayName", "description", "author", "keywords", "dependencies", "gitDependencies", "vpmDependencies", "legacyFolders", "legacyFiles", "isDeprecated", "deprecationMessage", "hideInEditor" };
			
			// If the url field is present, warn the user that it will be replaced by the server
			if (json["url"] != null)
				warningsList.Add("The url field will be replaced during publishing");
			
			// If the repo field is present, warn the user that it will be replaced by the server
			if (json["repo"] != null)
				warningsList.Add("The repo field will be replaced during publishing");
		
			// Return
			warnings = warningsList.ToArray();
			errors = errorsList.ToArray();
			return errors.Length == 0;
		}
		
		public static string[] CheckDependencies(JObject json, string objectName)
		{
			var dependencies = json[objectName];
			if (dependencies == null) return Array.Empty<string>();

			if (dependencies.Type != JTokenType.Object) return new[] { $"{objectName} must be an object" };
			var errors = new List<string>();
			foreach (var child in dependencies.Children<JProperty>())
			{
				var key = child.Name;
				var value = child.Value;
				
				if (value.Type != JTokenType.String) errors.Add($"[{objectName}/{key}] must be a string");
				
				if (!IsPackageNameValid(key, out var nameErrors))
					foreach (var error in nameErrors)
						errors.Add($"[{objectName}/{key}] {error}");

				if (objectName == "gitDependencies")
				{
					if (!IsValidGitUrl(value.Value<string>()))
						errors.Add($"[{objectName}/{key}] must be a valid git url");
				}
				else if (!SemanticVersioning.Range.TryParse(value.Value<string>(), out _))
					errors.Add($"[{objectName}/{key}] must be a valid semantic version range");
			}
			
			// Make sure there are no duplicate dependencies
			var duplicates = dependencies.Children<JProperty>().GroupBy(x => x.Name).Where(x => x.Count() > 1).Select(x => x.Key);
			foreach (var duplicate in duplicates)
				errors.Add($"{objectName} contains duplicate dependency {duplicate}");
			
			return errors.ToArray();
		}
		
		private const string GitUrlPattern = @"^((git|ssh|http(s)?)|(git@[\w\.]+))((:(//)?)([\w\.@\:/\-~]+)(\.git)(/)?)$";
		public static bool IsValidGitUrl(string url) => Regex.Match(url, GitUrlPattern).Success && url.EndsWith(".git");
		
		public static string[] CheckLegacy(JObject json, string objectName)
		{
			var legacy = json[objectName];
			if (legacy == null) return Array.Empty<string>();

			if (legacy.Type != JTokenType.Object) return new[] { $"{objectName} must be an object" };
			var errors = new List<string>();
			foreach (var child in legacy.Children<JProperty>())
			{
				var key = child.Name;
				var value = child.Value;
		
				// Make sure the key is a valid Unity Assets path, it doesn't have to exist
				if (key == "Assets" || key == "Assets/")
					errors.Add($"[{objectName}/{key}] must not be the root Assets folder");
				else if (!key.StartsWith("Assets/"))
					errors.Add($"[{objectName}/{key}] must be a valid Unity Assets path");
				else if (key.EndsWith("/"))
					errors.Add($"[{objectName}/{key}] must not end with a slash");
				try
				{
					_ = Path.GetFullPath(key);
				}
				catch (Exception)
				{
					errors.Add($"[{objectName}/{key}] must be a valid Unity Assets path");
				}

				if (value.Type != JTokenType.String) errors.Add($"[{objectName}/{key}] must be a string");
		
				// The value may be empty (thus optional), but if it's not, it must be a valid Unity Assets guid
				else if (value.Value<string>() != "" && !IsValidAssetGuid(value.Value<string>()))
					errors.Add($"[{objectName}/{key}] must be a valid Unity Assets guid");
			}
			
			// Make sure there are no duplicate legacy paths
			var duplicates = legacy.Children<JProperty>().GroupBy(x => x.Name).Where(x => x.Count() > 1).Select(x => x.Key);
			foreach (var duplicate in duplicates)
				errors.Add($"{objectName} contains duplicate path {duplicate}");
		
			return errors.ToArray();
		}
		
		public static bool IsValidAssetGuid(string guid) =>
			guid.Length == 32
			&& guid.All(c =>
				char.IsDigit(c)
				|| (c >= 'a' && c <= 'f')
				|| (c >= 'A' && c <= 'F')
			);
	}
}