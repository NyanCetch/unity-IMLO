using System;
using UnityEngine;
using UnityEngine.UI;

public class ImageItem : MonoBehaviour
{
    [SerializeField] private RawImage image;
    [SerializeField] private RawImage fade;
    [SerializeField] private float duration = 1f;
    [SerializeField] private Color originColor = new Color(1, 1, 1, 1);
    [SerializeField] private Color targetColor = new Color(1, 1, 1, 0);
    
    private bool _fading;
    private float _dt;
    private float _elapsed;

    public void SetTexture(Texture2D texture)
    {
        image.texture = texture;
        _fading = true;
    }

    private void Update()
    {
        if (!_fading)
            return;

        _dt += Time.smoothDeltaTime / duration;
        fade.color = Color.Lerp(originColor, targetColor, _dt);

        if (_dt > 1)
            _fading = false;
    }
}