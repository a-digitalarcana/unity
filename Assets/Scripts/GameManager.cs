using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using UnityEngine;
using TMPro;
using BestHTTP;
using BestHTTP.SocketIO3;

#if UNITY_EDITOR
using ParrelSync;
#endif

[Serializable]
public class CardMapping
{
	public int id;
	public int value;
	public int token_id;
	public string ipfsUri;
}

[Serializable]
public class CardMetadata
{
	public string name;
	public string displayUri; // TODO: Use artifactUri, apply point sampling to low res assets
	public string set;
	public string minting;
	public string lot;
}

[Serializable]
public enum DeckMode
{
	Stacked,
	LinearFanRight,
	LinearFanLeft
}

[Serializable]
public class DeckEntry
{
	public Transform root, playerSpot;
	public DeckMode mode;
	public List<GameObject> cards = new List<GameObject>();
}

public class GameManager : MonoBehaviour
{
	public TMP_Text console;
	public TMP_InputField input;
	string userName;

	Camera src;

	public GameObject cardPrefab;

	public List<DeckEntry> decks;
	DeckEntry playerDeck;
	Transform handTransform;

	public GameObject purchase, pack;
	public Color purchaseColor, pendingColor;

	public GameObject statue;

	public Deck loaner;

	public float cardOffset = 0.03f;
	public float stackOffset = 0.000254f;

	public string serverUrl;
	SocketManager manager;
	public bool localhost; // set to true for local testing

	// Hardcoded accounts for testing in editor.
	public string primaryAccount = "tz1VobZNhZRTWpgVgf6uwsTtMUhEYtNB7r2x";
	public string secondaryAccount = "tz1SeLL2CwUuRoPMN9coLj983fPKgMCA1Tmh";

	public string ipfsHostUrl = "https://gateway.pinata.cloud/ipfs/";
	public float pinataRateLimit = 120; // https://docs.pinata.cloud/rate-limits

	// TODO: Persist across sessions
	class CachedMetadata
	{
		public CardMetadata metadata = null;
		public int failures = 0;
	}
	Dictionary<string, CachedMetadata> metadataCache = new Dictionary<string, CachedMetadata>(); // by mapping.ipfsUri
	Dictionary<int, CardMetadata> tokenMetadata = new Dictionary<int, CardMetadata>(); // by token_id

	Dictionary<int, CardMapping> mappings = new Dictionary<int, CardMapping>(); // by id

	LinkedList<string> pendingDownloads = new LinkedList<string>();

	Dictionary<string, bool> textureDownloaded = new Dictionary<string, bool>(); // by metadata.displayUri

	private string IpfsUriToUrl(string uri)
	{
		if (!uri.StartsWith("ipfs://"))
		{
			Debug.Log("invalid ipfs uri prefix: " + uri);
			return "";
		}

		var cid = uri.Substring(7); // Strip off ipfs://
		return ipfsHostUrl + cid;
	}

	[DllImport("__Internal")]
	private static extern void GetHostAddress();

	[DllImport("__Internal")]
	private static extern void GetWalletAddress();

	[DllImport("__Internal")]
	private static extern void BuyCardPack();

	[DllImport("__Internal")]
	private static extern void RefundCardPack();

	[DllImport("__Internal")]
	private static extern void OpenCardPack();

	private void Awake()
	{
		src = GetComponent<Camera>();
		handTransform = GetDeck("HandRoot").root;
	}

	private void Start()
	{
		//Davinci.ClearAllCachedFiles();

		if (localhost)
		{
			serverUrl = "http://localhost:8080";
		}

#if (UNITY_EDITOR)
		SetHostAddress(serverUrl);
#elif (UNITY_WEBGL)
		GetHostAddress(); // ask browser, will call SetHostAddress
#endif

		OnSetDrawPile("DeckA");
		DealLoanerDeck();
	}

	DeckEntry GetDeck(string name)
	{
		foreach (var deck in decks)
		{
			if (deck.root.name == name)
			{
				return deck;
			}
		}
		return null;
	}

