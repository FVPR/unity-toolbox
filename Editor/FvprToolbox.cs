using System;
using UnityEditor;
using UnityEngine;

namespace FVPR
{
	public static class FvprToolbox
	{
		public static string MakeColorString(string message, string color) =>
			$"<color={color}>{message}</color>";
		
		public static string MakeLogString(string origin, string message, string color = "white") =>
			$"<b>{MakeColorString("[", "grey")} {MakeColorString(origin, color)} {MakeColorString("]", "grey")}</b> {message}";

		public static void Centered(Action action)
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			action();
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}
	}
}