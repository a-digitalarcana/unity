using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public static class IListExtensions
{
	public static void Shuffle<T>(this IList<T> list)
	{
		var count = list.Count;
		var last = count - 1;
		for (var i = 0; i < last; ++i)
		{
			var n = UnityEngine.Random.Range(i, count);
			var tmp = list[i];
			list[i] = list[n];
			list[n] = tmp;
		}
	}
}

[CreateAssetMenu(fileName = "Deck", menuName = "Digital Arcana/Create Deck", order = 1)]
public class Deck : ScriptableObject
{
	public Texture2D[] textures;
	public Texture2D back, blank;

	static int[] _indices = Enumerable.Range(0, Tarot.Utils.TotalCards).ToArray();
	public static int[] shuffledIndices
	{
		get
		{
			_indices.Shuffle();
			return _indices;
		}
	}

#if UNITY_EDITOR
	[MenuItem("CONTEXT/Deck/Populate Textures")]
	static void ValidateScene(MenuCommand command)
	{
		var deck = command.context as Deck;
		Debug.Log(deck);

		var path = Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(deck)), deck.name);
		Debug.Log(path);

		var cardNames = Enum.GetNames(typeof(Tarot.AllCards));
		deck.textures = new Texture2D[cardNames.Length];

		var i = 0;
		foreach (string name in cardNames)
		{
			var texturePath = Path.Combine(path, name + ".png");
			Debug.Log(texturePath);

			var texture = AssetDatabase.LoadAssetAtPath< Texture2D >(texturePath);
			deck.textures.SetValue(texture, i++);
		}
	}
#endif
}
