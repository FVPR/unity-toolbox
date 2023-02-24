using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace FVPR.Toolbox
{
	public class PackageJsonEditorWindow : EditorWindow
	{
		[MenuItem("FVPR/Edit Package Manifest", false, 605)]
		private static void ShowWindow() => PackageSelectorPopup.ShowWindow(
			"Select package to edit the manifest of",
			ShowWindow
		);
		
		private static readonly string[] AutoAddStrings = {
			"name",
			"version",
			"description",
			"category",
			"type",
			"license",
			"licensesUrl",
			"documentationUrl",
			"changelogUrl",
			"vrchatVersion",
		};
		private static readonly string[] AutoAddObjects = {
			"dependencies",
			"gitDependencies",
			"vpmDependencies",
			"legacyFolders",
			"legacyFiles",
		};

		internal static void ShowWindow(string packageName)
		{
			// Check if package exists
			if (!Directory.Exists(Path.Combine("Packages", packageName)))
			{
				Debug.LogError($"Package {packageName} does not exist");
				return;
			}
			
			// Dropdown to select package
			var window = GetWindow<PackageJsonEditorWindow>(true);
			window.titleContent = new GUIContent("Edit Package Manifest");
			window.minSize = new Vector2(450, 600);
			window._packageName = packageName;
			window.ShowModal();
		}
		
		private bool _doSave;
		private bool _doClose;
		private bool _isValid;
		private string _packageName;
		private JObject _packageJson;
		private Vector2 _scrollPosition;
		private bool _isFirstFrame = true;
		private bool _showUnmanagedProperties;
		
		private void FirstFrame()
		{
			if (!_isFirstFrame) return;
			_isFirstFrame = false;
			
			// Load package.json
			var path = Path.Combine("Packages", _packageName, "package.json");
			if (!File.Exists(path))
			{
				_packageJson = new JObject
				{
					["name"] = _packageName,
					["version"] = "1.0.0",
					["displayName"] = "",
					["description"] = "",
					["unity"] = "2019.4",
					["keywords"] = new JArray(),
					["dependencies"] = new JObject(),
					["gitDependencies"] = new JObject(),
					["vpmDependencies"] = new JObject(),
					["author"] = new JObject
					{
						["name"] = "",
						["email"] = "",
						["url"] = ""
					},
					["legacyFiles"] = new JObject(),
					["legacyFolders"] = new JObject(),
					["hideInEditor"] = true,
				};
			}
			else
			{
				try
				{
					_packageJson = JObject.Parse(File.ReadAllText(path));
				}
				catch (Exception e)
				{
					Debug.LogError($"Failed to parse {path}: {e.Message}");
					Close();
				}
				
				// Add any missing properties
				foreach (var key in AutoAddStrings)
					if (_packageJson[key] == null)
						_packageJson[key] = "";
				foreach (var key in AutoAddObjects)
					if (_packageJson[key] == null)
						_packageJson[key] = new JObject();
				if (_packageJson["keywords"] == null)
					_packageJson["keywords"] = new JArray();
				if (_packageJson["hideInEditor"] == null)
					_packageJson["hideInEditor"] = true;
				if (_packageJson["author"] == null)
					_packageJson["author"] = new JObject
					{
						["name"] = "",
						["email"] = "",
						["url"] = ""
					};
			}
		}

		private void OnGUI()
		{
			FirstFrame();
			
			_scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
				DrawJObject();
			EditorGUILayout.EndScrollView();

			EditorGUILayout.Separator();
			
			EditorGUILayout.BeginHorizontal();
			{
				if (Button("Cancel") && EditorUtility.DisplayDialog(
					"Package.json Editor",
					"Are you sure you want to cancel? All changes will be lost permanently.",
					"Yes, undo all changes",
					"No, keep editing"
				))
					_doClose = true;
				
				GUI.enabled = _isValid;
				if (Button("Save") && EditorUtility.DisplayDialog(
					"Package.json Editor",
					"Override the existing package.json file with the changes you made?\n\n" +
					"This action cannot be undone.",
					"Yes, override package.json",
					"No, keep editing"
				))
				{
					_doSave = true;
					_doClose = true;
				}
				GUI.enabled = true;
			}
			EditorGUILayout.EndHorizontal();
			
			if (_doClose) Close();
			if (_doSave)
			{
				RemoveDefaults();
				File.WriteAllText(Path.Combine("Packages", _packageName, "package.json"), _packageJson.ToString());
			}
		}

		private void RemoveDefaults()
		{
			var keys = _packageJson.Properties().Select(p => p.Name).ToList();
			foreach (var key in keys)
			{
				var value = _packageJson[key];
				if (value == null) continue; // Does nothing, but just in case
				switch (value.Type)
				{
					case JTokenType.String:
						if (string.IsNullOrWhiteSpace(value.Value<string>()))
							_packageJson.Remove(key);
						break;
					
					case JTokenType.Boolean:
						if (!value.Value<bool>())
							_packageJson.Remove(key);
						break;
					
					case JTokenType.Integer:
						if (value.Value<int>() == 0)
							_packageJson.Remove(key);
						break;
					
					case JTokenType.Float:
						if (value.Value<float>() == 0)
							_packageJson.Remove(key);
						break;
					
					case JTokenType.Null:
						_packageJson.Remove(key);
						break;
					
					case JTokenType.Object:
						if (!value.Children().Any())
							_packageJson.Remove(key);
						break;
					
					case JTokenType.Array:
						if (!value.Children().Any())
							_packageJson.Remove(key);
						break;
				}
			}
			
			// Remove author email / url if they are empty
			if (_packageJson["author"] is JObject author)
			{
				if (string.IsNullOrWhiteSpace(author["email"]?.Value<string>()))
					author.Remove("email");
				if (string.IsNullOrWhiteSpace(author["url"]?.Value<string>()))
					author.Remove("url");
				if (!author.Properties().Any())
					_packageJson.Remove("author");
			}
		}

		private void DrawJObject()
		{
			_isValid = true;
			string removeProperty = null;
			var unmangedProperties = new List<JProperty>();
			
			// Draw all properties
			foreach (var property in _packageJson.Properties())
			{
				switch (property.Name)
				{
					// name, version, displayName, description, unity, keywords, dependencies, gitDependencies, vpmDependencies, author, legacyFiles, legacyFolders, hideInEditor
					
					case "name":
						if (PropertyMustBeX(property, JTokenType.String))
						{
							JObjectGUI.DrawStringProperty(_packageJson, property);
							if (!ValidationUtils.IsPackageNameValid(property.Value.ToString(), out var errors))
							{
								_isValid = false;
								EditorGUI.indentLevel++;
								{
									EditorGUILayout.HelpBox(
										"Invalid package name",
										MessageType.Error
									);
									foreach (var error in errors)
										EditorGUILayout.HelpBox(error, MessageType.None);
								}
								EditorGUI.indentLevel--;
							}
						}
						break;
					
					case "version":
						if (PropertyMustBeX(property, JTokenType.String))
						{
							JObjectGUI.DrawStringProperty(_packageJson, property);
							if (!ValidationUtils.IsPackageVersionValid(property.Value.ToString()))
							{
								_isValid = false;
								EditorGUILayout.HelpBox(
									"Invalid semantic version",
									MessageType.Error
								);
							}
						}
						break;
					
					case "displayName":
						if (PropertyMustBeX(property, JTokenType.String))
							JObjectGUI.DrawStringProperty(_packageJson, property);
						break;
					
					case "description":
						if (PropertyMustBeX(property, JTokenType.String))
							JObjectGUI.DrawStringProperty(_packageJson, property);
						break;

					case "unity":
						if (property.Value.ToString() != "2019.4")
						{
							property.Value = "2019.4"; 
							Debug.LogWarning(
								$"Only Unity 2019.4 is supported. " +
								$"The value from {_packageName}/package.json has been reset from [{property.Value}] to [2019.4]."
							);
						}
						GUI.enabled = false;
						EditorGUILayout.TextField(
							new GUIContent("Unity", "Only [2019.4] is supported."),
							"2019.4"
						);
						GUI.enabled = true;
						break;
					
					case "keywords":
						if (PropertyMustBeX(property, JTokenType.Array))
						{
							EditorGUILayout.LabelField("Keywords");
							EditorGUI.indentLevel++;
							{
								var array = (JArray)property.Value;
								int toRemove = -1;
								for (var i = 0; i < array.Count; i++)
								{
									var keyword = array[i];
									if (keyword.Type != JTokenType.String)
									{
										_isValid = false;
										EditorGUILayout.HelpBox(
											$"The value of the property [{property.Name}] must be an array of strings.",
											MessageType.Error
										);
										if (GUILayout.Button("Fix"))
											toRemove = i;
										_isValid = false;
									}
									else
									{
										EditorGUILayout.BeginHorizontal();
										{
											JObjectGUI.DrawStringElement(array, i);
											if (GUILayout.Button("Remove"))
												toRemove = i;
										}
										EditorGUILayout.EndHorizontal();
									}
								}

								if (Button("Add keyword"))
									array.Add("");
								if (toRemove != -1)
									array.RemoveAt(toRemove);
							}
							EditorGUI.indentLevel--;
						}
						break;
					
					case "dependencies":
					case "gitDependencies":
					case "vpmDependencies":
						if (PropertyMustBeX(property, JTokenType.Object))
							DrawDependencies(property);
						break;
					
					case "author":
						if (PropertyMustBeX(property, JTokenType.Object))
						{
							var obj = (JObject) property.Value;
							var name = obj.Property("name");
							var email = obj.Property("email");
							var url = obj.Property("url");
							
							#region Check if the required properties exist
							// Make sure the name property exists
							if (name == null)
							{
								obj.Add("name", "");
								name = obj.Property("name");
							}
							// Make sure the email property exists
							if (email == null)
							{
								obj.Add("email", "");
								email = obj.Property("email");
							}
							// Make sure the url property exists
							if (url == null)
							{
								obj.Add("url", "");
								url = obj.Property("url");
							}
							#endregion
							
							#region Remove any extra properties
							for (var i = 0; i < obj.Properties().Count(); i++)
							{
								var prop = obj.Properties().ElementAt(i);
								if (prop.Name != "name" && prop.Name != "email" && prop.Name != "url")
								{
									Debug.LogWarning($"The invalid property [{prop.Name}] has been removed from the [author] object.");
									// RemoveAt
									obj.Remove(prop.Name);
									i--;
								}
							}
							#endregion
							
							#region Draw the properties
							
							EditorGUILayout.LabelField("Author");
							EditorGUI.indentLevel++;
							{
								if (TokenMustBeX(obj, "name", JTokenType.String))
								{
									JObjectGUI.DrawStringProperty(obj, name);
									if (string.IsNullOrEmpty(name.Value.ToString()))
									{
										_isValid = false;
										EditorGUILayout.HelpBox(
											"The [name] property of the [author] object cannot be empty.",
											MessageType.Error
										);
									}
								}
								if (TokenMustBeX(obj, "email", JTokenType.String))
									JObjectGUI.DrawStringProperty(obj, email);
								if (TokenMustBeX(obj, "url", JTokenType.String))
									JObjectGUI.DrawStringProperty(obj, url);
							}
							EditorGUI.indentLevel--;

							#endregion
							
							property.Value = obj;
						}
						break;
						
					// "legacyFolders" : {
					//		"Assets\\FolderName" : "vr031f928e5c709x9887f6513084aaa51"
					// },
					// "legacyFiles" : {
					// 		"ProjectVersion.txt" : "jf988739jfdskljf098323jjhf"
					// }

					case "legacyFolders":
					case "legacyFiles":
						if (PropertyMustBeX(property, JTokenType.Object))
							DrawLegacy(property);
						break;
					
					case "hideInEditor":
						if (PropertyMustBeX(property, JTokenType.Boolean))
							JObjectGUI.DrawBooleanProperty(_packageJson, property);
						break;

					case "isDeprecated":
					case "deprecationMessage":
						if (PropertyMustBeX(property, JTokenType.String))
						{
							JObjectGUI.DrawStringProperty(_packageJson, property);
							EditorGUILayout.BeginHorizontal();
							{
								EditorGUILayout.HelpBox(
									$"The [{property.Name}] property will prevent the package from being published to FVPR. Use the online dashboard to deprecate a package.",
									MessageType.Warning
								);
								if (GUILayout.Button("Remove Property"))
									removeProperty = property.Name;
							}
							EditorGUILayout.EndHorizontal();
						}
						break;
					
					// Must be string: "license", "licensesUrl", "category", "type", "documentationUrl", "changelogUrl", "vrchatVersion"
					case "license":
					case "licensesUrl":
					case "category":
					case "type":
					case "documentationUrl":
					case "changelogUrl":
					case "vrchatVersion":
						if (PropertyMustBeX(property, JTokenType.String))
							JObjectGUI.DrawStringProperty(_packageJson, property);
						break;
					
					// If the property is url or repo, warn the user that it will be changed upon publishing to FVPR
					case "url":
					case "repo":
						if (PropertyMustBeX(property, JTokenType.String))
						{
							if (string.IsNullOrEmpty(property.Value.ToString())) break;
							JObjectGUI.DrawStringProperty(_packageJson, property);
							EditorGUILayout.BeginHorizontal();
							{
								EditorGUILayout.HelpBox(
									$"The [{property.Name}] property will be changed server-side upon publishing to FVPR.",
									MessageType.Warning
								);
								if (GUILayout.Button("Remove Property"))
									removeProperty = property.Name;
							}
							EditorGUILayout.EndHorizontal();
						}
						break;

					default:
						unmangedProperties.Add(property);
						break;
				}
			}
			
			// Draw the unmanged properties
			if (unmangedProperties.Count > 0)
			{
				EditorGUILayout.Separator();
				EditorGUILayout.HelpBox(
					"The following properties are not recognized by FVPR! They will still be included in the package.json file, but wont get validated.",
					MessageType.Info
				);
				_showUnmanagedProperties = EditorGUILayout.Foldout(
					_showUnmanagedProperties,
					"Other Properties",
					true
				);
				if (_showUnmanagedProperties)
				{
					EditorGUI.indentLevel++;
					foreach (var property in unmangedProperties)
						JObjectGUI.DrawProperty(_packageJson, property);
					EditorGUI.indentLevel--;
				}
			}

			// Remove the property if the user clicked the button
			if (removeProperty != null)
				_packageJson.Remove(removeProperty);
		}
		
		private void DrawLegacy(JProperty legacy)
		{
			switch (legacy.Name)
			{
				case "legacyFolders":
					EditorGUILayout.LabelField("Legacy Folders");
					break;
				case "legacyFiles":
					EditorGUILayout.LabelField("Legacy Files");
					break;
			}
			
			var obj = (JObject) legacy.Value;
			EditorGUI.indentLevel++;
			{
				// Don't validate in the loop, another function will do it later
				int toRemove = -1;
				for (var i = 0; i < obj.Properties().Count(); i++)
				{
					var property = obj.Properties().ElementAt(i);
					if (property.Value.Type != JTokenType.String)
					{
						EditorGUILayout.HelpBox(
							$"The value of the property [{property.Name}] must be a string.",
							MessageType.Error
						);
						if (GUILayout.Button("Fix"))
							toRemove = i;
						_isValid = false;
					}
					else
					{
						EditorGUILayout.BeginHorizontal();
						{
							var newValue = EditorGUILayout.TextField(property.Name);
							if (newValue != property.Name)
							{
								obj.Add(newValue, property.Value);
								toRemove = i;
							}

							var newGuid = EditorGUILayout.TextField(property.Value.ToString());
							if (newGuid != property.Value.ToString())
							{
								obj.Remove(property.Name);
								obj.Add(property.Name, newGuid);
							}

							if (GUILayout.Button("-"))
								toRemove = i;
						}
						EditorGUILayout.EndHorizontal();
					}
				}

				if (Button("Add legacy folder"))
					obj.Add("", "");
				if (toRemove != -1)
					obj.Remove(obj.Properties().ElementAt(toRemove).Name);

				var errors = ValidationUtils.CheckLegacy(_packageJson, legacy.Name);
				foreach (var error in errors)
					EditorGUILayout.HelpBox(error, MessageType.Error);
			}
			EditorGUI.indentLevel--;
		}
		
		private void DrawDependencies(JProperty dependencies)
		{
			switch (dependencies.Name)
			{
				case "dependencies":
					EditorGUILayout.LabelField("Dependencies");
					break;
				case "gitDependencies":
					EditorGUILayout.LabelField("Git Dependencies");
					break;
				case "vpmDependencies":
					EditorGUILayout.LabelField("VPM Dependencies");
					break;
			}
			
			var obj = (JObject) dependencies.Value;
			EditorGUI.indentLevel++;
			{
				// Don't validate in the loop, another function will do it later
				int toRemove = -1;
				for (var i = 0; i < obj.Properties().Count(); i++)
				{
					var property = obj.Properties().ElementAt(i);
					if (property.Value.Type != JTokenType.String)
					{
						EditorGUILayout.BeginHorizontal();
						{
							EditorGUILayout.HelpBox(
								$"The value of the property [{property.Name}] must be a string.",
								MessageType.Error
							);
							if (GUILayout.Button("Fix"))
								property.Value = "";
						}
						EditorGUILayout.EndHorizontal();
					}
					else
					{
						EditorGUILayout.BeginHorizontal();
						{
							var newValue = EditorGUILayout.TextField(property.Name);
							if (newValue != property.Name)
							{
								obj.Add(newValue, property.Value);
								toRemove = i;
							}

							newValue = EditorGUILayout.TextField(property.Value.ToString());
							if (newValue != property.Value.ToString())
								property.Value = newValue;

							if (GUILayout.Button("-"))
								toRemove = i;
						}
						EditorGUILayout.EndHorizontal();
					}
				}

				if (Button("Add dependency"))
					obj.Add("", "");

				if (toRemove != -1)
					obj.Remove(obj.Properties().ElementAt(toRemove).Name);

				var errors = ValidationUtils.CheckDependencies(_packageJson, dependencies.Name);
				foreach (var error in errors)
					EditorGUILayout.HelpBox(error, MessageType.Error);
			}
			EditorGUI.indentLevel--;
		}
		
		private bool PropertyMustBeX(JProperty property, JTokenType type)
		{
			if (property.Value.Type != type)
			{
				EditorGUILayout.BeginHorizontal();
				{
					EditorGUILayout.HelpBox(
						$"The value of the property [{property.Name}] must be a {type}.",
						MessageType.Error
					);
					if (GUILayout.Button("Fix"))
					{
						switch (type)
						{
							case JTokenType.String:
								property.Value = "";
								break;
							
							case JTokenType.Boolean:
								property.Value = false;
								break;
							
							case JTokenType.Integer:
								property.Value = 0;
								break;
							
							case JTokenType.Float:
								property.Value = 0f;
								break;
							
							case JTokenType.Array:
								property.Value = new JArray();
								break;
							
							case JTokenType.Object:
								property.Value = new JObject();
								break;
							
							default:
								throw new ArgumentOutOfRangeException(nameof(type), type, null);
						}
					}
				}
				EditorGUILayout.EndHorizontal();
				_isValid = false;
				return false;
			}
			return true;
		}
		
		private bool TokenMustBeX(JObject obj, string name, JTokenType type)
		{
			var token = obj[name];
			if (token == null)
			{
				EditorGUILayout.BeginHorizontal();
				{
					EditorGUILayout.HelpBox(
						$"The property [{name}] must be a {type}.",
						MessageType.Error
					);
					if (GUILayout.Button("Fix"))
					{
						switch (type)
						{
							case JTokenType.String:
								obj.Add(name, "");
								break;
							
							case JTokenType.Boolean:
								obj.Add(name, false);
								break;
							
							case JTokenType.Integer:
								obj.Add(name, 0);
								break;
							
							case JTokenType.Float:
								obj.Add(name, 0f);
								break;
							
							case JTokenType.Array:
								obj.Add(name, new JArray());
								break;
							
							case JTokenType.Object:
								obj.Add(name, new JObject());
								break;
							
							default:
								throw new ArgumentOutOfRangeException(nameof(type), type, null);
						}
					}
				}
				EditorGUILayout.EndHorizontal();
				_isValid = false;
				return false;
			}
			return true;
		}

		private bool Button(string text)
		{
			var rect = EditorGUILayout.GetControlRect();
			rect.x += EditorGUI.indentLevel * 15;
			rect.width -= EditorGUI.indentLevel * 15;
			return GUI.Button(rect, text);
		}
	}
}