	void DealLoanerDeck()
	{
		var indices = Deck.shuffledIndices;
		foreach (var i in indices)
		{
			var cardObject = AddCard(playerDeck);
			var card = cardObject.GetComponent<Card>();
			card.value = (Tarot.AllCards)i;
			card.token_id = -1;
			card.front.material.mainTexture = loaner.textures[i];
			card.back.material.mainTexture = loaner.back;
		}
	}
	void AddToDeck(DeckEntry entry, int[] ids)
	{
		foreach (var id in ids)
		{
			var cardObject = AddCard(entry);
			var card = cardObject.GetComponent<Card>();
			card.id = id;

			// look up mapping
			if (mappings.ContainsKey(id))
			{
				var mapping = mappings[id];
				card.token_id = mapping.token_id;
				card.value = (Tarot.AllCards)mapping.value;
				card.revealed = true;

				// Use loaner deck texture as fallback until downloaded.
				card.front.material.mainTexture = loaner.textures[mapping.value];

				CardMetadata metadata;
				if (tokenMetadata.TryGetValue(mapping.token_id, out metadata))
				{
					if (textureDownloaded.ContainsKey(metadata.displayUri))
					{
						var priority = Card.GetLotPriority(metadata.lot);
						Davinci.get()
							.load(metadata.displayUri)
							.withColor(Card.lotColors[priority])
							.into(card.front)
							.start();
					}
				}
			}
			else
			{
				card.token_id = -1;
				card.front.material.mainTexture = loaner.blank;
			}

			// TODO: Get back texture from pack (if available)
			card.back.material.mainTexture = loaner.back;
		}
	}

	public void SetHostAddress(string address)
	{
		Debug.Log("Connecting to " + address);

		SocketOptions options = new SocketOptions();
		options.AutoConnect = false;

		manager = new SocketManager(new Uri(address), options);
		manager.Socket.On(SocketIOEventTypes.Connect, OnConnected);
		manager.Socket.On<string>("beginGame", OnBeginGame);
		manager.Socket.On<string>("setDrawPile", OnSetDrawPile);
		manager.Socket.On<string, string>("newDeck", OnNewDeck);
		manager.Socket.On<string>("msg", OnMsg);
		manager.Socket.On<CardMapping[]>("revealCards", OnRevealCards);
		manager.Open();
	}

	void OnConnected()
	{
		Debug.Log("Connected to server!");

	#if (UNITY_EDITOR)
		SetWalletAddress((ClonesManager.GetArgument() == "client") ? secondaryAccount : primaryAccount);
	#elif (UNITY_WEBGL)
		GetWalletAddress(); // ask browser, will call SetWalletAddress
	#endif
	}

	public void SetWalletAddress(string address)
	{
		OnMsg("Connected wallet: " + address);
		manager.Socket.Emit("setWallet", address);
	}

	void RemoveAllCards()
	{
		foreach (var deck in decks)
		{
			RemoveAllCards(deck.cards);
		}
	}

	void RemoveAllCards(List<GameObject> cards)
	{
		foreach (var card in cards)
		{
			Destroy(card);
		}
		cards.Clear();
	}

	void RemoveFromDeck(List<GameObject> cards, int[] ids)
	{
		foreach (var id in ids)
		{
			foreach (var card in cards)
			{
				if (card.GetComponent<Card>().id == id)
				{
					cards.Remove(card);
					Destroy(card);
					break;
				}
			}
		}
	}

	bool playingOnlineGame = false;

	void OnBeginGame(string name)
	{
		RemoveAllCards();
		pendingDownloads.Clear();
		playingOnlineGame = true;
	}

	void OnSetDrawPile(string name)
	{
		playerDeck = GetDeck(name);
		if (playerDeck == null)
		{
			Debug.LogError("Cound not find draw pile: " + name);
			return;
		}

		if (playerDeck.playerSpot != null)
		{
			var root = transform.parent;
			root.SetParent(playerDeck.playerSpot, false);
		}
	}

	void OnNewDeck(string name, string key)
	{
		Debug.Log("NewDeck: " + name + " " + key);

		var deck = GetDeck(name);
		if (deck == null)
		{
			Debug.LogError("Unknown deck: " + name);
			return;
		}

		manager[key].On<int[]>("addCards", (int[] cards) => AddToDeck(deck, cards));
		manager[key].On<int[]>("removeCards", (int[] cards) => RemoveFromDeck(deck.cards, cards));
	}

	float beginPurchaseTime = 0.0f;
	float pendingOpenTime = 0.0f;

