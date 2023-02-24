using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FVPR.Toolbox
{
	public class PackageSelectorPopup : EditorWindow
	{
		[Serializable]
		private struct PackageJson
		{
			public string displayName;
		}
		
		private static readonly Vector2 Size = new Vector2(400, 55);
		
		public static void ShowWindow(string message, Action<string> callback)
		{
			var window = GetWindow<PackageSelectorPopup>(true);
			window.titleContent = new GUIContent(message);
			window._callback = callback;
			window.minSize = Size;
			window.maxSize = Size;
			window.ShowModal();
		}
		
		private bool _firstFrame = true;
		private bool _doClose;
		private bool _doCallback;
		private Action<string> _callback;
		private string[] _packageDirs;
		private string[] _packageNames;
		private int _selectedPackageIndex;
		
		private void OnEnable()
		{
			// Get all directories in Packages
			var packageDirectories = Directory.GetDirectories("Packages").ToArray();
			var list = new List<string>();
			
			// If there is a package.json in the directory, read the displayName from it
			// Otherwise, use the directory name
			foreach (var packageDirectory in packageDirectories)
			{
				if (!File.Exists(Path.Combine(packageDirectory, "package.json")))
				{
					list.Add(packageDirectory);
					continue;
				}
				
				var packageJson = File.ReadAllText(Path.Combine(packageDirectory, "package.json"));
				try
				{
					var displayName = JsonUtility.FromJson<PackageJson>(packageJson).displayName;
					list.Add(displayName);
				}
				catch (Exception e)
				{
					Debug.LogError("Error parsing package.json in " + packageDirectory + ": " + e);
					list.Add(packageDirectory);
				}
			}

			_packageDirs = packageDirectories;
			_packageNames = list.ToArray();
			_selectedPackageIndex = 0;
		}
		
		private void OnGUI()
		{
			// Center window, only do this once
			if (_firstFrame)
			{
				_firstFrame = false;
				position = new Rect(
					(Screen.currentResolution.width - Size.x) / 2,
					(Screen.currentResolution.height - Size.y) / 2,
					Size.x,
					Size.y
				);
			}
			
			EditorGUILayout.Space();
			
			_selectedPackageIndex = EditorGUILayout.Popup(_selectedPackageIndex, _packageNames);

			EditorGUILayout.BeginHorizontal();
			{
				if (GUILayout.Button("Cancel")) _doClose = true;
				
				using (new EditorGUI.DisabledGroupScope(_selectedPackageIndex == -1))
				{
					if (GUILayout.Button("Select"))
					{
						_doClose = true;
						_doCallback = true;
					}
				}
			}
			EditorGUILayout.EndHorizontal();
			
			if (_doClose) Close();
			if (_doCallback)
			{
				try
				{
					_callback?.Invoke(Path.GetFileName(_packageDirs[_selectedPackageIndex]));
				}
				catch (Exception e)
				{
					Debug.LogError("Error in callback: " + e);
				}
			}
		}
	}
}