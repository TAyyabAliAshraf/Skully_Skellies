using UnityEngine;
using DG.Tweening;

public class ScaleEffect : MonoBehaviour
{
    [Header("Scale Animation Settings")]
    public Vector3 fromScale = Vector3.one;
    public Vector3 toScale = Vector3.one * 1.2f;
    public float duration = 0.3f;
    public float delay = 0f;

    [Header("Options")]
    public bool loop = false;
    public bool goBack = false;

    private Tween scaleTween;

    void Start()
    {
        PlayScaleAnimation();
    }

    public void PlayScaleAnimation()
    {
        // Kill existing tween if active
        scaleTween?.Kill();

        // Set the starting scale
        transform.localScale = fromScale;

        if (loop)
        {
            scaleTween = transform.DOScale(toScale, duration)
                .SetDelay(delay)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else if (goBack)
        {
            Sequence seq = DOTween.Sequence();
            seq.AppendInterval(delay);
            seq.Append(transform.DOScale(toScale, duration).SetEase(Ease.OutBack));
            seq.Append(transform.DOScale(fromScale, duration).SetEase(Ease.InBack));
            scaleTween = seq;
        }
        else
        {
            scaleTween = transform.DOScale(toScale, duration)
                .SetDelay(delay)
                .SetEase(Ease.OutBack);
        }
    }

    public void StopScaleAnimation()
    {
        scaleTween?.Kill();
        transform.localScale = fromScale;
    }
}
