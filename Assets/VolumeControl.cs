using UnityEngine;

public class VolumeControl : MonoBehaviour
{
	public float exp = 1.0f;

	new Animation animation;
	new AudioSource audio;
	new BoxCollider collider;

	float clickPos = 0.0f;
	float clickVol = 0.0f;
	float dragRange = 0.0f;

	private void Awake()
	{
		animation = GetComponent<Animation>();
		audio = GetComponent<AudioSource>();
		collider = GetComponent<BoxCollider>();

		SetVolume((float)System.Math.Pow(audio.volume, 1.0 / exp));
	}

	public void SetVolume( float pct )
	{
		animation.Play("PianoVolume");
		var anim = animation["PianoVolume"];
		anim.speed = 0;
		anim.normalizedTime = pct;

		audio.volume = (float)System.Math.Pow(pct, exp);
	}

	public void OnClick(Camera cam)
	{
		clickPos = Input.mousePosition.y;
		clickVol = (float)System.Math.Pow(audio.volume, 1.0/exp);

		var minPos = cam.WorldToScreenPoint(collider.bounds.min);
		var maxPos = cam.WorldToScreenPoint(collider.bounds.max);
		dragRange = System.Math.Abs(maxPos.y - minPos.y) * 0.5f;
	}

	private void Update()
	{
		if (dragRange > 0.0f)
		{
			var dragPos = Input.mousePosition.y;
			var offset = (dragPos - clickPos) / dragRange;
			SetVolume(clickVol + offset);

			if (!Input.GetMouseButton(0))
			{
				dragRange = 0.0f;
			}
		}
	}
}