	void Update()
	{
		// Handle pack open server timeout
		if (pendingOpenTime > 0.0f && Time.realtimeSinceStartup > pendingOpenTime + 60.0f)
		{
			OnMsg("Server timed out, resetting pack state.");
			pack.GetComponent<Renderer>().material.color = purchaseColor;
			pendingOpenTime = 0.0f;
		}

		if (!Input.GetMouseButtonDown(0))
			return;

		var ray = src.ScreenPointToRay(Input.mousePosition);

		RaycastHit hit;
		if (!Physics.Raycast(ray, out hit))
			return;

		// Handle clicking on purchase tray
		if (hit.collider.gameObject == purchase && !pack.activeSelf)
		{
			if (beginPurchaseTime > 0.0f && Time.realtimeSinceStartup < beginPurchaseTime + 60.0f)
			{
				Debug.Log("Throttling purchase");
				return;
			}
			beginPurchaseTime = Time.realtimeSinceStartup;
			OnMsg("Initiating purchase...");
		#if (UNITY_EDITOR)
			OnBuyCardPack(1);
		#elif (UNITY_WEBGL)
			BuyCardPack(); // ask browser, will call OnBuyCardPack
		#endif
			return;
		}

		// Handle clicking on unopened pack
		if (hit.collider.gameObject == pack)
		{
			var material = pack.GetComponent<Renderer>().material;
			if (material.color == pendingColor)
			{
				Debug.Log("Throttling open");
				return;
			}
			pendingOpenTime = Time.realtimeSinceStartup;
			pack.GetComponent<Renderer>().material.color = pendingColor;
			OnMsg("Opening pack...");
		#if (UNITY_EDITOR)
			OnOpenCardPack(1);
		#elif (UNITY_WEBGL)
			OpenCardPack(); // ask browser, will call OnOpenCardPack
		#endif
			return;
		}

		// Handle clicking on statue
		if (hit.collider.gameObject == statue)
		{
			OnMsg("Play War Online");
			hit.collider.enabled = false;
			manager.Socket.Emit("playOnline", "War");
			return;
		}

		var card = hit.collider.GetComponentInParent<Card>();
		if (!card)
			return;

		// Handle clicking on player's deck
		if (playerDeck != null && card.transform.parent == playerDeck.root)
		{
			if (playingOnlineGame)
			{
				manager.Socket.Emit("drawCard");
				return;
			}

			var hand = GetDeck("HandRoot");
			if (hand.cards.Count < 24)
			{
				var topCard = playerDeck.cards.Last();
				topCard.transform.parent = hand.root;
				topCard.transform.localPosition = new Vector3(hand.cards.Count * cardOffset, 0, 0);
				topCard.transform.localRotation = Quaternion.identity;
				playerDeck.cards.Remove(topCard);
				hand.cards.Add(topCard);
			}
			return;
		}

		// Handle clicking on card in hand
		if (card.transform.parent == handTransform)
		{
			var pos = card.transform.localPosition;
			pos.y = (pos.y > 0) ? 0 : cardOffset;
			card.transform.localPosition = pos;
		}
	}

	public void OnBuyCardPack(int success)
	{
		OnMsg("OnBuyCardPack: " + success);
		beginPurchaseTime = 0.0f;
		if (success != 0)
		{
			pack.GetComponent<Renderer>().material.color = purchaseColor;
			pack.SetActive(true);
		}
	}

	public void OnRefundCardPack(int success)
	{
		OnMsg("OnRefundCardPack: " + success);
	}

	public void OnOpenCardPack(int success)
	{
		OnMsg("OnOpenCardPack: " + success);
		pack.SetActive(false);

		OnMsg("<Insert cool reveal here>");
		OnMsg("(For now you will have to refresh the page to download your new cards.)");
	}

	public void OnMsg(string msg)
	{
		Debug.Log(msg);
		console.text += '\n' + msg;
	}

	public void Collapse(List<GameObject> cards)
	{
		float x = 0;
		foreach (var card in cards)
		{
			var pos = card.transform.localPosition;
			pos.x = x;
			card.transform.localPosition = pos;
			x += cardOffset;
		}
	}

	GameObject AddCard(DeckEntry deck)
	{
		var newCard = Instantiate(cardPrefab, deck.root);
		switch (deck.mode)
		{
			case DeckMode.Stacked:
				newCard.transform.localPosition = new Vector3(0, 0, deck.cards.Count * stackOffset);
				break;
			case DeckMode.LinearFanRight:
				newCard.transform.localPosition = new Vector3(deck.cards.Count * cardOffset, 0, 0);
				break;
			case DeckMode.LinearFanLeft:
				newCard.transform.localPosition = new Vector3(deck.cards.Count * -cardOffset, 0, 0);
				break;
		}
		deck.cards.Add(newCard);
		return newCard;
	}

	public void OnEnterText(string text)
	{
		if (string.IsNullOrEmpty(input.text))
			return;

		Debug.Log(input.text);

		if (string.IsNullOrEmpty(userName))
		{
			userName = input.text;
			manager.Socket.Emit("userName", userName);
		}
		else
		{
			manager.Socket.Emit("chat", input.text);
		}

		// Reset field and maintain focus.
		input.text = "";
		input.ActivateInputField();
	}

