using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NabaGame.Core.Editor.Utils
{
	public static class PrefabExtention
	{
		[MenuItem("GameObject/Apply Prefabs", false, 0)]
		[MenuItem("MyTool/Apply Prefabs %#z")]
		public static void ApplyPrefab()
		{
			List<GameObject> prefabs = new List<GameObject>();
			foreach (var gameObject in Selection.gameObjects)
			{
				var rootPrefab = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
				if (!prefabs.Contains(rootPrefab))
					prefabs.Add(rootPrefab);
			}

			foreach (var prefab in prefabs)
			{
				PrefabUtility.SaveAsPrefabAssetAndConnect(prefab,
					PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefab), InteractionMode.AutomatedAction);
			}
		}

		[MenuItem("MyTool/Clear Prefs %q")]
		public static void ClearPrefs()
		{
			Debug.Log("<color=cyan>=>" + "Clear Prefs" + "</color>");
			PlayerPrefs.DeleteAll();
			PlayerPrefs.Save();
		}
	}
}