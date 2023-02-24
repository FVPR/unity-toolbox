using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FVPR.Toolbox
{
	public class ConvertToPackageWindow : EditorWindow
	{
		private static readonly string[] ExtensionBlacklist = new[]
		{
			".asset",
			".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".gif", ".bmp",
			".fbx", ".obj",
			".mat",
			".prefab",
			".unity",
			".anim", ".controller", ".overrideController",
			".mask", ".guiskin", ".flare", ".curves",
			".compute", ".cginc", ".shader", ".cg", ".gml", ".hlsl",
			".uss", ".ussl", ".uxml", ".uxmll", ".xml",
			".playable",
			".ttf", ".otf", ".fnt",
			".fontsettings", ".guiskin",
		};
		
		[MenuItem("FVPR/Convert to Package", false, 600)]
		private static void ShowWindow()
		{
			var window = GetWindow<ConvertToPackageWindow>(true);
			window.titleContent = new GUIContent("Convert to Package");
			window.minSize = new Vector2(400, 150);
			window.maxSize = new Vector2(400, 150);
			window.Show();
		}
		
		private bool _isFirstFrame = true;

		private string _targetFolder;
		private string _targetFolderDisplayName;
		
		private string _packageName;
		private string _packageDisplayName;
		private string _packageVersion = "1.0.0";

		private void OnGUI()
		{
			if (_isFirstFrame)
			{
				_isFirstFrame = false;
				position = new Rect(
					(Screen.currentResolution.width - position.width) / 2,
					(Screen.currentResolution.height - position.height) / 2,
					position.width,
					position.height
				);
			}
			
			EditorGUILayout.Separator();
			EditorGUILayout.BeginHorizontal();
			{
				GUI.enabled = false;
				EditorGUILayout.TextField("Directory", _targetFolderDisplayName);
				GUI.enabled = true;
				if (GUILayout.Button("Select", GUILayout.Width(64)))
					Select();
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Separator();
			
			if (string.IsNullOrEmpty(_targetFolder))
				GUI.enabled = false;
			
			EditorGUILayout.LabelField("Package Info", EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			{
				_packageName = EditorGUILayout.TextField("Name", _packageName);
				_packageDisplayName = EditorGUILayout.TextField("Display Name", _packageDisplayName);
				_packageVersion = EditorGUILayout.TextField("Version", _packageVersion);
			}
			EditorGUI.indentLevel--;
			EditorGUILayout.Separator();

			GUI.enabled = GUI.enabled && ValidateInput();
			if (GUILayout.Button("Convert"))
				Convert();
		}

		private void Select()
		{
			_targetFolder = EditorUtility.OpenFolderPanel("Select Target Folder", Application.dataPath, "");
			if (string.IsNullOrEmpty(_targetFolder)) return;
			
			// Make sure the path is relative to the project, and is in the Assets folder
			if (!_targetFolder.StartsWith(Application.dataPath))
			{
				if (EditorUtility.DisplayDialog(
					    "Invalid Folder",
					    "The selected folder must be inside the Assets folder.",
					    "Retry",
					    "Cancel"
				)) Select();
				return;
			}
			if (_targetFolder == Application.dataPath)
			{
				if (EditorUtility.DisplayDialog(
					    "Invalid Folder",
					    "The selected folder must not be the Assets folder itself.",
					    "Retry",
					    "Cancel"
				)) Select();
				return;
			}
			_targetFolderDisplayName = _targetFolder.Substring(Application.dataPath.Length + 1);
		}

		private bool ValidateInput()
		{
			if (!ValidationUtils.IsPackageNameValid(_packageName, out _)) return false;
			if (!ValidationUtils.IsPackageVersionValid(_packageVersion)) return false;
			if (string.IsNullOrWhiteSpace(_packageDisplayName)) return false;
			return true;
		}

		private void Convert()
		{
			// Check if there are any Assets in the folder that are on the blacklist, and warn the user if so
			// Ignore anything in Resources folders
			if (HasBlacklist(_targetFolder, out var extensions))
			{
				var message = "The following file types will not be loadable from a package:\n";
				message += string.Join("\n", extensions);
				message += "\n\nUse a Resources folder to store these files instead.";
				if (!EditorUtility.DisplayDialog("Unsupported File Types", message, "Continue Anyway", "Cancel"))
				{
					Close();
					return;
				}
			}
			
			// Check if there already is a package.json file in the folder
			if (File.Exists(Path.Combine(_targetFolder, "package.json")))
			{
				if (!EditorUtility.DisplayDialog(
					    "Overwrite Existing Package",
					    "A package.json file already exists in this folder. Overwrite it?",
					    "Overwrite",
					    "Cancel"
				))
				{
					Close();
					return;
				}
			}
			
			// Make sure the package doesn't already exist in the project
			var packagePath = Path.Combine("Packages", _packageName);
			if (Directory.Exists(packagePath))
			{
				if (!EditorUtility.DisplayDialog(
					    "Overwrite Existing Package",
					    "A package with this name already exists in the project. Overwrite it?",
					    "Irreversibly Overwrite",
					    "Cancel"
				))
				{
					Close();
					return;
				}
				Directory.Delete(packagePath, true);
			}
			
			// Create the package.json file
			var packageJson = new JObject();
			packageJson["name"] = _packageName;
			packageJson["displayName"] = _packageDisplayName;
			packageJson["version"] = _packageVersion;
			packageJson["unity"] = "2019.4";
			File.WriteAllText(Path.Combine(_targetFolder, "package.json"), packageJson.ToString());
			
			// Move the folder to the Packages folder
			Directory.Move(_targetFolder, packagePath);
			
			// Delete the .meta file
			File.Delete(_targetFolder + ".meta");
			
			// Refresh the AssetDatabase
			AssetDatabase.Refresh();
			EditorUtility.RequestScriptReload();
			
			// Close the window
			Close();
			
			// If there are any scripts without an Assembly Definition, warn the user
			if (HasScriptsWithoutAssemblyDefinition(packagePath))
				EditorUtility.DisplayDialog(
					"Scripts Without Assembly Definitions",
					"Some scripts in this package do not have an Assembly Definition. " +
					"Unity will refuse to compile these scripts.",
					"Close"
				);
			
			// If there are any DLLs in the package, warn the user
			if (HasDlls(packagePath))
				EditorUtility.DisplayDialog(
					"Possible conflicts with DLLs",
					"Some DLLs were found in this package. " +
					"This may cause conflicts with other packages installed via VPM. " +
					"Consider moving these DLLs to a dedicated package.",
					"Close"
				);
			
			// Open the package.json editor
			PackageJsonEditorWindow.ShowWindow(_packageName);
		}

		private bool HasBlacklist(string path, out string[] extensions)
		{
			var files = Directory.GetFiles(path);
			var possibleExtensions = files.Select(Path.GetExtension).Distinct().ToArray();
			extensions = possibleExtensions.Where(x => ExtensionBlacklist.Contains(x)).ToArray();
			
			foreach (var directory in Directory.GetDirectories(path))
			{
				if (Path.GetFileName(directory) == "Resources") continue;
				if (HasBlacklist(directory, out var subExtensions))
					extensions = extensions.Concat(subExtensions).Distinct().ToArray();
			}
			
			return extensions.Length > 0;
		}
		
		private bool HasScriptsWithoutAssemblyDefinition(string path)
		{
			var files = Directory.GetFiles(path);
			if (files.Any(x => Path.GetExtension(x) == ".cs" && !File.Exists(Path.ChangeExtension(x, ".asmdef"))))
				return true;
			
			foreach (var directory in Directory.GetDirectories(path))
			{
				if (Path.GetFileName(directory) == "Resources") continue;
				if (HasScriptsWithoutAssemblyDefinition(directory))
					return true;
			}
			
			return false;
		}
		
		private bool HasDlls(string path)
		{
			var files = Directory.GetFiles(path);
			if (files.Any(x => Path.GetExtension(x) == ".dll"))
				return true;
			
			foreach (var directory in Directory.GetDirectories(path))
			{
				if (Path.GetFileName(directory) == "Resources") continue;
				if (HasDlls(directory))
					return true;
			}
			
			return false;
		}
	}
}