	public void OnRevealCards(CardMapping[] mappings)
	{
		bool isFetching = pendingDownloads.First != null;

		foreach (var mapping in mappings)
		{
			// TODO: Optimize
			foreach (var deck in decks)
			{
				foreach (var cardObj in deck.cards)
				{
					var card = cardObj.GetComponent<Card>();
					if (card.id == mapping.id && !card.revealed)
					{
						card.revealed = true;
						card.value = (Tarot.AllCards)mapping.value;
						card.token_id = mapping.token_id;

						// Use loaner deck texture as fallback until downloaded.
						card.front.material.mainTexture = loaner.textures[mapping.value];

						CardMetadata metadata;
						if (tokenMetadata.TryGetValue(mapping.token_id, out metadata))
						{
							if (textureDownloaded.ContainsKey(metadata.displayUri))
							{
								var priority = Card.GetLotPriority(metadata.lot);
								Davinci.get()
									.load(metadata.displayUri)
									.withColor(Card.lotColors[priority])
									.into(card.front)
									.start();
							}
						}


					}
				}
			}

			mapping.ipfsUri = IpfsUriToUrl(mapping.ipfsUri);
			this.mappings[mapping.id] = mapping;
			pendingDownloads.AddLast(mapping.ipfsUri);
		}

		if (!isFetching)
		{
			StartCoroutine(FetchCardInfo());
		}
	}

	IEnumerator FetchCardInfo()
	{
		var deltaTime = 60.0f / pinataRateLimit;

		while (pendingDownloads.First != null)
		{
			var url = pendingDownloads.First.Value;
			pendingDownloads.RemoveFirst();
			var done = pendingDownloads.First == null;

			if (Path.GetExtension(url) == ".json")
			{
				if (metadataCache.ContainsKey(url))
				{
					if (done) break;
					else continue;
				}
				metadataCache[url] = new CachedMetadata();

				var request = new HTTPRequest(new Uri(url), OnCardInfo);
				request.Send();
				//Debug.Log("Fetching: " + url);
			}
			else
			{
				var result = Davinci.get()
					.load(url)
					.withDownloadedAction(() => OnTextureDownloaded(url))
					.start();

				if (result != Davinci.StartResult.fetching)
				{
					if (done) break;
					else continue;
				}

				//Debug.Log("Fetching: " + url);
			}

			if (done) break;
			else yield return new WaitForSecondsRealtime(deltaTime);
		}

		//Debug.Log("Done!");
	}

	void OnCardInfo(HTTPRequest request, HTTPResponse response)
	{
		var url = request.Uri.OriginalString;
		Debug.Assert(metadataCache.ContainsKey(url));

		var text = response.DataAsText;
		if (text.Length < 1 || text[0] != '{')
		{
			if (++metadataCache[url].failures < 10)
			{
				Debug.Log("Retrying: " + url + "\n" + text);
				pendingDownloads.AddLast(url);
			}
			else
			{
				Debug.Log("Aborting after " + metadataCache[url].failures + " attempts\n" + url + "\n" + text);
			}
		}
		else
		{
			var metadata = JsonUtility.FromJson<CardMetadata>(response.DataAsText);
			metadataCache[url].metadata = metadata;

			// Map token_ids to metadata
			foreach (var mapping in mappings.Values)
			{
				if (mapping.ipfsUri == url)
				{
					Debug.Assert(!tokenMetadata.ContainsKey(mapping.token_id));
					tokenMetadata[mapping.token_id] = metadata;
					break;
				}
			}

			// Download artifact texture
			metadata.displayUri = IpfsUriToUrl(metadata.displayUri);
			pendingDownloads.AddFirst(metadata.displayUri);
		}
	}
	void OnTextureDownloaded(string url)
	{
		textureDownloaded[url] = true;
		foreach (var deck in decks)
		{
			UpgradeCards(deck.cards, url);
		}
	}

	void UpgradeCards(List<GameObject> cards, string textureUrl)
	{
		foreach (var cardObject in cards)
		{
			var card = cardObject.GetComponent<Card>();

			foreach (var mapping in mappings.Values)
			{
				var value = Tarot.Utils.GetValue(mapping.token_id);
				if (card.value != value)
					continue;

				CardMetadata metadata;
				if (!tokenMetadata.TryGetValue(mapping.token_id, out metadata))
					continue;

				if (metadata.displayUri != textureUrl)
					continue;

				var priority = Card.GetLotPriority(metadata.lot);

				Debug.Assert(card.token_id < 0 || tokenMetadata.ContainsKey(card.token_id));
				var lot = (card.token_id < 0) ? "" : tokenMetadata[card.token_id].lot;
				if (priority <= Card.GetLotPriority(lot))
					continue;

				//Debug.Log("Upgrading " + card.value + "(" + card.token_id + ") to " + metadata.lot + "(" + mapping.token_id + ")");
				card.token_id = mapping.token_id;
				Davinci.get()
					.load(textureUrl)
					.withColor(Card.lotColors[priority])
					.into(card.front)
					.start();
			}
		}
	}
}

