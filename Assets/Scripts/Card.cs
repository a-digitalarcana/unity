using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Card : MonoBehaviour
{
	public Tarot.AllCards value;
	public int token_id; // -1 if loaner
	public Renderer front, back;

	public static Color[] lotColors = new Color[]
	{
		Color.white,
		Color.white,
		new Color(1.0f, 0.84f, 0.086f),	// 2:gold
		new Color(0.45f, 0.35f, 1.0f),	// 3:purple
		new Color(0.38f, 0.86f, 1.0f),	// 4:blue
	};

	public static int GetLotPriority(string lot)
	{
		switch (lot)
		{
			case "spdp": return 4;
			case "eifd": return 3;
			case "lnuy": return 2;
			case "hrgl": return 1;
		}

		return 0;
	}
}
