using UnityEditor;
using UnityEngine;
using System;
using System.IO;

[CreateAssetMenu(fileName = "Deck", menuName = "Digital Arcana/Create Deck", order = 1)]
public class Deck : ScriptableObject
{
	public Texture2D[] textures;
	public Texture2D back, blank;

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
