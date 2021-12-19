using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
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

public class GameManager : MonoBehaviour
{
	public TMP_Text console;
	public TMP_InputField input;
	string userName;

	Camera src;

	public GameObject cardPrefab;
	public GameObject handRoot;
	List<GameObject> cards = new List<GameObject>();

	public GameObject deckRoot;
	public GameObject deckA;
	public GameObject deckB;
	Transform playerDeckRoot;
	Transform otherDeckRoot;
	List<GameObject> deck = new List<GameObject>();
	List<GameObject> otherDeck = new List<GameObject>();

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

	private void Awake()
	{
		src = GetComponent<Camera>();
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

		OnIsPlayerA();
		DealLoanerDeck();
	}

	void DealLoanerDeck()
	{
		var indices = Deck.shuffledIndices;
		foreach (var i in indices)
		{
			var cardObject = AddCard(playerDeckRoot, deck);
			var card = cardObject.GetComponent<Card>();
			card.value = (Tarot.AllCards)i;
			card.token_id = -1;
			card.front.material.mainTexture = loaner.textures[i];
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
		manager.Socket.On("isPlayerA", OnIsPlayerA);
		manager.Socket.On("isPlayerB", OnIsPlayerB);
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

		//var key = address + ".owned";
		//manager[key].On("addCards");
		//manager[key].On("removeCards");
	}

	void SetPlayerTransform(float scale)
	{
		var t = transform.parent;
		var pos = t.localPosition;
		var temp = pos.x;
		pos.x = pos.z * scale;
		pos.z = temp;
		t.localPosition = pos;
		var rot = t.localEulerAngles;
		rot.y += 90 * scale;
		t.localEulerAngles = rot;
	}

	void OnIsPlayerA()
	{
		SetPlayerTransform(1.0f);
		playerDeckRoot = deckA.transform;
		otherDeckRoot = deckB.transform;
	}

	void OnIsPlayerB()
	{
		SetPlayerTransform(-1.0f);
		playerDeckRoot = deckB.transform;
		otherDeckRoot = deckA.transform;
	}

	void Update()
	{
		if (!Input.GetMouseButtonDown(0))
			return;

		var ray = src.ScreenPointToRay(Input.mousePosition);

		RaycastHit hit;
		if (!Physics.Raycast(ray, out hit))
			return;

		var card = hit.collider.GetComponentInParent<Card>();
		if (!card)
			return;

		if (card.transform.parent == playerDeckRoot)
		{
			if (cards.Count < 24)
			{
				var topCard = deck.Last();
				topCard.transform.parent = handRoot.transform;
				topCard.transform.localPosition = new Vector3(cards.Count * cardOffset, 0, 0);
				topCard.transform.localRotation = Quaternion.identity;
				deck.Remove(topCard);
				cards.Add(topCard);
			}
			return;
		}

		var pos = card.transform.localPosition;
		pos.y = (pos.y > 0) ? 0 : cardOffset;
		card.transform.localPosition = pos;
	}

	void OnMsg(string msg)
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

	GameObject AddCard(Transform root, List<GameObject> list)
	{
		var newCard = Instantiate(cardPrefab, root);
		if (root == playerDeckRoot || root == otherDeckRoot)
		{
			newCard.transform.localPosition = new Vector3(0, 0, list.Count * stackOffset);
		}
		else
		{
			newCard.transform.localPosition = new Vector3(list.Count * cardOffset, 0, 0);
		}
		list.Add(newCard);
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

		while (true)
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
				Debug.Log("Fetching: " + url);
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

				Debug.Log("Fetching: " + url);
			}

			if (done) break;
			else yield return new WaitForSecondsRealtime(deltaTime);
		}

		Debug.Log("Done!");
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
		UpgradeCards(cards, url);
		UpgradeCards(deck, url);
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

				Debug.Assert(card.token_id < 0 || tokenMetadata.ContainsKey(card.token_id));
				var lot = (card.token_id < 0) ? "" : tokenMetadata[card.token_id].lot;
				if (Card.GetLotPriority(metadata.lot) <= Card.GetLotPriority(lot))
					continue;

				Debug.Log("Upgrading " + card.value + "(" + card.token_id + ") to " + metadata.lot + "(" + mapping.token_id + ")");
				card.token_id = mapping.token_id;
				Davinci.get()
					.load(textureUrl)
					.into(card.front)
					.start();
			}
		}
	}
}

