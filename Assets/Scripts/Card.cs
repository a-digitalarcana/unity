using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Card : MonoBehaviour
{
	public Tarot.AllCards value;
	public int token_id; // -1 if loaner
	public Renderer front, back;

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
